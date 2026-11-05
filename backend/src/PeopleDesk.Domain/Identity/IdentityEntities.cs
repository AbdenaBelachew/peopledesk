using PeopleDesk.Domain.Common;

namespace PeopleDesk.Domain.Identity;

public class User : Entity, IHasConcurrencyToken
{
    public Guid OrganizationId { get; set; }
    public string Email { get; set; } = default!;
    public string NormalizedEmail { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;

    public bool IsActive { get; set; } = true;
    public DateTime? EmailVerifiedAtUtc { get; set; }
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();

    public bool TwoFactorEnabled { get; set; }
    public string? TwoFactorSecret { get; set; }

    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }

    public Guid? EmployeeId { get; set; }

    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

public class Role : Entity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class Permission : Entity
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!;
}

public class RolePermission : Entity
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = default!;
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = default!;
}

public class UserRole : Entity
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = default!;
    public User User { get; set; } = default!;
}

public class RefreshToken : Entity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }

    public bool IsUsable => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}

public class AuthToken : Entity
{
    public Guid UserId { get; set; }
    public AuthTokenType Type { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }

    public bool IsUsable => UsedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}
