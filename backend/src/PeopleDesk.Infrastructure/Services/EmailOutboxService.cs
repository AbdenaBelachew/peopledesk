using PeopleDesk.Application.Common;
using PeopleDesk.Domain.Notifications;

using PeopleDesk.Domain.Common;
namespace PeopleDesk.Infrastructure.Services;

/// <summary>Writes emails to the transactional outbox. A BackgroundService dispatches them; nothing blocks API calls.</summary>
public class EmailOutboxService(IAppDbContext db, IDateTime clock) : IEmailOutbox
{
    public async Task EnqueueAsync(Guid? organizationId, string toEmail, string subject, string bodyHtml,
        string? bodyText = null, CancellationToken ct = default)
    {
        db.EmailOutbox.Add(new EmailOutbox
        {
            OrganizationId = organizationId,
            ToEmail = toEmail,
            Subject = subject,
            BodyHtml = bodyHtml,
            BodyText = bodyText,
            Status = EmailOutboxStatus.Pending,
            CreatedAtUtc = clock.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}
