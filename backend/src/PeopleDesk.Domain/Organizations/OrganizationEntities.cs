using PeopleDesk.Domain.Common;

namespace PeopleDesk.Domain.Organizations;

public class Organization : Entity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public OrganizationType Type { get; set; } = OrganizationType.Customer;

    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? LogoUrl { get; set; }
    public string? BrandColor { get; set; }

    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    /// <summary>IANA time zone id (e.g. "Africa/Addis_Ababa"). Timestamps stored in UTC; local math uses this.</summary>
    public string TimeZone { get; set; } = "Africa/Addis_Ababa";
    public string Currency { get; set; } = "ETB";

    /// <summary>Platform-level suspension switch for SaaS administration.</summary>
    public bool IsActive { get; set; } = true;

    public Subscription? Subscription { get; set; }
    public OrganizationSettings? Settings { get; set; }
}

public class OrganizationSettings : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public int FiscalYearStartMonth { get; set; } = 7; // Ethiopian fiscal year by default
    public int LateGraceMinutes { get; set; } = 10;
    public bool OvertimeEnabled { get; set; } = true;
    public decimal OvertimeMultiplier { get; set; } = 1.5m;
    public bool RequireApprovalForSensitiveProfileChanges { get; set; } = true;
    public bool AutoApproveLeaveWithoutManager { get; set; } = false;
    public int EmployeeNumberSequence { get; set; } = 0;
}

public class SubscriptionPlan : Entity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int MaxEmployees { get; set; }
    public decimal PricePerMonth { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Subscription : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = default!;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trialing;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? TrialEndsAtUtc { get; set; }
    public DateTime? CurrentPeriodEndUtc { get; set; }
    public string? Notes { get; set; }
}
