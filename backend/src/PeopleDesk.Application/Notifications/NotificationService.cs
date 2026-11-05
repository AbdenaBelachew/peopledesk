using Microsoft.EntityFrameworkCore;
using PeopleDesk.Application.Common;
using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.Notifications;

namespace PeopleDesk.Application.Notifications;

/// <summary>In-app notification hub. Emails flow through the outbox; this writes in-app rows.</summary>
public class NotificationService(IAppDbContext db, ICurrentUser user, IDateTime clock)
{
    public async Task NotifyEmployeeAsync(Guid employeeId, NotificationType type, string title, string body,
        string? link = null, CancellationToken ct = default)
    {
        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee?.UserId is not { } userId) return;

        db.Notifications.Add(new Notification
        {
            OrganizationId = employee.OrganizationId,
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            Link = link
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task NotifyUserAsync(Guid userId, Guid organizationId, NotificationType type, string title,
        string body, string? link = null, CancellationToken ct = default)
    {
        db.Notifications.Add(new Notification
        {
            OrganizationId = organizationId,
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            Link = link
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Fans out to all active users holding a permission in the current org (e.g. leave.approve).</summary>
    public async Task NotifyApproversAsync(NotificationType type, string title, string body,
        string? link = null, CancellationToken ct = default)
    {
        if (user.OrganizationId is not { } orgId) return;

        var approverIds = await db.UserRoles.AsNoTracking()
            .Join(db.RolePermissions, ur => ur.RoleId, rp => rp.RoleId, (ur, rp) => new { ur.UserId, rp.PermissionId, ur.RoleId })
            .Join(db.Roles, x => x.RoleId, r => r.Id, (x, r) => new { x.UserId, x.PermissionId, r.OrganizationId })
            .Join(db.Permissions, x => x.PermissionId, p => p.Id, (x, p) => new { x.UserId, x.OrganizationId, p.Code })
            .Join(db.Users, x => x.UserId, u => u.Id, (x, u) => new { x.UserId, x.OrganizationId, x.Code, u.IsActive })
            .Where(x => x.OrganizationId == orgId && x.Code == Permissions.LeaveApprove && x.IsActive)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var userId in approverIds)
        {
            db.Notifications.Add(new Notification
            {
                OrganizationId = orgId,
                UserId = userId,
                Type = type,
                Title = title,
                Body = body,
                Link = link
            });
        }
        await db.SaveChangesAsync(ct);
    }

    // ─── Reading ─────────────────────────────────────────────────────────────────

    public record NotificationDto(Guid Id, NotificationType Type, string Title, string Body, string? Link, DateTime? ReadAtUtc, DateTime CreatedAtUtc);

    public async Task<PagedResult<NotificationDto>> ListAsync(PagedRequest page, bool unreadOnly, CancellationToken ct = default)
    {
        if (user.UserId is not { } userId) throw new ForbiddenException("Not authenticated.");

        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => n.ReadAtUtc == null);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip(page.Skip).Take(Math.Clamp(page.PageSize, 1, 100))
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Body, n.Link, n.ReadAtUtc, n.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<NotificationDto>(items, page.Page, page.PageSize, total);
    }

    public async Task<int> UnreadCountAsync(CancellationToken ct = default)
    {
        if (user.UserId is not { } userId) return 0;
        return await db.Notifications.AsNoTracking().CountAsync(n => n.UserId == userId && n.ReadAtUtc == null, ct);
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct = default)
    {
        if (user.UserId is not { } userId) throw new ForbiddenException("Not authenticated.");
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
            ?? throw new NotFoundException("Notification", id);
        n.ReadAtUtc ??= clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(CancellationToken ct = default)
    {
        if (user.UserId is not { } userId) throw new ForbiddenException("Not authenticated.");
        var unread = await db.Notifications.Where(n => n.UserId == userId && n.ReadAtUtc == null).ToListAsync(ct);
        var now = clock.UtcNow;
        foreach (var n in unread) n.ReadAtUtc = now;
        await db.SaveChangesAsync(ct);
    }
}
