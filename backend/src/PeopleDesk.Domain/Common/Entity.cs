namespace PeopleDesk.Domain.Common;

/// <summary>Base for all persisted entities. Uses GUID v7 keys (sequential-friendly for PostgreSQL).</summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }
}

/// <summary>Marker for entities that participate in optimistic concurrency checks.</summary>
public interface IHasConcurrencyToken
{
    /// <summary>Bumped by the SaveChanges interceptor on every modification.</summary>
    Guid ConcurrencyToken { get; set; }
}

/// <summary>Marker for soft-deletable entities.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
}

/// <summary>Marker for entities owned by an organization (tenant-scoped rows).</summary>
public interface IOrganizationScoped
{
    Guid OrganizationId { get; set; }
}
