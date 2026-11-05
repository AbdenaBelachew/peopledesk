using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PeopleDesk.Application.Common;

namespace PeopleDesk.Infrastructure.Services;

/// <summary>Resolves caller identity from the access token's claims (set by the JWT middleware).</summary>
public class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId => Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public Guid? EmployeeId => Guid.TryParse(Principal?.FindFirstValue("employee"), out var id) ? id : null;
    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);
    public string? FullName => Principal?.FindFirstValue("name");
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
    public bool IsPlatformAdmin =>
        bool.TryParse(Principal?.FindFirstValue("platform_admin"), out var v) && v;

    public Guid? OrganizationId =>
        Guid.TryParse(accessor.HttpContext?.Items["TenantId"] as string
            ?? Principal?.FindFirstValue("org"), out var id) ? id : null;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.ToString();

    public IReadOnlySet<string> PermissionSet
    {
        get
        {
            if (accessor.HttpContext?.Items["Permissions"] is IReadOnlySet<string> cached) return cached;
            var perms = Principal?.FindAll("perm").Select(c => c.Value).ToHashSet() ?? new HashSet<string>();
            var set = (IReadOnlySet<string>)perms;
            if (accessor.HttpContext is not null) accessor.HttpContext.Items["Permissions"] = set;
            return set;
        }
    }

    public bool Has(string permissionCode) => PermissionSet.Contains(permissionCode);

    public void Ensure(string permissionCode)
    {
        if (!IsAuthenticated) throw new ForbiddenException("Not authenticated.");
        if (IsPlatformAdmin) return;
        if (!Has(permissionCode))
            throw new ForbiddenException($"Missing required permission: {permissionCode}.");
    }
}

/// <summary>Tenant scope used by EF global query filters. Middleware sets it from the token (or X-Tenant-Id for platform admins).</summary>
public class TenantContext : ITenantContext
{
    public Guid? OrganizationId { get; private set; }
    public bool HasTenant => OrganizationId is not null;
    public void SetTenant(Guid organizationId) => OrganizationId = organizationId;
}

public class DateTimeService : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);
}
