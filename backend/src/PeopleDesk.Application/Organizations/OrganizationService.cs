using Microsoft.EntityFrameworkCore;
using FluentValidation;
using PeopleDesk.Application.Common;
using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.Organizations;

namespace PeopleDesk.Application.Organizations;

public class OrganizationService(IAppDbContext db, ICurrentUser user, IDateTime clock)
{
    private Guid OrgId => user.OrganizationId
        ?? throw new ForbiddenException("No organization context for this request.");

    // ─── Profile & settings ──────────────────────────────────────────────────────

    public async Task<OrganizationDto> GetCurrentAsync(CancellationToken ct = default)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == OrgId, ct)
            ?? throw new NotFoundException("Organization");
        return ToDto(org);
    }

    public async Task<OrganizationDto> UpdateAsync(UpdateOrganizationRequest request, CancellationToken ct = default)
    {
        await new UpdateOrganizationRequestValidator().ValidateAndThrowAsync(request, ct);
        user.Ensure(Permissions.OrgUpdate);

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == OrgId, ct)
            ?? throw new NotFoundException("Organization");

        org.Name = request.Name.Trim();
        org.Email = request.Email;
        org.Phone = request.Phone;
        org.LogoUrl = request.LogoUrl;
        org.BrandColor = request.BrandColor;
        org.AddressLine = request.AddressLine;
        org.City = request.City;
        org.Country = request.Country;
        org.TimeZone = request.TimeZone;
        org.Currency = request.Currency.ToUpperInvariant();

        await db.SaveChangesAsync(ct);
        return ToDto(org);
    }

    public async Task<SettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var s = await db.OrganizationSettings.FirstOrDefaultAsync(x => x.OrganizationId == OrgId, ct)
            ?? throw new NotFoundException("Organization settings");
        return ToSettingsDto(s);
    }

    public async Task<SettingsDto> UpdateSettingsAsync(UpdateSettingsRequest request, CancellationToken ct = default)
    {
        user.Ensure(Permissions.SettingsManage);
        if (request.OvertimeMultiplier is < 1m or > 4m)
            throw new BadRequestException("Overtime multiplier must be between 1 and 4.");

        var s = await db.OrganizationSettings.FirstOrDefaultAsync(x => x.OrganizationId == OrgId, ct)
            ?? throw new NotFoundException("Organization settings");

        s.FiscalYearStartMonth = Math.Clamp(request.FiscalYearStartMonth, 1, 12);
        s.LateGraceMinutes = Math.Clamp(request.LateGraceMinutes, 0, 120);
        s.OvertimeEnabled = request.OvertimeEnabled;
        s.OvertimeMultiplier = request.OvertimeMultiplier;
        s.RequireApprovalForSensitiveProfileChanges = request.RequireApprovalForSensitiveProfileChanges;
        s.AutoApproveLeaveWithoutManager = request.AutoApproveLeaveWithoutManager;

        await db.SaveChangesAsync(ct);
        return ToSettingsDto(s);
    }

    // ─── Structure: departments / branches / positions / employment types ────────

    public Task<List<StructureItemDto>> ListDepartmentsAsync(CancellationToken ct = default) =>
        ListStructureAsync(db.Departments, d => d.Name, ct);

    public Task<List<StructureItemDto>> ListBranchesAsync(CancellationToken ct = default) =>
        ListStructureAsync(db.Branches, b => b.Name, ct);

    public Task<List<StructureItemDto>> ListPositionsAsync(CancellationToken ct = default) =>
        ListStructureAsync(db.Positions, p => p.Title, ct);

    public Task<List<StructureItemDto>> ListEmploymentTypesAsync(CancellationToken ct = default) =>
        ListStructureAsync(db.EmploymentTypes, t => t.Name, ct);

    private async Task<List<StructureItemDto>> ListStructureAsync<TEntity>(
        IQueryable<TEntity> set, Func<TEntity, string> nameOf, CancellationToken ct)
        where TEntity : Entity, IOrganizationScoped, ISoftDeletable
    {
        user.Ensure(Permissions.StructureView);
        var items = await set.IgnoreQueryFilters().Where(x => !x.IsDeleted && x.OrganizationId == OrgId).ToListAsync(ct);
        return items.Select(x => new StructureItemDto
        {
            Id = x.Id,
            Name = nameOf(x),
            Code = x switch
            {
                Department d => d.Code,
                Branch b => b.Code,
                Position p => p.Code,
                EmploymentType e => e.Name,
                _ => null
            },
            Description = x switch
            {
                Department d2 => d2.Description,
                Branch b2 => b2.Address,
                Position p2 => p2.Level,
                EmploymentType e2 => e2.Description,
                _ => null
            },
            ManagerEmployeeId = x is Department d3 ? d3.ManagerEmployeeId : null
        }).OrderBy(x => x.Name).ToList();
    }

    public async Task<StructureItemDto> CreateDepartmentAsync(StructureItemRequest request, CancellationToken ct = default) =>
        await CreateStructureAsync(db.Departments, request, (orgId, r) =>
            new Department { OrganizationId = orgId, Name = r.Name.Trim(), Code = r.Code, Description = r.Description, ManagerEmployeeId = r.ManagerEmployeeId },
            "Department",
            name => db.Departments.IgnoreQueryFilters().AnyAsync(d => !d.IsDeleted && d.OrganizationId == OrgId && d.Name == name, ct), ct);

    public async Task<StructureItemDto> CreateBranchAsync(StructureItemRequest request, CancellationToken ct = default) =>
        await CreateStructureAsync(db.Branches, request, (orgId, r) =>
            new Branch { OrganizationId = orgId, Name = r.Name.Trim(), Code = r.Code, Address = r.Description },
            "Branch",
            name => db.Branches.IgnoreQueryFilters().AnyAsync(b => !b.IsDeleted && b.OrganizationId == OrgId && b.Name == name, ct), ct);

    public async Task<StructureItemDto> CreatePositionAsync(StructureItemRequest request, CancellationToken ct = default) =>
        await CreateStructureAsync(db.Positions, request, (orgId, r) =>
            new Position { OrganizationId = orgId, Title = r.Name.Trim(), Code = r.Code, Level = r.Level },
            "Position",
            name => db.Positions.IgnoreQueryFilters().AnyAsync(p => !p.IsDeleted && p.OrganizationId == OrgId && p.Title == name, ct), ct);

    public async Task<StructureItemDto> CreateEmploymentTypeAsync(StructureItemRequest request, CancellationToken ct = default) =>
        await CreateStructureAsync(db.EmploymentTypes, request, (orgId, r) =>
            new EmploymentType { OrganizationId = orgId, Name = r.Name.Trim(), Description = r.Description },
            "Employment type",
            name => db.EmploymentTypes.IgnoreQueryFilters().AnyAsync(t => !t.IsDeleted && t.OrganizationId == OrgId && t.Name == name, ct), ct);

    private async Task<StructureItemDto> CreateStructureAsync<TEntity>(
        DbSet<TEntity> set, StructureItemRequest request, Func<Guid, StructureItemRequest, TEntity> factory,
        string what, Func<string, Task<bool>> existsAsync, CancellationToken ct)
        where TEntity : Entity, IOrganizationScoped, ISoftDeletable
    {
        user.Ensure(Permissions.StructureManage);
        await new StructureItemRequestValidator().ValidateAndThrowAsync(request, ct);

        var name = request.Name.Trim();
        if (await existsAsync(name))
            throw new ConflictException($"{what} '{name}' already exists.");

        var entity = factory(OrgId, request);
        set.Add(entity);
        await db.SaveChangesAsync(ct);
        return new StructureItemDto { Id = entity.Id, Name = name, Code = request.Code, Description = request.Description, ManagerEmployeeId = request.ManagerEmployeeId };
    }

    public async Task UpdateDepartmentAsync(Guid id, StructureItemRequest request, CancellationToken ct = default) =>
        await UpdateStructureAsync(db.Departments, id, request, d => d.Name = request.Name.Trim(), "Department", ct);

    public async Task UpdateBranchAsync(Guid id, StructureItemRequest request, CancellationToken ct = default) =>
        await UpdateStructureAsync(db.Branches, id, request, b => b.Name = request.Name.Trim(), "Branch", ct);

    public async Task UpdatePositionAsync(Guid id, StructureItemRequest request, CancellationToken ct = default) =>
        await UpdateStructureAsync(db.Positions, id, request, p => p.Title = request.Name.Trim(), "Position", ct);

    public async Task UpdateEmploymentTypeAsync(Guid id, StructureItemRequest request, CancellationToken ct = default) =>
        await UpdateStructureAsync(db.EmploymentTypes, id, request, t => t.Name = request.Name.Trim(), "Employment type", ct);

    private async Task UpdateStructureAsync<TEntity>(
        DbSet<TEntity> set, Guid id, StructureItemRequest request, Action<TEntity> mutate,
        string what, CancellationToken ct)
        where TEntity : Entity, IOrganizationScoped, ISoftDeletable
    {
        user.Ensure(Permissions.StructureManage);
        await new StructureItemRequestValidator().ValidateAndThrowAsync(request, ct);

        var entity = await set.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted && x.OrganizationId == OrgId, ct)
            ?? throw new NotFoundException(what, id);
        mutate(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteStructureAsync<TEntity>(DbSet<TEntity> set, Guid id, string what, CancellationToken ct)
        where TEntity : Entity, IOrganizationScoped, ISoftDeletable
    {
        user.Ensure(Permissions.StructureManage);
        var entity = await set.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted && x.OrganizationId == OrgId, ct)
            ?? throw new NotFoundException(what, id);
        entity.IsDeleted = true;
        entity.DeletedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public Task DeleteDepartmentAsync(Guid id, CancellationToken ct = default) => DeleteStructureAsync(db.Departments, id, "Department", ct);
    public Task DeleteBranchAsync(Guid id, CancellationToken ct = default) => DeleteStructureAsync(db.Branches, id, "Branch", ct);
    public Task DeletePositionAsync(Guid id, CancellationToken ct = default) => DeleteStructureAsync(db.Positions, id, "Position", ct);
    public Task DeleteEmploymentTypeAsync(Guid id, CancellationToken ct = default) => DeleteStructureAsync(db.EmploymentTypes, id, "Employment type", ct);

    // ─── Work schedules & holidays ───────────────────────────────────────────────

    public async Task<List<WorkScheduleDto>> ListWorkSchedulesAsync(CancellationToken ct = default)
    {
        user.Ensure(Permissions.StructureView);
        var schedules = await db.WorkSchedules.Include(s => s.Days)
            .Where(s => s.OrganizationId == OrgId).OrderBy(s => s.Name).ToListAsync(ct);
        return schedules.Select(ToScheduleDto).ToList();
    }

    public async Task<WorkScheduleDto> SaveWorkScheduleAsync(SaveWorkScheduleRequest request, CancellationToken ct = default)
    {
        user.Ensure(Permissions.StructureManage);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BadRequestException("Schedule name is required.");
        if (request.Days.Count == 0)
            throw new BadRequestException("At least one day definition is required.");

        WorkSchedule schedule;
        if (request.Id is { } id)
        {
            schedule = await db.WorkSchedules.Include(s => s.Days)
                .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == OrgId, ct)
                ?? throw new NotFoundException("Work schedule", id);
            schedule.Name = request.Name.Trim();
            schedule.Days.Clear();
        }
        else
        {
            schedule = new WorkSchedule { OrganizationId = OrgId, Name = request.Name.Trim() };
            db.WorkSchedules.Add(schedule);
        }

        foreach (var d in request.Days)
        {
            schedule.Days.Add(new WorkScheduleDay
            {
                OrganizationId = OrgId,
                ScheduleId = schedule.Id,
                DayOfWeek = d.DayOfWeek,
                IsWorkingDay = d.IsWorkingDay,
                StartMinutes = ParseTime(d.StartTime),
                EndMinutes = ParseTime(d.EndTime),
                BreakMinutes = Math.Max(0, d.BreakMinutes)
            });
        }

        if (request.IsDefault)
        {
            var others = await db.WorkSchedules.Where(s => s.OrganizationId == OrgId && s.Id != schedule.Id).ToListAsync(ct);
            foreach (var s in others) s.IsDefault = false;
        }
        schedule.IsDefault = request.IsDefault;

        await db.SaveChangesAsync(ct);
        return ToScheduleDto(schedule);
    }

    // ─── Holidays ────────────────────────────────────────────────────────────────

    public async Task<List<HolidayDto>> ListHolidaysAsync(int? year, CancellationToken ct = default)
    {
        user.Ensure(Permissions.StructureView);
        var query = db.Holidays.AsNoTracking().Where(h => h.OrganizationId == OrgId);
        if (year is { } y)
        {
            var from = new DateOnly(y, 1, 1);
            var to = new DateOnly(y, 12, 31);
            query = query.Where(h => (h.Date >= from && h.Date <= to) || h.RecurringYearly);
        }
        var holidays = await query.OrderBy(h => h.Date).ToListAsync(ct);
        return holidays.Select(ToHolidayDto).ToList();
    }

    public async Task<HolidayDto> SaveHolidayAsync(SaveHolidayRequest request, CancellationToken ct = default)
    {
        user.Ensure(Permissions.StructureManage);
        await new SaveHolidayRequestValidator().ValidateAndThrowAsync(request, ct);

        Holiday holiday;
        if (request.Id is { } id)
        {
            holiday = await db.Holidays.FirstOrDefaultAsync(h => h.Id == id && h.OrganizationId == OrgId, ct)
                ?? throw new NotFoundException("Holiday", id);
        }
        else
        {
            holiday = new Holiday { OrganizationId = OrgId };
            db.Holidays.Add(holiday);
        }

        holiday.Name = request.Name.Trim();
        holiday.Date = request.Date;
        holiday.RecurringYearly = request.RecurringYearly;
        holiday.Description = request.Description;

        await db.SaveChangesAsync(ct);
        return ToHolidayDto(holiday);
    }

    public async Task DeleteHolidayAsync(Guid id, CancellationToken ct = default)
    {
        user.Ensure(Permissions.StructureManage);
        var holiday = await db.Holidays.FirstOrDefaultAsync(h => h.Id == id && h.OrganizationId == OrgId, ct)
            ?? throw new NotFoundException("Holiday", id);
        db.Holidays.Remove(holiday);
        await db.SaveChangesAsync(ct);
    }

    // ─── Announcements ───────────────────────────────────────────────────────────

    public async Task<List<AnnouncementDto>> ListAnnouncementsAsync(bool includeExpired, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var query = db.Announcements.AsNoTracking().Where(a => a.OrganizationId == OrgId);
        if (!includeExpired)
            query = query.Where(a => a.ExpiresAtUtc == null || a.ExpiresAtUtc > now);
        var items = await query.OrderByDescending(a => a.PublishedAtUtc).ToListAsync(ct);
        return items.Select(a => new AnnouncementDto
        {
            Id = a.Id, Title = a.Title, Body = a.Body,
            PublishedAtUtc = a.PublishedAtUtc, ExpiresAtUtc = a.ExpiresAtUtc
        }).ToList();
    }

    public async Task<AnnouncementDto> SaveAnnouncementAsync(SaveAnnouncementRequest request, CancellationToken ct = default)
    {
        user.Ensure(Permissions.AnnouncementsManage);
        await new SaveAnnouncementRequestValidator().ValidateAndThrowAsync(request, ct);

        Announcement announcement;
        if (request.Id is { } id)
        {
            announcement = await db.Announcements.FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == OrgId, ct)
                ?? throw new NotFoundException("Announcement", id);
        }
        else
        {
            announcement = new Announcement { OrganizationId = OrgId, PublishedAtUtc = clock.UtcNow, CreatedBy = user.Email };
            db.Announcements.Add(announcement);
        }

        announcement.Title = request.Title.Trim();
        announcement.Body = request.Body;
        announcement.ExpiresAtUtc = request.ExpiresAtUtc;
        await db.SaveChangesAsync(ct);

        return new AnnouncementDto
        {
            Id = announcement.Id, Title = announcement.Title, Body = announcement.Body,
            PublishedAtUtc = announcement.PublishedAtUtc, ExpiresAtUtc = announcement.ExpiresAtUtc
        };
    }

    // ─── Dashboard counters ──────────────────────────────────────────────────────

    public async Task<OrgDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var total = await db.Employees.AsNoTracking().CountAsync(ct);
        var active = await db.Employees.AsNoTracking().CountAsync(e => e.Status == EmploymentStatus.Active, ct);
        var probation = await db.Employees.AsNoTracking().CountAsync(e => e.Status == EmploymentStatus.Probation, ct);
        var departments = await db.Departments.AsNoTracking().CountAsync(ct);
        var branches = await db.Branches.AsNoTracking().CountAsync(ct);
        var pendingLeave = await db.LeaveRequests.AsNoTracking().CountAsync(r => r.Status == ReviewStatus.Pending, ct);
        var pendingCorrections = await db.AttendanceCorrectionRequests.AsNoTracking().CountAsync(c => c.Status == ReviewStatus.Pending, ct);
        var pendingProfile = await db.ProfileChangeRequests.AsNoTracking().CountAsync(p => p.Status == ReviewStatus.Pending, ct);
        var nextPeriod = await db.PayrollPeriods.AsNoTracking()
            .Where(p => !p.Closed && p.EndDate >= clock.TodayUtc).OrderBy(p => p.StartDate).FirstOrDefaultAsync(ct);

        return new OrgDashboardDto
        {
            TotalEmployees = total,
            ActiveEmployees = active,
            OnProbation = probation,
            Departments = departments,
            Branches = branches,
            PendingLeaveRequests = pendingLeave,
            PendingCorrections = pendingCorrections,
            PendingProfileChanges = pendingProfile,
            NextPayrollDate = nextPeriod?.EndDate
        };
    }

    // ─── mapping ─────────────────────────────────────────────────────────────────

    private static OrganizationDto ToDto(Organization org) => new()
    {
        Id = org.Id, Name = org.Name, Slug = org.Slug, Email = org.Email, Phone = org.Phone,
        LogoUrl = org.LogoUrl, BrandColor = org.BrandColor, AddressLine = org.AddressLine,
        City = org.City, Country = org.Country, TimeZone = org.TimeZone, Currency = org.Currency
    };

    private static SettingsDto ToSettingsDto(OrganizationSettings s) => new()
    {
        FiscalYearStartMonth = s.FiscalYearStartMonth,
        LateGraceMinutes = s.LateGraceMinutes,
        OvertimeEnabled = s.OvertimeEnabled,
        OvertimeMultiplier = s.OvertimeMultiplier,
        RequireApprovalForSensitiveProfileChanges = s.RequireApprovalForSensitiveProfileChanges,
        AutoApproveLeaveWithoutManager = s.AutoApproveLeaveWithoutManager
    };

    private static WorkScheduleDto ToScheduleDto(WorkSchedule s) => new()
    {
        Id = s.Id, Name = s.Name, IsDefault = s.IsDefault,
        Days = [.. s.Days.OrderBy(d => (int)d.DayOfWeek == 0 ? 7 : (int)d.DayOfWeek).Select(d => new WorkScheduleDayDto
        {
            DayOfWeek = d.DayOfWeek,
            IsWorkingDay = d.IsWorkingDay,
            StartTime = $"{d.StartMinutes / 60:D2}:{d.StartMinutes % 60:D2}",
            EndTime = $"{d.EndMinutes / 60:D2}:{d.EndMinutes % 60:D2}",
            BreakMinutes = d.BreakMinutes
        })]
    };

    private static HolidayDto ToHolidayDto(Holiday h) => new()
    {
        Id = h.Id, Name = h.Name, Date = h.Date, RecurringYearly = h.RecurringYearly, Description = h.Description
    };

    private static int ParseTime(string time)
    {
        var parts = time.Split(':');
        if (parts.Length < 2 || !int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m))
            throw new BadRequestException($"Invalid time '{time}'. Use HH:mm.");
        return Math.Clamp(h, 0, 23) * 60 + Math.Clamp(m, 0, 59);
    }
}
