using PeopleDesk.Domain.Common;

namespace PeopleDesk.Domain.SelfService;

/// <summary>Employee-submitted profile change awaiting HR review when the org requires approval for sensitive fields.</summary>
public class ProfileChangeRequest : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid EmployeeId { get; set; }

    public string FieldChangesJson { get; set; } = "{}";   // [{field, oldValue(redacted), newValue}]
    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
}

public class HRRequest : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid EmployeeId { get; set; }
    public HrRequestCategory Category { get; set; }
    public string Subject { get; set; } = default!;
    public string Body { get; set; } = default!;

    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;
    public string? Response { get; set; }
    public string? HandledBy { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
}
