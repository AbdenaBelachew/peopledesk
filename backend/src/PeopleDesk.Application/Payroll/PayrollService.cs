using Microsoft.EntityFrameworkCore;
using PeopleDesk.Application.Attendance;
using FluentValidation;
using PeopleDesk.Application.Common;
using PeopleDesk.Domain.Common;
using PeopleDesk.Application.Notifications;
using PeopleDesk.Domain.Attendance;
using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.Employees;
using PeopleDesk.Domain.Leave;
using PeopleDesk.Domain.Payroll;

namespace PeopleDesk.Application.Payroll;

/// <summary>
/// Configurable payroll engine. Tax/pension rules come from effective-dated PayrollRuleSets
/// (per jurisdiction/version) — nothing country-specific is hardcoded here. Money is decimal only.
/// State machine: Draft → Calculated → Approved → Finalized (locked) → Reversed.
/// </summary>
public class PayrollService(
    IAppDbContext db,
    ICurrentUser user,
    IDateTime clock,
    ScheduleProvider schedules,
    IFileStorage storage,
    NotificationService notifications)
{
    private Guid OrgId => user.OrganizationId
        ?? throw new ForbiddenException("No organization context for this request.");

    /// <summary>Standard paid hours per month used to derive hourly rates (documented constant; org settings can override later).</summary>
    public const decimal StandardMonthlyHours = 208m;

    // ─── Rule sets ───────────────────────────────────────────────────────────────

    public async Task<List<object>> ListRuleSetsAsync(CancellationToken ct = default)
    {
        user.Ensure(Permissions.PayrollView);
        var sets = await db.PayrollRuleSets.AsNoTracking()
            .Where(r => r.OrganizationId == OrgId)
            .OrderByDescending(r => r.EffectiveFrom)
            .Select(r => new
            {
                r.Id, r.JurisdictionCode, r.Name, r.Version, r.EffectiveFrom, r.EffectiveTo,
                r.IsActive, r.PensionEmployeeRate, r.PensionEmployerRate,
                BracketCount = db.PayrollTaxBrackets.Count(b => b.RuleSetId == r.Id)
            })
            .ToListAsync(ct);
        return [.. sets];
    }

    public async Task<object> GetRuleSetAsync(Guid id, CancellationToken ct = default)
    {
        user.Ensure(Permissions.PayrollView);
        var set = await db.PayrollRuleSets.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == OrgId, ct)
            ?? throw new NotFoundException("Payroll rule set", id);
        var brackets = await db.PayrollTaxBrackets.AsNoTracking()
            .Where(b => b.RuleSetId == id).OrderBy(b => b.LowerBound)
            .Select(b => new { b.LowerBound, b.UpperBound, b.Rate, b.QuickDeduction })
            .ToListAsync(ct);
        return new
        {
            set.Id, set.JurisdictionCode, set.Name, set.Version, set.EffectiveFrom, set.EffectiveTo,
            set.IsActive, set.PensionEmployeeRate, set.PensionEmployerRate, Brackets = brackets
        };
    }

    /// <summary>Resolves the effective rule set for a period end date (latest EffectiveFrom ≤ date).</summary>
    public async Task<PayrollRuleSet> ResolveRuleSetAsync(DateOnly date, CancellationToken ct)
    {
        var set = await db.PayrollRuleSets.AsNoTracking()
            .Where(r => r.OrganizationId == OrgId && r.IsActive && r.EffectiveFrom <= date
                && (r.EffectiveTo == null || r.EffectiveTo >= date))
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException(
                $"No payroll rule set is effective for {date:yyyy-MM-dd}. Configure one under Payroll → Rule sets.");
        return set;
    }

    // ─── Periods ─────────────────────────────────────────────────────────────────

    public async Task<List<PayrollPeriodDto>> ListPeriodsAsync(CancellationToken ct = default)
    {
        user.Ensure(Permissions.PayrollView);
        var periods = await db.PayrollPeriods.AsNoTracking()
            .Include(p => p.ActiveRun)
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
            .ToListAsync(ct);
        return periods.Select(ToPeriodDto).ToList();
    }

    public async Task<PayrollPeriodDto> CreatePeriodAsync(CreatePeriodRequest request, CancellationToken ct = default)
    {
        user.Ensure(Permissions.PayrollRun);
        await new CreatePeriodRequestValidator().ValidateAndThrowAsync(request, ct);

        DateOnly start, end;
        if (request.Type == PayrollPeriodType.Monthly)
        {
            start = new DateOnly(request.Year, request.Month, 1);
            end = start.AddMonths(1).AddDays(-1);
        }
        else
        {
            // BiWeekly/Weekly: Month is interpreted as the month of the period start; 14/7 day spans.
            start = new DateOnly(request.Year, request.Month, 1);
            end = start.AddDays(request.Type == PayrollPeriodType.BiWeekly ? 13 : 6);
        }

        var overlaps = await db.PayrollPeriods.IgnoreQueryFilters().AnyAsync(p =>
            p.OrganizationId == OrgId && p.StartDate <= end && p.EndDate >= start, ct);
        if (overlaps)
            throw new ConflictException("A payroll period already covers these dates.");

        var period = new PayrollPeriod
        {
            OrganizationId = OrgId,
            Name = $"{start:yyyy-MM}",
            Type = request.Type,
            Year = request.Year,
            Month = request.Month,
            StartDate = start,
            EndDate = end,
            PayDate = request.PayDate
        };
        db.PayrollPeriods.Add(period);
        await db.SaveChangesAsync(ct);
        return ToPeriodDto(period);
    }

    public async Task DeletePeriodAsync(Guid periodId, CancellationToken ct = default)
    {
        user.Ensure(Permissions.PayrollRun);
        var period = await db.PayrollPeriods.Include(p => p.ActiveRun)
            .FirstOrDefaultAsync(p => p.Id == periodId, ct)
            ?? throw new NotFoundException("Payroll period", periodId);
        if (period.ActiveRun is not null)
            throw new BusinessRuleException("Delete the payroll run before deleting the period.");
        db.PayrollPeriods.Remove(period);
        await db.SaveChangesAsync(ct);
    }

    // ─── Runs ────────────────────────────────────────────────────────────────────

    public async Task<PayrollRunDto> CreateRunAsync(Guid periodId, CancellationToken ct = default)
    {
        user.Ensure(Permissions.PayrollRun);
        var period = await db.PayrollPeriods.Include(p => p.ActiveRun)
            .FirstOrDefaultAsync(p => p.Id == periodId, ct)
            ?? throw new NotFoundException("Payroll period", periodId);
        if (period.ActiveRun is not null)
            throw new BusinessRuleException($"Period {period.Name} already has a run ({period.ActiveRun.Status}).");

        var run = new PayrollRun
        {
            OrganizationId = OrgId,
            PayrollPeriodId = period.Id,
            Status = PayrollRunStatus.Draft
        };
        db.PayrollRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return ToRunDto(run, period, []);
    }

    public async Task<PayrollRunDto> CalculateAsync(Guid runId, CancellationToken ct = default)
    {
        user.Ensure(Permissions.PayrollRun);

        var run = await db.PayrollRuns.Include(r => r.Period)
            .FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new NotFoundException("Payroll run", runId);
        if (run.Status is PayrollRunStatus.Approved or PayrollRunStatus.Finalized)
            throw new BusinessRuleException("Approved or finalized runs cannot be recalculated. Reverse first.");

        var period = run.Period;
        var ruleSet = await ResolveRuleSetAsync(period.EndDate, ct);
        var brackets = await db.PayrollTaxBrackets.AsNoTracking()
            .Where(b => b.RuleSetId == ruleSet.Id)
            .Select(b => new IncomeTaxCalculator.Bracket(b.LowerBound, b.UpperBound, b.Rate, b.QuickDeduction))
            .ToListAsync(ct);

        // Clear previous calculation + adjustments stay (they re-apply).
        var oldItems = await db.PayrollItems.Where(i => i.PayrollRunId == run.Id).ToListAsync(ct);
        db.PayrollItems.RemoveRange(oldItems);

        var (tz, settings) = await schedules.GetOrgContextAsync(OrgId, ct);
        var periodStartLocal = ToUtc(period.StartDate, tz, startOfDay: true);
        var periodEndLocal = ToUtc(period.EndDate, tz, startOfDay: false);

        // Eligible employees: hired within/before the period and not terminated before it started.
        var employees = await db.Employees.AsNoTracking()
            .Where(e => e.HireDate <= period.EndDate
                && (e.TerminationDate == null || e.TerminationDate >= period.StartDate))
            .OrderBy(e => e.EmployeeNumber)
            .ToListAsync(ct);

        var skipped = new List<string>();
        decimal totalGross = 0, totalDed = 0, totalNet = 0;
        var now = clock.UtcNow;

        foreach (var employee in employees)
        {
            var structure = await db.SalaryStructures.AsNoTracking()
                .Include(s => s.Components)
                .Where(s => s.EmployeeId == employee.Id && s.EffectiveFrom <= period.EndDate
                    && (s.EffectiveTo == null || s.EffectiveTo >= period.StartDate))
                .OrderByDescending(s => s.EffectiveFrom)
                .FirstOrDefaultAsync(ct);

            if (structure is null)
            {
                skipped.Add($"{employee.EmployeeNumber} {employee.FirstName} {employee.LastName} (no salary structure)");
                continue;
            }

            var item = new PayrollItem
            {
                OrganizationId = OrgId,
                PayrollRunId = run.Id,
                EmployeeId = employee.Id,
                SalaryStructureId = structure.Id,
                Currency = structure.Currency
            };

            // ── Attendance-derived inputs ──
            var records = await db.AttendanceRecords.AsNoTracking()
                .Where(a => a.EmployeeId == employee.Id && a.WorkDate >= period.StartDate && a.WorkDate <= period.EndDate)
                .ToListAsync(ct);
            item.DaysWorked = records.Count(r => r.WorkedMinutes > 0);
            item.OvertimeHours = Math.Round(records.Sum(r => r.OvertimeMinutes) / 60m, 2);

            // ── Unpaid leave days (approved, unpaid leave types, within period) ──
            var unpaidLeaveDays = await db.LeaveRequests.AsNoTracking()
                .Where(lr => lr.EmployeeId == employee.Id && lr.Status == ReviewStatus.Approved
                    && lr.StartDate <= period.EndDate && lr.EndDate >= period.StartDate)
                .Join(db.LeaveTypes.Where(t => !t.IsPaid),
                    lr => lr.LeaveTypeId, t => t.Id, (lr, t) => lr)
                .SumAsync(lr => (decimal?)lr.Days, ct) ?? 0m;
            item.UnpaidLeaveDays = unpaidLeaveDays;

            var components = structure.Components.Where(c => c.IsActive).ToList();
            var basic = components.FirstOrDefault(c => c.Type == SalaryComponentType.Basic)?.Amount
                ?? components.Where(c => c.Kind == PayComponentKind.Earning).Sum(c => c.CalculationType == SalaryCalculationType.PercentOfBasic ? 0 : c.Amount);
            item.BasicSalary = basic;

            var earnings = new List<PayrollEarning>();
            var deductions = new List<PayrollDeduction>();

            decimal AddEarning(string code, string label, SalaryComponentType type, decimal amount)
            {
                amount = IncomeTaxCalculator.Round2(amount);
                earnings.Add(new PayrollEarning
                { OrganizationId = OrgId, PayrollItemId = item.Id, Code = code, Label = label, Type = type, Amount = amount });
                return amount;
            }

            // Structural earnings.
            var grossTaxable = 0m;
            var grossNonTaxable = 0m;
            foreach (var c in components.Where(c => c.Kind == PayComponentKind.Earning && c.Type != SalaryComponentType.Basic))
            {
                var amount = c.CalculationType == SalaryCalculationType.PercentOfBasic
                    ? basic * c.Amount / 100m
                    : c.Amount;
                amount = IncomeTaxCalculator.Round2(amount);
                if (c.IsTaxable) grossTaxable += amount; else grossNonTaxable += amount;
                AddEarning(c.Code, c.Label, c.Type, amount);
            }
            AddEarning("BASIC", "Basic Salary", SalaryComponentType.Basic, basic);
            grossTaxable += basic;

            // Overtime pay: raw OT hours × hourly rate × org multiplier.
            if (item.OvertimeHours > 0 && settings.OvertimeEnabled)
            {
                var hourly = basic / StandardMonthlyHours;
                var otPay = item.OvertimeHours * hourly * settings.OvertimeMultiplier;
                grossTaxable += IncomeTaxCalculator.Round2(otPay);
                AddEarning("OT", $"Overtime ({item.OvertimeHours:0.##} h)", SalaryComponentType.Overtime, otPay);
            }

            // Run adjustments (earnings — bonuses, commissions, one-offs).
            var adjustments = await db.PayrollRunAdjustments.AsNoTracking()
                .Where(a => a.PayrollRunId == run.Id && a.EmployeeId == employee.Id)
                .ToListAsync(ct);
            foreach (var a in adjustments.Where(a => a.Kind == PayComponentKind.Earning))
            {
                var amount = AddEarning(a.Code, a.Label, SalaryComponentType.Bonus, a.Amount);
                grossTaxable += amount;
            }

            var gross = grossTaxable + grossNonTaxable;

            // ── Statutory deductions from the resolved rule set ──
            var pensionEmployee = IncomeTaxCalculator.Round2(grossTaxable * ruleSet.PensionEmployeeRate);
            var taxableIncome = EthiopiaTaxable(grossTaxable, pensionEmployee);
            var incomeTax = IncomeTaxCalculator.Compute(taxableIncome, brackets);

            deductions.Add(new PayrollDeduction
            {
                OrganizationId = OrgId, PayrollItemId = item.Id, Code = "PENSION",
                Label = $"Pension ({ruleSet.PensionEmployeeRate:P1} employee)", Type = SalaryComponentType.Pension,
                Amount = pensionEmployee
            });
            deductions.Add(new PayrollDeduction
            {
                OrganizationId = OrgId, PayrollItemId = item.Id, Code = "INCOME_TAX",
                Label = $"Income tax ({ruleSet.JurisdictionCode} {ruleSet.Version})", Type = SalaryComponentType.Tax,
                Amount = incomeTax
            });

            var totalDeductions = pensionEmployee + incomeTax;

            // Structural non-tax deductions (loans, other).
            foreach (var c in components.Where(c => c.Kind == PayComponentKind.Deduction))
            {
                var amount = c.CalculationType == SalaryCalculationType.PercentOfBasic
                    ? basic * c.Amount / 100m
                    : c.Amount;
                amount = IncomeTaxCalculator.Round2(amount);
                deductions.Add(new PayrollDeduction
                { OrganizationId = OrgId, PayrollItemId = item.Id, Code = c.Code, Label = c.Label, Type = c.Type, Amount = amount });
                totalDeductions += amount;
            }

            // Unpaid leave deduction: pro-rata daily rate.
            if (unpaidLeaveDays > 0)
            {
                var workingDaysInPeriod = await WorkingDaysInPeriodAsync(period, ct);
                var dailyRate = workingDaysInPeriod > 0 ? basic / workingDaysInPeriod : basic / 30m;
                var unpaidAmount = IncomeTaxCalculator.Round2(unpaidLeaveDays * dailyRate);
                deductions.Add(new PayrollDeduction
                {
                    OrganizationId = OrgId, PayrollItemId = item.Id, Code = "UNPAID_LEAVE",
                    Label = $"Unpaid leave ({unpaidLeaveDays:0.##} d)", Type = SalaryComponentType.Other,
                    Amount = unpaidAmount
                });
                totalDeductions += unpaidAmount;
            }

            // Run adjustments (deductions).
            foreach (var a in adjustments.Where(a => a.Kind == PayComponentKind.Deduction))
            {
                deductions.Add(new PayrollDeduction
                { OrganizationId = OrgId, PayrollItemId = item.Id, Code = a.Code, Label = a.Label, Type = SalaryComponentType.Other, Amount = a.Amount });
                totalDeductions += a.Amount;
            }

            var gross2 = IncomeTaxCalculator.Round2(gross);
            var totalDed2 = IncomeTaxCalculator.Round2(totalDeductions);
            item.TotalEarnings = gross2;
            item.GrossSalary = gross2;
            item.TotalDeductions = totalDed2;
            item.NetSalary = IncomeTaxCalculator.Round2(gross2 - totalDed2);

            db.PayrollItems.Add(item);
            db.PayrollEarnings.AddRange(earnings);
            db.PayrollDeductions.AddRange(deductions);

            totalGross += gross2;
            totalDed += totalDed2;
            totalNet += item.NetSalary;
        }

        run.Status = PayrollRunStatus.Calculated;
        run.CalculatedAtUtc = now;
        run.CalculatedBy = user.Email;
        run.TotalGross = IncomeTaxCalculator.Round2(totalGross);
        run.TotalDeductions = IncomeTaxCalculator.Round2(totalDed);
        run.TotalNet = IncomeTaxCalculator.Round2(totalNet);

        await db.SaveChangesAsync(ct);
        run.EmployeeCount = await db.PayrollItems.CountAsync(i => i.PayrollRunId == run.Id, ct);
        await db.SaveChangesAsync(ct);

        return ToRunDto(run, period, skipped);
    }

    public async Task<PayrollRunDto> ApproveAsync(Guid runId, CancellationToken ct = default)
    {
        user.Ensure(Permissions.PayrollApprove);
        var run = await db.PayrollRuns.Include(r => r.Period)
            .FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new NotFoundException("Payroll run", runId);
        if (run.Status != PayrollRunStatus.Calculated)
            throw new BusinessRuleException("Only calculated runs can be approved. Calculate first.");

        run.Status = PayrollRunStatus.Approved;
        run.ApprovedBy = user.Email;
        run.ApprovedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToRunDto(run, run.Period, []);
    }

    public async Task<PayrollRunDto> FinalizeAsync(Guid runId, CancellationToken ct = default)
    {
        user.Ensure(Permissions.PayrollFinalize);
        var run = await db.PayrollRuns.Include(r => r.Period)
            .FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new NotFoundException("Payroll run", runId);
        if (run.Status != PayrollRunStatus.Approved)
            throw new BusinessRuleException("Only approved runs can be finalized.");
        var itemCount = await db.PayrollItems.CountAsync(i => i.PayrollRunId == run.Id, ct);
        if (itemCount == 0)
            throw new BusinessRuleException("Nothing to finalize: this run has no payroll items.");

        // Lock attendance records inside the period so past calculations stay stable.
        var locked = await db.AttendanceRecords
            .Where(a => a.WorkDate >= run.Period.StartDate && a.WorkDate <= run.Period.EndDate && !a.IsLocked)
            .ToListAsync(ct);
        foreach (var a in locked) a.IsLocked = true;

        run.Status = PayrollRunStatus.Finalized;
        run.FinalizedBy = user.Email;
        run.FinalizedAtUtc = clock.UtcNow;
        run.Period.Closed = true;
        await db.SaveChangesAsync(ct);

        await notifications.NotifyApproversAsync(NotificationType.PayrollCompleted,
            "Payroll finalized",
            $"Payroll for {run.Period.Name} was finalized: {run.EmployeeCount} employees, net {run.TotalNet:0.00}.",
            "/payroll", ct);

        return ToRunDto(run, run.Period, []);
    }

    public async Task<PayrollRunDto> ReverseAsync(Guid runId, ReverseRunRequest request, CancellationToken ct = default)
    {
        user.Ensure(Permissions.PayrollReverse);
        if (string.IsNullOrWhiteSpace(request.Note))
            throw new BadRequestException("A reversal note is required for the audit trail.");

        var run = await db.PayrollRuns.Include(r => r.Period)
            .FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new NotFoundException("Payroll run", runId);
        if (run.Status != PayrollRunStatus.Finalized)
            throw new BusinessRuleException("Only finalized runs can be reversed.");

        // Unlock attendance records inside the period.
        var locked = await db.AttendanceRecords
            .Where(a => a.WorkDate >= run.Period.StartDate && a.WorkDate <= run.Period.EndDate && a.IsLocked)
            .ToListAsync(ct);
        foreach (var a in locked) a.IsLocked = false;

        // Void the payslips of the reversed run (files + rows); the corrected run regenerates them.
        var payslips = await db.Payslips.Where(p => p.PayrollRunId == runId).ToListAsync(ct);
        foreach (var payslip in payslips)
        {
            await storage.DeleteAsync(payslip.StorageKey, ct);
            db.Payslips.Remove(payslip);
        }

        run.Status = PayrollRunStatus.Reversed;
        run.ReversedBy = user.Email;
        run.ReversedAtUtc = clock.UtcNow;
        run.ReversalNote = request.Note;
        run.Period.Closed = false;

        await db.SaveChangesAsync(ct);
        return ToRunDto(run, run.Period, []);
    }

    public async Task<PayrollRunDto> GetRunAsync(Guid runId, CancellationToken ct = default)
    {
        user.Ensure(Permissions.PayrollView);
        var run = await db.PayrollRuns.AsNoTracking().Include(r => r.Period)
            .FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new NotFoundException("Payroll run", runId);
        return ToRunDto(run, run.Period, []);
    }

    public async Task<PagedResult<PayrollItemDto>> GetRunItemsAsync(Guid runId, PagedRequest page, CancellationToken ct = default)
    {
        user.Ensure(Permissions.PayrollView);
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new NotFoundException("Payroll run", runId);

        var query = db.PayrollItems.AsNoTracking().Where(i => i.PayrollRunId == run.Id);
        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(Math.Clamp(page.PageSize, 1, 100)).ToListAsync(ct);

        var employeeIds = items.Select(i => i.EmployeeId).ToList();
        var employees = await db.Employees.AsNoTracking()
            .Where(e => employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        var dtos = new List<PayrollItemDto>(items.Count);
        foreach (var item in items)
        {
            var earnings = await db.PayrollEarnings.AsNoTracking().Where(x => x.PayrollItemId == item.Id).ToListAsync(ct);
            var deductions = await db.PayrollDeductions.AsNoTracking().Where(x => x.PayrollItemId == item.Id).ToListAsync(ct);
            var e = employees.GetValueOrDefault(item.EmployeeId);
            dtos.Add(new PayrollItemDto
            {
                Id = item.Id,
                EmployeeId = item.EmployeeId,
                EmployeeName = e is null ? "?" : $"{e.FirstName} {e.LastName}",
                EmployeeNumber = e?.EmployeeNumber ?? "?",
                Currency = item.Currency,
                DaysWorked = item.DaysWorked,
                UnpaidLeaveDays = item.UnpaidLeaveDays,
                OvertimeHours = item.OvertimeHours,
                BasicSalary = item.BasicSalary,
                TotalEarnings = item.TotalEarnings,
                TotalDeductions = item.TotalDeductions,
                GrossSalary = item.GrossSalary,
                NetSalary = item.NetSalary,
                Earnings = earnings.Select(x => new PayrollLineDto { Code = x.Code, Label = x.Label, Type = x.Type.ToString(), Amount = x.Amount }).ToList(),
                Deductions = deductions.Select(x => new PayrollLineDto { Code = x.Code, Label = x.Label, Type = x.Type.ToString(), Amount = x.Amount }).ToList()
            });
        }
        return new PagedResult<PayrollItemDto>(dtos, page.Page, page.PageSize, total);
    }

    public async Task AddAdjustmentAsync(Guid runId, AddAdjustmentRequest request, CancellationToken ct = default)
    {
        user.Ensure(Permissions.PayrollRun);
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new NotFoundException("Payroll run", runId);
        if (run.Status is PayrollRunStatus.Approved or PayrollRunStatus.Finalized)
            throw new BusinessRuleException("Adjustments can only be added to draft or calculated runs.");
        if (request.Amount <= 0)
            throw new BadRequestException("Adjustment amount must be positive.");
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Label))
            throw new BadRequestException("Adjustment code and label are required.");

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.EmployeeId, ct)
            ?? throw new NotFoundException("Employee", request.EmployeeId);
        _ = employee;

        db.PayrollRunAdjustments.Add(new PayrollRunAdjustment
        {
            OrganizationId = OrgId,
            PayrollRunId = runId,
            EmployeeId = request.EmployeeId,
            Kind = request.Kind,
            Code = request.Code.Trim().ToUpperInvariant(),
            Label = request.Label.Trim(),
            Amount = request.Amount,
            Note = request.Note
        });
        await db.SaveChangesAsync(ct);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private static decimal EthiopiaTaxable(decimal grossTaxable, decimal pensionEmployee)
        => EthiopiaPayroll.TaxableIncome(grossTaxable, pensionEmployee);

    private async Task<int> WorkingDaysInPeriodAsync(PayrollPeriod period, CancellationToken ct)
    {
        var holidays = await schedules.GetHolidayDatesAsync(OrgId, period.StartDate.Year, ct);
        var count = 0;
        for (var d = period.StartDate; d <= period.EndDate; d = d.AddDays(1))
        {
            if (holidays.Contains(d)) continue;
            var day = await schedules.ResolveDayAsync(OrgId, null, d, ct);
            if (day.IsWorkingDay) count++;
        }
        return Math.Max(1, count);
    }

    private static DateTime ToUtc(DateOnly date, TimeZoneInfo tz, bool startOfDay)
    {
        var local = startOfDay
            ? date.ToDateTime(TimeOnly.MinValue)
            : date.ToDateTime(new TimeOnly(23, 59, 59));
        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }

    private static PayrollPeriodDto ToPeriodDto(PayrollPeriod p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Type = p.Type,
        Year = p.Year,
        Month = p.Month,
        StartDate = p.StartDate,
        EndDate = p.EndDate,
        PayDate = p.PayDate,
        Closed = p.Closed,
        ActiveRunId = p.ActiveRun?.Id,
        RunStatus = p.ActiveRun?.Status.ToString(),
        TotalNet = p.ActiveRun?.TotalNet
    };

    private static PayrollRunDto ToRunDto(PayrollRun r, PayrollPeriod period, List<string> skipped) => new()
    {
        Id = r.Id,
        PayrollPeriodId = r.PayrollPeriodId,
        PeriodName = period.Name,
        Status = r.Status.ToString(),
        TotalGross = r.TotalGross,
        TotalDeductions = r.TotalDeductions,
        TotalNet = r.TotalNet,
        EmployeeCount = r.EmployeeCount,
        CalculatedAtUtc = r.CalculatedAtUtc,
        CalculatedBy = r.CalculatedBy,
        ApprovedBy = r.ApprovedBy,
        FinalizedBy = r.FinalizedBy,
        FinalizedAtUtc = r.FinalizedAtUtc,
        ReversalNote = r.ReversalNote,
        SkippedEmployees = skipped
    };
}
