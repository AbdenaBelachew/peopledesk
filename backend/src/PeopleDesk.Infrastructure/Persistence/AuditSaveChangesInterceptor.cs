using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PeopleDesk.Application.Common;
using PeopleDesk.Domain.Audit;
using PeopleDesk.Domain.Attendance;
using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.Employees;
using PeopleDesk.Domain.Identity;
using PeopleDesk.Domain.Leave;
using PeopleDesk.Domain.Payroll;

namespace PeopleDesk.Infrastructure.Persistence;

/// <summary>
/// Sets audit fields, bumps concurrency tokens, and writes AuditLog rows for sensitive entities.
/// Values of sensitive fields (passwords, 2FA secrets, bank/national ids) are REDACTED so
/// payroll and personal data never leak into logs.
/// </summary>
public class AuditSaveChangesInterceptor(ICurrentUser currentUser, ITenantContext tenant, IDateTime clock) : SaveChangesInterceptor
{
    /// <summary>Entity → columns whose values must never be persisted into audit logs.</summary>
    private static readonly HashSet<string> SensitiveFields =
    [
        nameof(User.PasswordHash), nameof(User.TwoFactorSecret),
        nameof(RefreshToken.TokenHash), nameof(AuthToken.TokenHash),
        nameof(Employee.NationalId), nameof(Employee.BankAccount), nameof(Employee.TaxIdentificationNumber)
    ];

    /// <summary>Entities that produce audit entries when created/modified/deleted.</summary>
    private static readonly HashSet<Type> AuditedEntities =
    [
        typeof(Employee), typeof(User), typeof(Role), typeof(PayrollRun), typeof(PayrollPeriod),
        typeof(SalaryStructure), typeof(SalaryComponent), typeof(Payslip), typeof(LeaveRequest),
        typeof(LeaveType), typeof(LeavePolicy), typeof(AttendanceRecord)
    ];

    private static readonly HashSet<string> ValueSuppressedEntities =
    [
        // Payroll: record that a change happened, but never the amounts.
        nameof(PayrollRun), nameof(PayrollItem), nameof(SalaryStructure), nameof(SalaryComponent), nameof(Payslip)
    ];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext context)
    {
        var actor = currentUser.Email ?? "system";
        var actorId = currentUser.UserId;
        var now = clock.UtcNow;

        var logs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            if (entry.Entity is AuditLog) continue;

            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.CreatedBy = actor;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.ModifiedAtUtc = now;
                entry.Entity.ModifiedBy = actor;
                if (entry.Entity is IHasConcurrencyToken token)
                    token.ConcurrencyToken = Guid.NewGuid();
            }
            else if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable soft)
            {
                // Convert hard deletes of soft-deletable entities into tombstones.
                entry.State = EntityState.Modified;
                soft.IsDeleted = true;
                soft.DeletedAtUtc = now;
                entry.Entity.ModifiedAtUtc = now;
                entry.Entity.ModifiedBy = actor;
            }

            if (!AuditedEntities.Contains(entry.Entity.GetType())) continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            var entityName = entry.Entity.GetType().Name;
            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Created,
                EntityState.Deleted => AuditAction.Deleted,
                _ => AuditAction.Updated
            };

            var changes = entry.State == EntityState.Added
                ? Describe(entry.CurrentValues, entityName, skipUnchanged: false)
                : entry.State == EntityState.Deleted
                    ? "[]"
                    : Describe(entry.CurrentValues, entityName, skipUnchanged: true, original: entry.OriginalValues);

            logs.Add(new AuditLog
            {
                OrganizationId = entry.Entity is IOrganizationScoped scoped
                    ? scoped.OrganizationId
                    : tenant.OrganizationId ?? Guid.Empty,
                UserId = actorId,
                ActorEmail = currentUser.Email,
                Action = action,
                EntityName = entityName,
                EntityId = entry.Entity.Id,
                ChangesJson = changes,
                IpAddress = currentUser.IpAddress,
                UserAgent = currentUser.UserAgent,
                TimestampUtc = now
            });
        }

        foreach (var log in logs) context.Add(log);
    }

    private string Describe(
        Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues values,
        string entityName, bool skipUnchanged, Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues? original = null)
    {
        var list = new List<object>();
        foreach (var name in values.Properties.Select(p => p.Name))
        {
            if (name is nameof(Entity.CreatedAtUtc) or nameof(Entity.CreatedBy) or nameof(Entity.ModifiedAtUtc)
                or nameof(Entity.ModifiedBy) or "ConcurrencyToken") continue;

            var current = values[name];
            if (skipUnchanged)
            {
                var orig = original?[name];
                if (Equals(current, orig)) continue;
            }

            var value = ValueSuppressedEntities.Contains(entityName) || SensitiveFields.Contains(name)
                ? "[REDACTED]"
                : current is byte[] b ? Convert.ToBase64String(b[..Math.Min(b.Length, 32)]) + "…" : current;

            list.Add(new { field = name, value });
        }
        return JsonSerializer.Serialize(list);
    }
}
