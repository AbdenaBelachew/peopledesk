using PeopleDesk.Domain.Common;

namespace PeopleDesk.Domain.Notifications;

public class Notification : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string? Link { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}

/// <summary>Transactional outbox row. The background dispatcher sends pending emails; API calls never block on SMTP.</summary>
public class EmailOutbox : Entity
{
    public Guid? OrganizationId { get; set; }   // null = platform/system email
    public string ToEmail { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string BodyHtml { get; set; } = default!;
    public string? BodyText { get; set; }
    public EmailOutboxStatus Status { get; set; } = EmailOutboxStatus.Pending;
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime? SentAtUtc { get; set; }
}
