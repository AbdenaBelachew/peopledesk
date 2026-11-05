using Microsoft.EntityFrameworkCore;
using FluentValidation;
using PeopleDesk.Application.Common;
using PeopleDesk.Domain.Attendance;
using PeopleDesk.Domain.Common;

namespace PeopleDesk.Application.Attendance;

public class AttendanceService(
    IAppDbContext db,
    ICurrentUser user,
    IDateTime clock,
    ScheduleProvider schedules)
{
    private Guid OrgId => user.OrganizationId
        ?? throw new ForbiddenException("No organization context for this request.");

    // ─── Clock in / out (self-service) ───────────────────────────────────────────

    public async Task<ClockActionDto> ClockInAsync(CancellationToken ct = default)
    {
        var employeeId = RequireEmployeeId();
        var (tz, settings) = await schedules.GetOrgContextAsync(OrgId, ct);

        var now = clock.UtcNow;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, tz);
        var workDate = DateOnly.FromDateTime(localNow);

        var existing = await db.AttendanceRecords.FirstOrDefaultAsync(a =>
            a.EmployeeId == employeeId && a.WorkDate == workDate, ct);

        if (existing?.ClockInAtUtc is not null)
            throw new BusinessRuleException($"You already clocked in today at {TimeZoneInfo.ConvertTimeFromUtc(existing.ClockInAtUtc.Value, tz):HH:mm}.");

        var day = await schedules.ResolveForEmployeeAsync(OrgId, employeeId, workDate, ct);

        AttendanceRecord record;
        if (existing is null)
        {
            record = new AttendanceRecord { OrganizationId = OrgId, EmployeeId = employeeId, WorkDate = workDate };
            db.AttendanceRecords.Add(record);
        }
        else
        {
            record = existing;
        }

        record.ClockInAtUtc = now;
        record.Source = AttendanceSource.SelfClock;

        var result = AttendanceCalculator.Evaluate(day, now, record.ClockOutAtUtc, record.BreakMinutes,
            settings.LateGraceMinutes, settings.OvertimeEnabled, settings.OvertimeMultiplier, tz);
        record.Status = result.Status;
        record.LateMinutes = result.LateMinutes;
        record.WorkedMinutes = result.WorkedMinutes;

        await db.SaveChangesAsync(ct);

        return new ClockActionDto
        {
            RecordId = record.Id, Action = "clock-in", AtUtc = now, WorkDate = workDate,
            LateMinutes = result.LateMinutes, WorkedMinutes = result.WorkedMinutes, Status = result.Status.ToString()
        };
    }

    public async Task<ClockActionDto> ClockOutAsync(CancellationToken ct = default)
    {
        var employeeId = RequireEmployeeId();
        var (tz, settings) = await schedules.GetOrgContextAsync(OrgId, ct);

        var now = clock.UtcNow;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, tz);
        var workDate = DateOnly.FromDateTime(localNow);

        // If the clock-out happens after midnight local time, it still closes yesterday's open record.
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(a =>
            a.EmployeeId == employeeId && a.ClockInAtUtc != null && a.ClockOutAtUtc == null, ct);

        if (record is null)
            throw new BusinessRuleException("No open clock-in found. Clock in first.");

        record.ClockOutAtUtc = now;
        var day = await schedules.ResolveForEmployeeAsync(OrgId, employeeId, record.WorkDate, ct);
        var result = AttendanceCalculator.Evaluate(day, record.ClockInAtUtc, now, record.BreakMinutes,
            settings.LateGraceMinutes, settings.OvertimeEnabled, settings.OvertimeMultiplier, tz);

        record.WorkedMinutes = result.WorkedMinutes;
        record.LateMinutes = result.LateMinutes;
        record.EarlyDepartureMinutes = result.EarlyDepartureMinutes;
        record.OvertimeMinutes = result.OvertimeMinutes;
        record.Status = result.Status;

        await db.SaveChangesAsync(ct);

        return new ClockActionDto
        {
            RecordId = record.Id, Action = "clock-out", AtUtc = now, WorkDate = record.WorkDate,
            LateMinutes = result.LateMinutes, WorkedMinutes = result.WorkedMinutes, Status = result.Status.ToString()
        };
    }

    public async Task<AttendanceDto?> GetTodayAsync(CancellationToken ct = default)
    {
        var employeeId = RequireEmployeeId();
        var (tz, _) = await schedules.GetOrgContextAsync(OrgId, ct);
        var workDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, tz));

        var record = await db.AttendanceRecords.AsNoTracking()
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.WorkDate == workDate, ct);
        return record is null ? null : await ToDtoAsync(record, ct);
    }

    // ─── Listing / timesheets ────────────────────────────────────────────────────

    public async Task<PagedResult<AttendanceDto>> ListAsync(PagedRequest page, Guid? employeeId,
        DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        // Employees may see only their own records; managers need viewAll.
        Guid? effectiveEmployeeId = employeeId;
        if (!user.Has(Permissions.AttendanceViewAll))
        {
            if (!user.Has(Permissions.AttendanceViewOwn))
                throw new ForbiddenException("Missing permission: attendance.view");
            effectiveEmployeeId = user.EmployeeId
                ?? throw new ForbiddenException("Your account is not linked to an employee record.");
        }

        var query = db.AttendanceRecords.AsNoTracking();
        if (effectiveEmployeeId is { } emp) query = query.Where(a => a.EmployeeId == emp);
        if (from is { } f) query = query.Where(a => a.WorkDate >= f);
        if (to is { } t) query = query.Where(a => a.WorkDate <= t);

        query = page.SortDesc ? query.OrderByDescending(a => a.WorkDate) : query.OrderBy(a => a.WorkDate);

        var total = await query.CountAsync(ct);
        var records = await query.Skip(page.Skip).Take(Math.Clamp(page.PageSize, 1, 100)).ToListAsync(ct);

        var dtos = new List<AttendanceDto>(records.Count);
        foreach (var r in records) dtos.Add(await ToDtoAsync(r, ct));
        return new PagedResult<AttendanceDto>(dtos, page.Page, page.PageSize, total);
    }

    // ─── Manual entry (HR) ───────────────────────────────────────────────────────

    public async Task<AttendanceDto> ManualSaveAsync(ManualAttendanceRequest request, CancellationToken ct = default)
    {
        user.Ensure(Permissions.AttendanceManage);
        await new ManualAttendanceRequestValidator().ValidateAndThrowAsync(request, ct);

        var (tz, settings) = await schedules.GetOrgContextAsync(OrgId, ct);
        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.EmployeeId, ct)
            ?? throw new NotFoundException("Employee", request.EmployeeId);

        var record = await db.AttendanceRecords.FirstOrDefaultAsync(a =>
            a.EmployeeId == request.EmployeeId && a.WorkDate == request.WorkDate, ct);

        if (record is { IsLocked: true })
            throw new BusinessRuleException("This attendance record is locked by a finalized payroll run.");

        if (record is null)
        {
            record = new AttendanceRecord
            {
                OrganizationId = OrgId,
                EmployeeId = request.EmployeeId,
                WorkDate = request.WorkDate
            };
            db.AttendanceRecords.Add(record);
        }

        record.ClockInAtUtc = request.ClockInUtc;
        record.ClockOutAtUtc = request.ClockOutUtc;
        record.BreakMinutes = request.BreakMinutes;
        record.Source = AttendanceSource.ManualEntry;
        record.Note = request.Note;
        record.ManualEntryBy = user.Email;

        var day = await schedules.ResolveForEmployeeAsync(OrgId, request.EmployeeId, request.WorkDate, ct);
        var result = AttendanceCalculator.Evaluate(day, request.ClockInUtc, request.ClockOutUtc, request.BreakMinutes,
            settings.LateGraceMinutes, settings.OvertimeEnabled, settings.OvertimeMultiplier, tz);

        record.WorkedMinutes = result.WorkedMinutes;
        record.LateMinutes = result.LateMinutes;
        record.EarlyDepartureMinutes = result.EarlyDepartureMinutes;
        record.OvertimeMinutes = result.OvertimeMinutes;
        record.Status = request.Status == AttendanceStatus.Present && !day.IsWorkingDay
            ? result.Status
            : request.Status == AttendanceStatus.OnLeave || request.Status == AttendanceStatus.Holiday
                ? request.Status
                : result.Status;

        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(record, ct);
    }

    // ─── Correction workflow ─────────────────────────────────────────────────────

    public async Task<CorrectionRequestDto> CreateCorrectionAsync(CreateCorrectionRequest request, CancellationToken ct = default)
    {
        var employeeId = RequireEmployeeId();
        await new CreateCorrectionRequestValidator().ValidateAndThrowAsync(request, ct);

        var duplicate = await db.AttendanceCorrectionRequests.AnyAsync(c =>
            c.EmployeeId == employeeId && c.WorkDate == request.WorkDate && c.Status == ReviewStatus.Pending, ct);
        if (duplicate)
            throw new ConflictException("A correction request for this date is already pending.");

        var correction = new AttendanceCorrectionRequest
        {
            OrganizationId = OrgId,
            EmployeeId = employeeId,
            WorkDate = request.WorkDate,
            RequestedClockInAtUtc = request.RequestedClockInUtc,
            RequestedClockOutAtUtc = request.RequestedClockOutUtc,
            RequestedBreakMinutes = request.RequestedBreakMinutes,
            Reason = request.Reason.Trim()
        };
        db.AttendanceCorrectionRequests.Add(correction);
        await db.SaveChangesAsync(ct);

        return (await ToCorrectionDtoAsync(correction, ct));
    }

    public async Task<CorrectionRequestDto> ReviewCorrectionAsync(Guid correctionId, ReviewCorrectionRequest request, CancellationToken ct = default)
    {
        user.Ensure(Permissions.AttendanceApprove);

        var correction = await db.AttendanceCorrectionRequests
            .FirstOrDefaultAsync(c => c.Id == correctionId, ct)
            ?? throw new NotFoundException("Correction request", correctionId);
        if (correction.Status != ReviewStatus.Pending)
            throw new BusinessRuleException("This correction request was already reviewed.");

        var (tz, settings) = await schedules.GetOrgContextAsync(OrgId, ct);
        correction.Status = request.Approve ? ReviewStatus.Approved : ReviewStatus.Rejected;
        correction.ReviewedBy = user.Email;
        correction.ReviewedAtUtc = clock.UtcNow;
        correction.ReviewNote = request.ReviewNote;

        if (request.Approve)
        {
            var record = await db.AttendanceRecords.FirstOrDefaultAsync(a =>
                a.EmployeeId == correction.EmployeeId && a.WorkDate == correction.WorkDate, ct);

            if (record is { IsLocked: true })
                throw new BusinessRuleException("This attendance record is locked by a finalized payroll run.");

            if (record is null)
            {
                record = new AttendanceRecord
                {
                    OrganizationId = OrgId,
                    EmployeeId = correction.EmployeeId,
                    WorkDate = correction.WorkDate,
                    Source = AttendanceSource.Correction
                };
                db.AttendanceRecords.Add(record);
            }

            record.ClockInAtUtc = correction.RequestedClockInAtUtc ?? record.ClockInAtUtc;
            record.ClockOutAtUtc = correction.RequestedClockOutAtUtc ?? record.ClockOutAtUtc;
            record.BreakMinutes = correction.RequestedBreakMinutes ?? record.BreakMinutes;
            record.Source = AttendanceSource.Correction;

            var day = await schedules.ResolveForEmployeeAsync(OrgId, correction.EmployeeId, correction.WorkDate, ct);
            var result = AttendanceCalculator.Evaluate(day, record.ClockInAtUtc, record.ClockOutAtUtc, record.BreakMinutes,
                settings.LateGraceMinutes, settings.OvertimeEnabled, settings.OvertimeMultiplier, tz);
            record.WorkedMinutes = result.WorkedMinutes;
            record.LateMinutes = result.LateMinutes;
            record.EarlyDepartureMinutes = result.EarlyDepartureMinutes;
            record.OvertimeMinutes = result.OvertimeMinutes;
            record.Status = result.Status;
        }

        await db.SaveChangesAsync(ct);
        return await ToCorrectionDtoAsync(correction, ct);
    }

    public async Task<PagedResult<CorrectionRequestDto>> ListCorrectionsAsync(PagedRequest page, ReviewStatus? status, CancellationToken ct = default)
    {
        var query = db.AttendanceCorrectionRequests.AsNoTracking();
        if (!user.Has(Permissions.AttendanceViewAll))
        {
            if (user.EmployeeId is not { } selfId)
                throw new ForbiddenException("Your account is not linked to an employee record.");
            query = query.Where(c => c.EmployeeId == selfId);
        }
        if (status is { } s) query = query.Where(c => c.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query.Skip(page.Skip).Take(Math.Clamp(page.PageSize, 1, 100))
            .OrderByDescending(c => c.CreatedAtUtc).ToListAsync(ct);

        var dtos = new List<CorrectionRequestDto>(items.Count);
        foreach (var c in items) dtos.Add(await ToCorrectionDtoAsync(c, ct));
        return new PagedResult<CorrectionRequestDto>(dtos, page.Page, page.PageSize, total);
    }

    // ─── Monthly summary ─────────────────────────────────────────────────────────

    public async Task<MonthlyAttendanceSummaryDto> MonthlySummaryAsync(Guid? employeeId, int year, int month, CancellationToken ct = default)
    {
        var targetEmployeeId = employeeId;
        if (!user.Has(Permissions.AttendanceViewAll))
            targetEmployeeId = user.EmployeeId
                ?? throw new ForbiddenException("Your account is not linked to an employee record.");

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == targetEmployeeId, ct)
            ?? throw new NotFoundException("Employee");
        var (tz, settings) = await schedules.GetOrgContextAsync(OrgId, ct);

        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, tz));
        var effectiveTo = to > today && today >= from ? today : to;

        var records = await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.EmployeeId == employee.Id && a.WorkDate >= from && a.WorkDate <= to)
            .OrderBy(a => a.WorkDate).ToListAsync(ct);

        var holidays = await schedules.GetHolidayDatesAsync(OrgId, year, ct);
        var workingDays = 0;
        for (var d = from; d <= effectiveTo; d = d.AddDays(1))
        {
            if (holidays.Contains(d)) continue;
            var day = await schedules.ResolveDayAsync(OrgId, employee.ScheduleId, d, ct);
            if (day.IsWorkingDay) workingDays++;
        }

        return new MonthlyAttendanceSummaryDto
        {
            EmployeeId = employee.Id,
            EmployeeName = $"{employee.FirstName} {employee.LastName}",
            Year = year,
            Month = month,
            WorkingDays = workingDays,
            DaysPresent = records.Count(r => r.Status is AttendanceStatus.Present or AttendanceStatus.Late or AttendanceStatus.Remote),
            DaysLate = records.Count(r => r.LateMinutes > 0),
            DaysAbsent = records.Count(r => r.Status == AttendanceStatus.Absent),
            DaysOnLeave = records.Count(r => r.Status == AttendanceStatus.OnLeave),
            DaysHoliday = records.Count(r => r.Status == AttendanceStatus.Holiday),
            TotalWorkedMinutes = records.Sum(r => r.WorkedMinutes),
            TotalOvertimeMinutes = records.Sum(r => r.OvertimeMinutes),
            TotalLateMinutes = records.Sum(r => r.LateMinutes),
            Records = records.Count <= 31 ? records.Select(r => ToDto(r, employee)).ToList() : []
        };
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private Guid RequireEmployeeId() => user.EmployeeId
        ?? throw new ForbiddenException("Your account is not linked to an employee record.");

    private async Task<AttendanceDto> ToDtoAsync(AttendanceRecord r, CancellationToken ct)
    {
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.EmployeeId, ct)
            ?? new Domain.Employees.Employee { EmployeeNumber = "?", FirstName = "?", LastName = "" };
        return ToDto(r, e);
    }

    private static AttendanceDto ToDto(AttendanceRecord r, Domain.Employees.Employee e) => new()
    {
        Id = r.Id,
        EmployeeId = r.EmployeeId,
        EmployeeName = $"{e.FirstName} {e.LastName}",
        EmployeeNumber = e.EmployeeNumber,
        WorkDate = r.WorkDate,
        ClockInUtc = r.ClockInAtUtc,
        ClockOutUtc = r.ClockOutAtUtc,
        BreakMinutes = r.BreakMinutes,
        WorkedMinutes = r.WorkedMinutes,
        LateMinutes = r.LateMinutes,
        EarlyDepartureMinutes = r.EarlyDepartureMinutes,
        OvertimeMinutes = r.OvertimeMinutes,
        Status = r.Status.ToString(),
        Source = r.Source.ToString(),
        Note = r.Note,
        IsLocked = r.IsLocked
    };

    private async Task<CorrectionRequestDto> ToCorrectionDtoAsync(AttendanceCorrectionRequest c, CancellationToken ct)
    {
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == c.EmployeeId, ct)
            ?? new Domain.Employees.Employee { FirstName = "?", LastName = "" };
        return new CorrectionRequestDto
        {
            Id = c.Id,
            EmployeeId = c.EmployeeId,
            EmployeeName = $"{e.FirstName} {e.LastName}",
            WorkDate = c.WorkDate,
            RequestedClockInUtc = c.RequestedClockInAtUtc,
            RequestedClockOutUtc = c.RequestedClockOutAtUtc,
            RequestedBreakMinutes = c.RequestedBreakMinutes,
            Reason = c.Reason,
            Status = c.Status.ToString(),
            ReviewedBy = c.ReviewedBy,
            ReviewedAtUtc = c.ReviewedAtUtc,
            ReviewNote = c.ReviewNote
        };
    }
}
