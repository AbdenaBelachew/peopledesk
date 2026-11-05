using Microsoft.EntityFrameworkCore;
using FluentValidation;
using PeopleDesk.Application.Attendance;
using PeopleDesk.Application.Common;
using PeopleDesk.Application.Notifications;
using PeopleDesk.Domain.Attendance;
using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.Leave;

namespace PeopleDesk.Application.Leave;

public class LeaveService(
    IAppDbContext db,
    ICurrentUser user,
    IDateTime clock,
    ScheduleProvider schedules,
    NotificationService notifications)
{
    private Guid OrgId => user.OrganizationId
        ?? throw new ForbiddenException("No organization context for this request.");

    // ─── Leave types & policies ──────────────────────────────────────────────────

    public async Task<List<LeaveTypeDto>> ListTypesAsync(CancellationToken ct = default)
    {
        var types = await db.LeaveTypes.AsNoTracking().Where(t => t.OrganizationId == OrgId).OrderBy(t => t.Name).ToListAsync(ct);
        var policies = await db.LeavePolicies.AsNoTracking().Where(p => p.OrganizationId == OrgId).ToDictionaryAsync(p => p.LeaveTypeId, ct);

        return types.Select(t =>
        {
            policies.TryGetValue(t.Id, out var p);
            return ToTypeDto(t, p);
        }).ToList();
    }

    public async Task<LeaveTypeDto> SaveTypeAsync(SaveLeaveTypeRequest request, CancellationToken ct = default)
    {
        user.Ensure(Permissions.LeaveManage);
        await new SaveLeaveTypeRequestValidator().ValidateAndThrowAsync(request, ct);

        var code = request.Code.Trim().ToUpperInvariant();
        var duplicate = await db.LeaveTypes.IgnoreQueryFilters()
            .AnyAsync(t => t.OrganizationId == OrgId && t.Code == code && t.Id != request.Id && !t.IsDeleted, ct);
        if (duplicate) throw new ConflictException($"Leave type code '{code}' already exists.");

        LeaveType type;
        LeavePolicy policy;
        if (request.Id is { } id)
        {
            type = await db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("Leave type", id);
            policy = await db.LeavePolicies.FirstOrDefaultAsync(p => p.LeaveTypeId == id, ct)
                ?? throw new NotFoundException("Leave policy", id);
        }
        else
        {
            type = new LeaveType { OrganizationId = OrgId, Code = code };
            policy = new LeavePolicy { OrganizationId = OrgId, LeaveTypeId = type.Id };
            db.LeaveTypes.Add(type);
            db.LeavePolicies.Add(policy);
        }

        type.Name = request.Name.Trim();
        type.Code = code;
        type.ColorHex = request.ColorHex;
        type.IsPaid = request.IsPaid;
        type.RequiresApproval = request.RequiresApproval;
        type.Description = request.Description;

        policy.AnnualEntitlementDays = request.AnnualEntitlementDays;
        policy.AccrualFrequency = request.AccrualFrequency;
        policy.CarryOverMaxDays = request.CarryOverMaxDays;
        policy.MinNoticeDays = request.MinNoticeDays;
        policy.MaxConsecutiveDays = request.MaxConsecutiveDays;
        policy.AllowHalfDays = request.AllowHalfDays;

        await db.SaveChangesAsync(ct);
        return ToTypeDto(type, policy);
    }

    // ─── Balances ────────────────────────────────────────────────────────────────

    public async Task<List<LeaveBalanceDto>> BalancesAsync(Guid employeeId, int? year, CancellationToken ct = default)
    {
        if (employeeId != user.EmployeeId)
            user.Ensure(Permissions.LeaveViewAll);

        var targetYear = year ?? clock.TodayUtc.Year;
        var types = await db.LeaveTypes.AsNoTracking().Where(t => t.OrganizationId == OrgId).ToListAsync(ct);
        var policies = await db.LeavePolicies.AsNoTracking().Where(p => p.OrganizationId == OrgId).ToDictionaryAsync(p => p.LeaveTypeId, ct);
        var balances = await db.LeaveBalances.AsNoTracking()
            .Where(b => b.EmployeeId == employeeId && b.Year == targetYear)
            .ToDictionaryAsync(b => b.LeaveTypeId, ct);

        var result = new List<LeaveBalanceDto>();
        foreach (var t in types)
        {
            policies.TryGetValue(t.Id, out var policy);
            balances.TryGetValue(t.Id, out var balance);

            result.Add(new LeaveBalanceDto
            {
                LeaveTypeId = t.Id,
                TypeName = t.Name,
                TypeCode = t.Code,
                ColorHex = t.ColorHex,
                Year = targetYear,
                EntitledDays = balance?.EntitledDays ?? policy?.AnnualEntitlementDays ?? 0,
                CarriedOverDays = balance?.CarriedOverDays ?? 0,
                AccruedDays = balance?.AccruedDays ?? 0,
                UsedDays = balance?.UsedDays ?? 0,
                PendingDays = balance?.PendingDays ?? 0,
                AdjustedDays = balance?.AdjustedDays ?? 0
            });
        }
        return result;
    }

    public async Task<LeaveBalanceDto> AdjustBalanceAsync(Guid employeeId, Guid leaveTypeId, decimal days, string? note, CancellationToken ct = default)
    {
        user.Ensure(Permissions.LeaveManage);
        if (days == 0) throw new BadRequestException("Adjustment must be non-zero.");

        var year = clock.TodayUtc.Year;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var balance = await EnsureBalanceAsync(employeeId, leaveTypeId, year, ct);
        balance.AdjustedDays += days;

        await WriteTransactionAsync(employeeId, leaveTypeId, year, LeaveTransactionType.Adjustment,
            days, balance.Available, null, note, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return (await BalancesAsync(employeeId, year, ct)).First(b => b.LeaveTypeId == leaveTypeId);
    }

    public async Task<List<LeaveTransactionDto>> TransactionsAsync(Guid employeeId, Guid? leaveTypeId, int? year, CancellationToken ct = default)
    {
        if (employeeId != user.EmployeeId)
            user.Ensure(Permissions.LeaveViewAll);

        var query = db.LeaveTransactions.AsNoTracking().Where(t => t.EmployeeId == employeeId);
        if (leaveTypeId is { } ltid) query = query.Where(t => t.LeaveTypeId == ltid);
        if (year is { } y) query = query.Where(t => t.Year == y);

        var rows = await query.OrderByDescending(t => t.CreatedAtUtc).Take(200).ToListAsync(ct);
        return rows.Select(t => new LeaveTransactionDto
        {
            Id = t.Id, EmployeeId = t.EmployeeId, LeaveTypeId = t.LeaveTypeId, Year = t.Year,
            Type = t.Type.ToString(), Days = t.Days, BalanceAfter = t.BalanceAfter,
            LeaveRequestId = t.LeaveRequestId, Description = t.Description, Actor = t.Actor,
            CreatedAtUtc = t.CreatedAtUtc
        }).ToList();
    }

    // ─── Requests ────────────────────────────────────────────────────────────────

    public async Task<LeaveRequestDto> CreateAsync(CreateLeaveRequest request, CancellationToken ct = default)
    {
        await new CreateLeaveRequestValidator().ValidateAndThrowAsync(request, ct);

        var employeeId = request.EmployeeId == Guid.Empty ? user.EmployeeId : request.EmployeeId;
        if (employeeId is null)
            throw new BadRequestException("EmployeeId is required (or sign in with a linked employee account).");
        if (employeeId != user.EmployeeId && !user.Has(Permissions.EmployeesCreate) && !user.Has(Permissions.LeaveManage))
            throw new ForbiddenException("You can only create leave requests for yourself.");

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new NotFoundException("Employee", employeeId.Value);
        var type = await db.LeaveTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.LeaveTypeId, ct)
            ?? throw new NotFoundException("Leave type", request.LeaveTypeId);
        var policy = await db.LeavePolicies.AsNoTracking().FirstOrDefaultAsync(p => p.LeaveTypeId == type.Id, ct)
            ?? throw new NotFoundException("Leave policy");

        // Policy checks.
        if (request.IsHalfDay && !policy.AllowHalfDays)
            throw new BusinessRuleException($"Leave type '{type.Name}' does not allow half days.");
        if (request.StartDate < clock.TodayUtc.AddDays(policy.MinNoticeDays) && policy.MinNoticeDays > 0)
            throw new BusinessRuleException($"'{type.Name}' requires at least {policy.MinNoticeDays} day(s) notice.");
        if (request.EndDate > request.StartDate.AddDays(policy.MaxConsecutiveDays))
            throw new BusinessRuleException($"Maximum consecutive days for '{type.Name}' is {policy.MaxConsecutiveDays}.");

        // Working-day-aware day count.
        var holidays = await schedules.GetHolidayDatesAsync(OrgId, clock.TodayUtc.Year, ct);
        var days = 0m;
        if (request.IsHalfDay)
        {
            days = 0.5m;
        }
        else
        {
            for (var d = request.StartDate; d <= request.EndDate; d = d.AddDays(1))
            {
                if (holidays.Contains(d)) continue;
                var day = await schedules.ResolveForEmployeeAsync(OrgId, employeeId.Value, d, ct);
                if (day.IsWorkingDay) days += 1m;
            }
        }
        if (days <= 0)
            throw new BusinessRuleException("The selected range contains no working days.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Overlap guard (pending or approved) — enforced inside the transaction.
        var overlaps = await db.LeaveRequests.AnyAsync(r =>
            r.EmployeeId == employeeId &&
            r.StartDate <= request.EndDate && r.EndDate >= request.StartDate &&
            (r.Status == ReviewStatus.Pending || r.Status == ReviewStatus.Approved), ct);
        if (overlaps)
            throw new ConflictException("An overlapping leave request already exists for this period.");

        var balance = await EnsureBalanceAsync(employeeId.Value, type.Id, clock.TodayUtc.Year, ct);
        if (balance.Available < days)
            throw new BusinessRuleException(
                $"Insufficient balance: requested {days:0.##} day(s), available {balance.Available:0.##} for '{type.Name}'.");

        var leaveRequest = new LeaveRequest
        {
            OrganizationId = OrgId,
            EmployeeId = employeeId.Value,
            LeaveTypeId = type.Id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Days = days,
            IsHalfDay = request.IsHalfDay,
            Reason = request.Reason,
            Status = ReviewStatus.Pending
        };
        db.LeaveRequests.Add(leaveRequest);

        // Reserve days into pending.
        balance.PendingDays += days;
        await WriteTransactionAsync(employeeId.Value, type.Id, clock.TodayUtc.Year,
            LeaveTransactionType.Application, -days, balance.Available, leaveRequest.Id, "Leave requested", ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await notifications.NotifyApproversAsync(
            NotificationType.LeaveRequestSubmitted,
            "New leave request",
            $"{employee.FirstName} {employee.LastName} requested {days:0.##} day(s) of {type.Name} ({request.StartDate:yyyy-MM-dd} → {request.EndDate:yyyy-MM-dd}).",
            $"/leave/approvals", ct);

        return await ToRequestDtoAsync(leaveRequest, ct);
    }

    public async Task<LeaveRequestDto> ReviewAsync(Guid requestId, ReviewLeaveRequest request, CancellationToken ct = default)
    {
        if (request.IsHrOverride)
            user.Ensure(Permissions.LeaveManage);
        else
            user.Ensure(Permissions.LeaveApprove);

        var leaveRequest = await db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new NotFoundException("Leave request", requestId);
        if (leaveRequest.Status != ReviewStatus.Pending)
            throw new BusinessRuleException($"This request is already {leaveRequest.Status}.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var balance = await db.LeaveBalances
                .FirstOrDefaultAsync(b => b.EmployeeId == leaveRequest.EmployeeId
                    && b.LeaveTypeId == leaveRequest.LeaveTypeId && b.Year == leaveRequest.StartDate.Year, ct)
                ?? throw new NotFoundException("Leave balance");

            if (request.Approve)
            {
                leaveRequest.Status = ReviewStatus.Approved;
                balance.PendingDays -= leaveRequest.Days;
                balance.UsedDays += leaveRequest.Days;

                await WriteTransactionAsync(leaveRequest.EmployeeId, leaveRequest.LeaveTypeId, leaveRequest.StartDate.Year,
                    LeaveTransactionType.Approval, 0, balance.Available, leaveRequest.Id, "Approved", ct);

                // Mark approved leave days on the attendance calendar.
                await MarkAttendanceOnLeaveAsync(leaveRequest, ct);
            }
            else
            {
                leaveRequest.Status = ReviewStatus.Rejected;
                balance.PendingDays -= leaveRequest.Days;

                await WriteTransactionAsync(leaveRequest.EmployeeId, leaveRequest.LeaveTypeId, leaveRequest.StartDate.Year,
                    LeaveTransactionType.Rejection, leaveRequest.Days, balance.Available, leaveRequest.Id,
                    "Rejected — pending days released", ct);
            }

            leaveRequest.ReviewedBy = user.Email;
            leaveRequest.ReviewedAtUtc = clock.UtcNow;
            leaveRequest.ReviewNote = request.ReviewNote;
            if (request.IsHrOverride)
                leaveRequest.HrOverrideBy = user.Email;

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        var type = await db.LeaveTypes.AsNoTracking().FirstAsync(t => t.Id == leaveRequest.LeaveTypeId, ct);
        await notifications.NotifyEmployeeAsync(leaveRequest.EmployeeId,
            NotificationType.LeaveRequestDecision,
            request.Approve ? "Leave approved" : "Leave rejected",
            $"Your {type.Name} request ({leaveRequest.StartDate:yyyy-MM-dd} → {leaveRequest.EndDate:yyyy-MM-dd}) was {leaveRequest.Status.ToString().ToLowerInvariant()}." +
            (string.IsNullOrWhiteSpace(request.ReviewNote) ? "" : $" Note: {request.ReviewNote}"),
            "/leave", ct);

        return await ToRequestDtoAsync(leaveRequest, ct);
    }

    public async Task<LeaveRequestDto> CancelAsync(Guid requestId, CancellationToken ct = default)
    {
        var leaveRequest = await db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new NotFoundException("Leave request", requestId);

        var isOwner = leaveRequest.EmployeeId == user.EmployeeId;
        if (!isOwner && !user.Has(Permissions.LeaveManage))
            throw new ForbiddenException("Only the requester or HR can cancel a leave request.");

        if (leaveRequest.Status is not (ReviewStatus.Pending or ReviewStatus.Approved))
            throw new BusinessRuleException($"A {leaveRequest.Status} request cannot be cancelled.");
        if (leaveRequest.Status == ReviewStatus.Approved && leaveRequest.StartDate <= clock.TodayUtc)
            throw new BusinessRuleException("Approved leave that has already started cannot be cancelled. Contact HR.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var balance = await db.LeaveBalances
                .FirstOrDefaultAsync(b => b.EmployeeId == leaveRequest.EmployeeId
                    && b.LeaveTypeId == leaveRequest.LeaveTypeId && b.Year == leaveRequest.StartDate.Year, ct)
                ?? throw new NotFoundException("Leave balance");

            if (leaveRequest.Status == ReviewStatus.Pending)
                balance.PendingDays -= leaveRequest.Days;
            else
            {
                balance.UsedDays -= leaveRequest.Days;
                await RestoreAttendanceAsync(leaveRequest, ct);
            }

            leaveRequest.Status = ReviewStatus.Cancelled;
            leaveRequest.CancelledAtUtc = clock.UtcNow;

            await WriteTransactionAsync(leaveRequest.EmployeeId, leaveRequest.LeaveTypeId, leaveRequest.StartDate.Year,
                LeaveTransactionType.Cancellation, leaveRequest.Days, balance.Available, leaveRequest.Id,
                leaveRequest.Status == ReviewStatus.Pending ? "Pending request cancelled" : "Approved leave cancelled", ct);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return await ToRequestDtoAsync(leaveRequest, ct);
    }

    public async Task<PagedResult<LeaveRequestDto>> ListAsync(PagedRequest page, Guid? employeeId, ReviewStatus? status,
        Guid? leaveTypeId, CancellationToken ct = default)
    {
        Guid? effectiveEmployeeId = employeeId;
        if (!user.Has(Permissions.LeaveViewAll))
            effectiveEmployeeId = user.EmployeeId
                ?? throw new ForbiddenException("Your account is not linked to an employee record.");

        var query = db.LeaveRequests.AsNoTracking();
        if (effectiveEmployeeId is { } emp) query = query.Where(r => r.EmployeeId == emp);
        if (status is { } s) query = query.Where(r => r.Status == s);
        if (leaveTypeId is { } lt) query = query.Where(r => r.LeaveTypeId == lt);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip(page.Skip).Take(Math.Clamp(page.PageSize, 1, 100))
            .ToListAsync(ct);

        var dtos = new List<LeaveRequestDto>(rows.Count);
        foreach (var r in rows) dtos.Add(await ToRequestDtoAsync(r, ct));
        return new PagedResult<LeaveRequestDto>(dtos, page.Page, page.PageSize, total);
    }

    public async Task<List<LeaveCalendarEntryDto>> CalendarAsync(DateOnly from, DateOnly to, Guid? departmentId, CancellationToken ct = default)
    {
        user.Ensure(Permissions.LeaveViewAll);

        var query = db.LeaveRequests.AsNoTracking()
            .Where(r => r.StartDate <= to && r.EndDate >= from && (r.Status == ReviewStatus.Approved || r.Status == ReviewStatus.Pending));
        if (departmentId is { } dep)
        {
            var deptEmployees = db.Employees.Where(e => e.DepartmentId == dep).Select(e => e.Id);
            query = query.Where(r => deptEmployees.Contains(r.EmployeeId));
        }

        var rows = await query.OrderBy(r => r.StartDate).ToListAsync(ct);
        var employeeIds = rows.Select(r => r.EmployeeId).Distinct().ToList();
        var employees = await db.Employees.AsNoTracking()
            .Where(e => employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => $"{e.FirstName} {e.LastName}", ct);
        var types = await db.LeaveTypes.AsNoTracking().Where(t => t.OrganizationId == OrgId)
            .ToDictionaryAsync(t => t.Id, ct);

        return rows.Select(r => new LeaveCalendarEntryDto
        {
            RequestId = r.Id,
            EmployeeId = r.EmployeeId,
            EmployeeName = employees.GetValueOrDefault(r.EmployeeId, "?"),
            LeaveTypeId = r.LeaveTypeId,
            TypeName = types.GetValueOrDefault(r.LeaveTypeId)?.Name ?? "?",
            ColorHex = types.GetValueOrDefault(r.LeaveTypeId)?.ColorHex,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            Status = r.Status.ToString()
        }).ToList();
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    /// <summary>Creates the year's balance row from the policy when missing (grant ledger entry included).</summary>
    private async Task<LeaveBalance> EnsureBalanceAsync(Guid employeeId, Guid leaveTypeId, int year, CancellationToken ct)
    {
        var balance = await db.LeaveBalances
            .FirstOrDefaultAsync(b => b.EmployeeId == employeeId && b.LeaveTypeId == leaveTypeId && b.Year == year, ct);
        if (balance is not null) return balance;

        var policy = await db.LeavePolicies.AsNoTracking().FirstOrDefaultAsync(p => p.LeaveTypeId == leaveTypeId, ct)
            ?? throw new NotFoundException("Leave policy");
        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new NotFoundException("Employee", employeeId);

        // Probation gating: no accrual before the policy's probation window elapses.
        var probationEnd = employee.HireDate.AddMonths(policy.ProbationMonthsBeforeEligible);
        var entitled = clock.TodayUtc.ToDateTime(TimeOnly.MinValue) >= probationEnd.ToDateTime(TimeOnly.MinValue)
            ? policy.AnnualEntitlementDays
            : 0m;

        balance = new LeaveBalance
        {
            OrganizationId = OrgId,
            EmployeeId = employeeId,
            LeaveTypeId = leaveTypeId,
            Year = year,
            EntitledDays = entitled
        };
        db.LeaveBalances.Add(balance);
        await db.SaveChangesAsync(ct);

        await WriteTransactionAsync(employeeId, leaveTypeId, year, LeaveTransactionType.Grant,
            entitled, balance.Available, null, $"Annual grant {year}", ct);
        await db.SaveChangesAsync(ct);

        return balance;
    }

    private async Task WriteTransactionAsync(Guid employeeId, Guid leaveTypeId, int year,
        LeaveTransactionType type, decimal days, decimal balanceAfter, Guid? requestId, string? description, CancellationToken ct)
    {
        db.LeaveTransactions.Add(new LeaveTransaction
        {
            OrganizationId = OrgId,
            EmployeeId = employeeId,
            LeaveTypeId = leaveTypeId,
            Year = year,
            Type = type,
            Days = days,
            BalanceAfter = balanceAfter,
            LeaveRequestId = requestId,
            Description = description,
            Actor = user.Email
        });
        await Task.CompletedTask;
    }

    private async Task MarkAttendanceOnLeaveAsync(LeaveRequest leaveRequest, CancellationToken ct)
    {
        var type = await db.LeaveTypes.AsNoTracking().FirstAsync(t => t.Id == leaveRequest.LeaveTypeId, ct);
        if (!type.IsPaid && type.Code == "UNPAID") { /* unpaid leave still marks calendar */ }

        var holidays = await schedules.GetHolidayDatesAsync(OrgId, leaveRequest.StartDate.Year, ct);
        var (tz, _) = await schedules.GetOrgContextAsync(OrgId, ct);

        for (var d = leaveRequest.StartDate; d <= leaveRequest.EndDate; d = d.AddDays(1))
        {
            if (holidays.Contains(d)) continue;
            var day = await schedules.ResolveForEmployeeAsync(OrgId, leaveRequest.EmployeeId, d, ct);
            if (!day.IsWorkingDay) continue;

            var localMidnightUtc = TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(TimeOnly.MinValue), tz);
            var record = await db.AttendanceRecords.FirstOrDefaultAsync(a =>
                a.EmployeeId == leaveRequest.EmployeeId && a.WorkDate == d, ct);

            if (record is null)
            {
                record = new AttendanceRecord
                {
                    OrganizationId = OrgId,
                    EmployeeId = leaveRequest.EmployeeId,
                    WorkDate = d,
                    Status = AttendanceStatus.OnLeave,
                    Source = AttendanceSource.ManualEntry,
                    Note = $"Leave: {type.Name}",
                    ManualEntryBy = "system"
                };
                db.AttendanceRecords.Add(record);
            }
            else if (record.ClockInAtUtc is null)
            {
                record.Status = AttendanceStatus.OnLeave;
                record.Note = $"Leave: {type.Name}";
            }
            _ = localMidnightUtc;
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task RestoreAttendanceAsync(LeaveRequest leaveRequest, CancellationToken ct)
    {
        var records = await db.AttendanceRecords
            .Where(a => a.EmployeeId == leaveRequest.EmployeeId && a.WorkDate >= leaveRequest.StartDate
                && a.WorkDate <= leaveRequest.EndDate && a.Status == AttendanceStatus.OnLeave
                && a.ClockInAtUtc == null && !a.IsLocked)
            .ToListAsync(ct);
        foreach (var r in records)
        {
            r.Status = AttendanceStatus.Absent;
            r.Note = "Leave cancelled";
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<LeaveRequestDto> ToRequestDtoAsync(LeaveRequest r, CancellationToken ct)
    {
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.EmployeeId, ct)
            ?? new Domain.Employees.Employee { FirstName = "?", LastName = "", EmployeeNumber = "?" };
        var t = await db.LeaveTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.LeaveTypeId, ct)
            ?? new LeaveType { Name = "?" };
        return new LeaveRequestDto
        {
            Id = r.Id,
            EmployeeId = r.EmployeeId,
            EmployeeName = $"{e.FirstName} {e.LastName}",
            EmployeeNumber = e.EmployeeNumber,
            LeaveTypeId = r.LeaveTypeId,
            TypeName = t.Name,
            ColorHex = t.ColorHex,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            Days = r.Days,
            IsPaid = t.IsPaid,
            Reason = r.Reason,
            Status = r.Status.ToString(),
            ReviewedBy = r.ReviewedBy,
            ReviewedAtUtc = r.ReviewedAtUtc,
            ReviewNote = r.ReviewNote,
            HrOverride = r.HrOverrideBy is not null,
            CancelledAtUtc = r.CancelledAtUtc
        };
    }

    private static LeaveTypeDto ToTypeDto(LeaveType t, LeavePolicy? p) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Code = t.Code,
        ColorHex = t.ColorHex,
        IsPaid = t.IsPaid,
        RequiresApproval = t.RequiresApproval,
        Description = t.Description,
        AnnualEntitlementDays = p?.AnnualEntitlementDays ?? 0,
        AccrualFrequency = (p?.AccrualFrequency ?? LeaveAccrualFrequency.YearlyUpfront).ToString(),
        CarryOverMaxDays = p?.CarryOverMaxDays ?? 0,
        MinNoticeDays = p?.MinNoticeDays ?? 0,
        MaxConsecutiveDays = p?.MaxConsecutiveDays ?? 0,
        AllowHalfDays = p?.AllowHalfDays ?? false
    };
}
