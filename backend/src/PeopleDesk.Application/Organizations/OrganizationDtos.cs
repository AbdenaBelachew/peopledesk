using FluentValidation;
using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.Organizations;

namespace PeopleDesk.Application.Organizations;

public record OrganizationDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string Slug { get; init; } = default!;
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? LogoUrl { get; init; }
    public string? BrandColor { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public string TimeZone { get; init; } = default!;
    public string Currency { get; init; } = default!;
}

public record UpdateOrganizationRequest
{
    public string Name { get; init; } = default!;
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? LogoUrl { get; init; }
    public string? BrandColor { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public string TimeZone { get; init; } = default!;
    public string Currency { get; init; } = default!;
}

public record SettingsDto
{
    public int FiscalYearStartMonth { get; init; }
    public int LateGraceMinutes { get; init; }
    public bool OvertimeEnabled { get; init; }
    public decimal OvertimeMultiplier { get; init; }
    public bool RequireApprovalForSensitiveProfileChanges { get; init; }
    public bool AutoApproveLeaveWithoutManager { get; init; }
}

public record UpdateSettingsRequest
{
    public int FiscalYearStartMonth { get; init; } = 7;
    public int LateGraceMinutes { get; init; } = 10;
    public bool OvertimeEnabled { get; init; } = true;
    public decimal OvertimeMultiplier { get; init; } = 1.5m;
    public bool RequireApprovalForSensitiveProfileChanges { get; init; } = true;
    public bool AutoApproveLeaveWithoutManager { get; init; }
}

public record StructureItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Code { get; init; }
    public string? Description { get; init; }
    public Guid? ManagerEmployeeId { get; init; }
    public Guid? ParentRef { get; init; }
    public int EmployeeCount { get; init; }
}

public record StructureItemRequest
{
    public string Name { get; init; } = default!;
    public string? Code { get; init; }
    public string? Description { get; init; }
    public Guid? ManagerEmployeeId { get; init; }
    public string? Level { get; init; }
}

public record WorkScheduleDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public bool IsDefault { get; init; }
    public IReadOnlyList<WorkScheduleDayDto> Days { get; init; } = [];
}

public record WorkScheduleDayDto
{
    public DayOfWeek DayOfWeek { get; init; }
    public bool IsWorkingDay { get; init; }
    public string StartTime { get; init; } = "09:00";
    public string EndTime { get; init; } = "17:00";
    public int BreakMinutes { get; init; } = 60;
}

public record SaveWorkScheduleRequest
{
    public Guid? Id { get; init; }
    public string Name { get; init; } = default!;
    public bool IsDefault { get; init; }
    public IReadOnlyList<WorkScheduleDayDto> Days { get; init; } = [];
}

public record HolidayDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public DateOnly Date { get; init; }
    public bool RecurringYearly { get; init; }
    public string? Description { get; init; }
}

public record SaveHolidayRequest
{
    public Guid? Id { get; init; }
    public string Name { get; init; } = default!;
    public DateOnly Date { get; init; }
    public bool RecurringYearly { get; init; }
    public string? Description { get; init; }
}

public record AnnouncementDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = default!;
    public string Body { get; init; } = default!;
    public DateTime PublishedAtUtc { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
}

public record SaveAnnouncementRequest
{
    public Guid? Id { get; init; }
    public string Title { get; init; } = default!;
    public string Body { get; init; } = default!;
    public DateTime? ExpiresAtUtc { get; init; }
}

public record OrgDashboardDto
{
    public int TotalEmployees { get; init; }
    public int ActiveEmployees { get; init; }
    public int OnProbation { get; init; }
    public int Departments { get; init; }
    public int Branches { get; init; }
    public int PendingLeaveRequests { get; init; }
    public int PendingCorrections { get; init; }
    public int PendingProfileChanges { get; init; }
    public DateOnly? NextPayrollDate { get; init; }
}

public class UpdateOrganizationRequestValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.TimeZone).NotEmpty().Must(TzValid).WithMessage("Unknown IANA time zone.");
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }

    private static bool TzValid(string tz)
    {
        try { TimeZoneInfo.FindSystemTimeZoneById(tz); return true; }
        catch { return false; }
    }
}

public class StructureItemRequestValidator : AbstractValidator<StructureItemRequest>
{
    public StructureItemRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Code).MaximumLength(20);
    }
}

public class SaveHolidayRequestValidator : AbstractValidator<SaveHolidayRequest>
{
    public SaveHolidayRequestValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
}

public class SaveAnnouncementRequestValidator : AbstractValidator<SaveAnnouncementRequest>
{
    public SaveAnnouncementRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(5000);
    }
}
