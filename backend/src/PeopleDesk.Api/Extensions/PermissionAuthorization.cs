using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using PeopleDesk.Application.Common;

namespace PeopleDesk.Api.Extensions;

/// <summary>Endpoint-level permission requirement, e.g. [HasPermission(Permissions.EmployeesCreate)].</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class HasPermissionAttribute(string code) : AuthorizeAttribute
{
    public string Permission { get; } = code;
}

/// <summary>Builds one policy per permission code on demand; the JWT claim "perm" carries the grant.</summary>
public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "perm:";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName[Prefix.Length..]))
                .Build();
        }
        return await base.GetPolicyAsync(policyName);
    }
}

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // Platform super admins pass everything; org users must hold the exact permission claim.
        if (context.User.HasClaim("platform_admin", "true"))
        {
            context.Succeed(requirement);
        }
        else if (context.User.HasClaim(c => c.Type == "perm" && c.Value == requirement.Permission))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

public static class PermissionAuthorizationExtensions
{
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        return services;
    }
}

public static class HasPermissionExtensions
{
    /// <summary>Builds an [Authorize(Policy = "perm:{code}")] policy string for endpoint attributes.</summary>
    public static string Policy(string code) => PermissionPolicyProvider.Prefix + code;

    public static AuthorizeAttribute Authorize(string code) => new(Policy(code));
}
