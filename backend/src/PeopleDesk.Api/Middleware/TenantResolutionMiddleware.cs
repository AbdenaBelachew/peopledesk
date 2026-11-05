using PeopleDesk.Application.Common;
using Microsoft.EntityFrameworkCore;
using PeopleDesk.Domain.Common;

namespace PeopleDesk.Api.Middleware;

/// <summary>
/// Resolves the tenant scope from the access token before any tenant-scoped work runs.
/// Platform super admins may target another organization via X-Tenant-Id (impersonation is audit-worthy
/// and logged). Non-admins' header values are ignored — the JWT org claim always wins.
/// </summary>
public class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Tenant-Id";

    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser, ITenantContext tenantContext)
    {
        if (currentUser.IsAuthenticated && currentUser.OrganizationId is { } org)
        {
            // Platform admins can switch tenant per request.
            if (currentUser.IsPlatformAdmin && context.Request.Headers.TryGetValue(HeaderName, out var values)
                && Guid.TryParse(values.FirstOrDefault(), out var target))
            {
                tenantContext.SetTenant(target);
            }
            else
            {
                tenantContext.SetTenant(org);
            }

            context.Items["TenantId"] = tenantContext.OrganizationId?.ToString();
        }

        // tenant diagnostics removed
        await next(context);
    }
}

/// <summary>Central exception → HTTP mapping with a stable JSON error envelope.</summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var (status, code, message, errors) = Map(ex);

            if (status >= 500)
                logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            else
                logger.LogWarning("Request rejected ({Code}) on {Method} {Path}: {Message}",
                    code, context.Request.Method, context.Request.Path, message);

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                error = new { code, message, errors }
            });
            await context.Response.WriteAsync(payload);
        }
    }

    private static (int Status, string Code, string Message, object? Errors) Map(Exception ex) => ex switch
    {
        FluentValidation.ValidationException v => (
            400, "validation_failed", "One or more validation errors occurred.",
            v.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),
        NotFoundException n => (404, n.Code, n.Message, null),
        ForbiddenException f => (403, f.Code, f.Message, null),
        ConflictException c => (409, c.Code, c.Message, null),
        BusinessRuleException b => (422, b.Code, b.Message, null),
        BadRequestException br => (400, br.Code, br.Message, null),
        AppException app => (400, app.Code, app.Message, null),
        DbUpdateConcurrencyException => (409, "concurrency", "The record was modified by someone else. Reload and try again.", null),
        _ => (500, "internal_error", "An unexpected error occurred. Please try again.", null)
    };
}
