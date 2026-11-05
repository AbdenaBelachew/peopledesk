using PeopleDesk.Domain.Common;

namespace PeopleDesk.Domain.Audit;

/// <summary>
/// Immutable audit trail. ChangesJson stores field names with values REDACTED for
/// sensitive fields (salary, bank account, national id, 2FA secrets) so payroll and
/// personal data never leak into logs.
/// </summary>
public class AuditLog : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid? UserId { get; set; }
    public string? ActorEmail { get; set; }

    public AuditAction Action { get; set; }
    public string EntityName { get; set; } = default!;
    public Guid EntityId { get; set; }

    public string ChangesJson { get; set; } = "[]";
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime TimestampUtc { get; set; }
}
