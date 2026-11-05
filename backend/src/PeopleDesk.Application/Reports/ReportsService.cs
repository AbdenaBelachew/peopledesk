using Microsoft.EntityFrameworkCore;
using PeopleDesk.Application.Common;
using PeopleDesk.Domain.Common;

namespace PeopleDesk.Application.Reports;

public record HeadcountReportDto
{
    public int Total { get; init; }
    public int Active { get; init; }
    public int OnProbation { get; init; }
    public int TerminatedOrResigned { get; init; }
    public IReadOnlyList<GroupCount> ByDepartment { get; init; } = [];
    public IReadOnlyList<GroupCount> ByBranch { get; init; } = [];
    public IReadOnlyList<GroupCount> ByEmploymentType { get; init; } = [];
    public IReadOnlyList<GroupCount> ByGender { get; init; } = [];
}

public record GroupCount(string Label, int Count);

public record AttendanceTrendDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public IReadOnlyList<TrendPoint> Points { get; init; } = [];
    public decimal AvgLateMinutesPerDay { get; init; }
    public int TotalOvertimeMinutes { get; init; }
}

public record TrendPoint(DateOnly Date, int Present, int Late, int Absent, int OnLeave, int OvertimeMinutes);

public record LeaveUtilizationDto
{
    public int Year { get; init; }
    public IReadOnlyList<LeaveTypeUsage> ByType { get; init; } = [];
}

public record LeaveTypeUsage(string TypeName, string TypeCode, string? ColorHex, decimal Entitled, decimal Used, decimal Pending);

public record PayrollExpenseDto
{
    public IReadOnlyList<PayrollPeriodExpense> Periods { get; init; } = [];
    public decimal TotalGross { get; init; }
    public decimal TotalNet { get; init; }
    public decimal TotalOvertimeCost { get; init; }
}

public record PayrollPeriodExpense(string Period, decimal Gross, decimal Deductions, decimal Net, int Employees);

public record TurnoverReportDto
{
    public int Year { get; init; }
    public int StartingHeadcount { get; init; }
    public int NewHires { get; init; }
    public int Exits { get; init; }
    public decimal TurnoverRate { get; init; }
    public IReadOnlyList<GroupCount> ExitsByMonth { get; init; } = [];
}

public class ReportsService(IAppDbContext db, ICurrentUser user, IDateTime clock, IXlsxExporter xlsx)
{
    private readonly IXlsxExporter _xlsx = xlsx;

    private Guid OrgId => user.OrganizationId
        ?? throw new ForbiddenException("No organization context for this request.");

    // ─── Headcount ───────────────────────────────────────────────────────────────

    public async Task<HeadcountReportDto> HeadcountAsync(CancellationToken ct = default)
    {
        user.Ensure(Permissions.ReportsView);

        var employees = await db.Employees.AsNoTracking().ToListAsync(ct);
        var departments = await db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var branches = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        var types = await db.EmploymentTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var byDepartment = employees
            .GroupBy(e => e.DepartmentId)
            .Select(g => new GroupCount(
                g.Key is { } id ? departments.GetValueOrDefault(id, "(deleted)") : "Unassigned",
                g.Count()))
            .OrderByDescending(x => x.Count).ToList();

        var byBranch = employees
            .GroupBy(e => e.BranchId)
            .Select(g => new GroupCount(
                g.Key is { } id ? branches.GetValueOrDefault(id, "(deleted)") : "Unassigned",
                g.Count()))
            .OrderByDescending(x => x.Count).ToList();

        var byType = employees
            .GroupBy(e => e.EmploymentTypeId)
            .Select(g => new GroupCount(
                g.Key is { } id ? types.GetValueOrDefault(id, "(deleted)") : "Unspecified",
                g.Count()))
            .OrderByDescending(x => x.Count).ToList();

        var byGender = employees
            .GroupBy(e => e.Gender)
            .Select(g => new GroupCount(g.Key.ToString(), g.Count()))
            .OrderByDescending(x => x.Count).ToList();

        return new HeadcountReportDto
        {
            Total = employees.Count,
            Active = employees.Count(e => e.Status == EmploymentStatus.Active),
            OnProbation = employees.Count(e => e.Status == EmploymentStatus.Probation),
            TerminatedOrResigned = employees.Count(e => e.Status is EmploymentStatus.Terminated or EmploymentStatus.Resigned or EmploymentStatus.Retired),
            ByDepartment = byDepartment,
            ByBranch = byBranch,
            ByEmploymentType = byType,
            ByGender = byGender
        };
    }

    // ─── Attendance trends ───────────────────────────────────────────────────────

    public async Task<AttendanceTrendDto> AttendanceTrendAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        user.Ensure(Permissions.ReportsView);

        var records = await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.WorkDate >= from && a.WorkDate <= to)
            .ToListAsync(ct);

        var points = records.GroupBy(r => r.WorkDate).OrderBy(g => g.Key)
            .Select(g => new TrendPoint(
                g.Key,
                g.Count(r => r.Status is AttendanceStatus.Present or AttendanceStatus.Late or AttendanceStatus.Remote),
                g.Count(r => r.LateMinutes > 0),
                g.Count(r => r.Status == AttendanceStatus.Absent),
                g.Count(r => r.Status == AttendanceStatus.OnLeave),
                g.Sum(r => r.OvertimeMinutes)))
            .ToList();

        return new AttendanceTrendDto
        {
            From = from,
            To = to,
            Points = points,
            AvgLateMinutesPerDay = points.Count == 0 ? 0 : Math.Round((decimal)records.Sum(r => r.LateMinutes) / points.Count, 1),
            TotalOvertimeMinutes = records.Sum(r => r.OvertimeMinutes)
        };
    }

    // ─── Leave utilization ───────────────────────────────────────────────────────

    public async Task<LeaveUtilizationDto> LeaveUtilizationAsync(int year, CancellationToken ct = default)
    {
        user.Ensure(Permissions.ReportsView);

        var types = await db.LeaveTypes.AsNoTracking().ToListAsync(ct);
        var balances = await db.LeaveBalances.AsNoTracking()
            .Where(b => b.Year == year).ToListAsync(ct);

        var usage = types.Select(t =>
        {
            var typeBalances = balances.Where(b => b.LeaveTypeId == t.Id).ToList();
            return new LeaveTypeUsage(
                t.Name, t.Code, t.ColorHex,
                typeBalances.Sum(b => b.EntitledDays + b.CarriedOverDays + b.AccruedDays + b.AdjustedDays),
                typeBalances.Sum(b => b.UsedDays),
                typeBalances.Sum(b => b.PendingDays));
        }).OrderByDescending(u => u.Used).ToList();

        return new LeaveUtilizationDto { Year = year, ByType = usage };
    }

    // ─── Payroll expenses ────────────────────────────────────────────────────────

    public async Task<PayrollExpenseDto> PayrollExpensesAsync(CancellationToken ct = default)
    {
        user.Ensure(Permissions.ReportsView);

        var runs = await db.PayrollRuns.AsNoTracking()
            .Include(r => r.Period)
            .Where(r => r.Status == PayrollRunStatus.Finalized || r.Status == PayrollRunStatus.Approved || r.Status == PayrollRunStatus.Calculated)
            .OrderBy(r => r.Period.StartDate)
            .ToListAsync(ct);

        var periods = runs.Select(r => new PayrollPeriodExpense(
            r.Period.Name, r.TotalGross, r.TotalDeductions, r.TotalNet, r.EmployeeCount)).ToList();

        var otItems = await db.PayrollItems.AsNoTracking()
            .Join(db.PayrollEarnings, i => i.Id, e => e.PayrollItemId, (i, e) => new { i.PayrollRunId, e.Code, e.Amount })
            .Join(db.PayrollRuns.Where(r => r.Status == PayrollRunStatus.Finalized), x => x.PayrollRunId, r => r.Id, (x, r) => new { x.Code, x.Amount })
            .ToListAsync(ct);
        var otCost = otItems.Where(x => x.Code == "OT").Sum(x => x.Amount);

        return new PayrollExpenseDto
        {
            Periods = periods,
            TotalGross = periods.Sum(p => p.Gross),
            TotalNet = periods.Sum(p => p.Net),
            TotalOvertimeCost = otCost
        };
    }

    // ─── Turnover ────────────────────────────────────────────────────────────────

    public async Task<TurnoverReportDto> TurnoverAsync(int year, CancellationToken ct = default)
    {
        user.Ensure(Permissions.ReportsView);

        var exits = await db.Employees.AsNoTracking()
            .Where(e => e.TerminationDate != null && e.TerminationDate.Value.Year == year)
            .ToListAsync(ct);
        var hires = await db.Employees.AsNoTracking()
            .Where(e => e.HireDate.Year == year)
            .CountAsync(ct);
        var starting = await db.Employees.AsNoTracking()
            .CountAsync(e => e.HireDate < new DateOnly(year, 1, 1)
                && (e.TerminationDate == null || e.TerminationDate.Value.Year >= year));

        var avg = starting + hires == 0 ? 0 : (starting + hires) / 2m;
        var rate = avg == 0 ? 0 : Math.Round(exits.Count / avg * 100m, 1);

        var exitsByMonth = exits
            .GroupBy(e => e.TerminationDate!.Value.Month)
            .Select(g => new GroupCount(new DateTime(year, g.Key, 1).ToString("MMM"), g.Count()))
            .OrderBy(x => DateTime.ParseExact(x.Label, "MMM", null).Month).ToList();

        return new TurnoverReportDto
        {
            Year = year,
            StartingHeadcount = starting,
            NewHires = hires,
            Exits = exits.Count,
            TurnoverRate = rate,
            ExitsByMonth = exitsByMonth
        };
    }

    // ─── CSV / XLSX exports ──────────────────────────────────────────────────────

    public async Task<(byte[] Content, string ContentType, string FileName)> ExportHeadcountAsync(bool xlsxFormat, CancellationToken ct = default)
    {
        var report = await HeadcountAsync(ct);
        var employees = await db.Employees.AsNoTracking().ToListAsync(ct);
        var departments = await db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var branches = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        string[] headers = ["EmployeeNumber", "FullName", "Status", "HireDate", "Department", "Branch", "WorkEmail"];
        var rows = employees.OrderBy(e => e.EmployeeNumber).Select(e => new object?[]
        {
            e.EmployeeNumber, $"{e.FirstName} {e.LastName}", e.Status.ToString(), e.HireDate,
            e.DepartmentId is { } d ? departments.GetValueOrDefault(d) : null,
            e.BranchId is { } b ? branches.GetValueOrDefault(b) : null,
            e.WorkEmail
        });

        return BuildExport("headcount", headers, rows, xlsxFormat);
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> ExportAttendanceAsync(DateOnly from, DateOnly to, bool xlsxFormat, CancellationToken ct = default)
    {
        user.Ensure(Permissions.ReportsView);
        var records = await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.WorkDate >= from && a.WorkDate <= to)
            .OrderBy(a => a.WorkDate).ToListAsync(ct);
        var employeeIds = records.Select(r => r.EmployeeId).Distinct().ToList();
        var employees = await db.Employees.AsNoTracking()
            .Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);

        string[] headers = ["Date", "EmployeeNumber", "EmployeeName", "ClockInUtc", "ClockOutUtc", "WorkedMinutes", "LateMinutes", "OvertimeMinutes", "Status", "Source"];
        var rows = records.Select(r =>
        {
            var e = employees.GetValueOrDefault(r.EmployeeId);
            return new object?[]
            {
                r.WorkDate, e?.EmployeeNumber, e is null ? "?" : $"{e.FirstName} {e.LastName}",
                r.ClockInAtUtc?.ToString("yyyy-MM-dd HH:mm"), r.ClockOutAtUtc?.ToString("yyyy-MM-dd HH:mm"),
                r.WorkedMinutes, r.LateMinutes, r.OvertimeMinutes, r.Status.ToString(), r.Source.ToString()
            };
        });

        return BuildExport($"attendance-{from:yyyyMMdd}-{to:yyyyMMdd}", headers, rows, xlsxFormat);
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> ExportPayrollAsync(Guid runId, bool xlsxFormat, CancellationToken ct = default)
    {
        user.Ensure(Permissions.PayrollView);
        var run = await db.PayrollRuns.AsNoTracking().Include(r => r.Period)
            .FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new NotFoundException("Payroll run", runId);
        var items = await db.PayrollItems.AsNoTracking().Where(i => i.PayrollRunId == runId).ToListAsync(ct);
        var employeeIds = items.Select(i => i.EmployeeId).ToList();
        var employees = await db.Employees.AsNoTracking()
            .Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);

        string[] headers = ["EmployeeNumber", "EmployeeName", "DaysWorked", "OvertimeHours", "UnpaidLeaveDays", "BasicSalary", "GrossSalary", "TotalDeductions", "NetSalary", "Currency"];
        var rows = items.Select(i =>
        {
            var e = employees.GetValueOrDefault(i.EmployeeId);
            return new object?[]
            {
                e?.EmployeeNumber, e is null ? "?" : $"{e.FirstName} {e.LastName}",
                i.DaysWorked, i.OvertimeHours, i.UnpaidLeaveDays,
                i.BasicSalary, i.GrossSalary, i.TotalDeductions, i.NetSalary, i.Currency
            };
        });

        return BuildExport($"payroll-{run.Period.Name}", headers, rows, xlsxFormat);
    }

    private (byte[], string, string) BuildExport(string name, string[] headers, IEnumerable<IEnumerable<object?>> rows, bool xlsxFormat)
    {
        if (xlsxFormat)
            return (_xlsx.Export(name, headers, rows),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{name}.xlsx");
        var csv = CsvBuilder.BuildBytes(headers, rows);
        return (csv, "text/csv", $"{name}.csv");
    }
}
