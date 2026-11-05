using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using Microsoft.AspNetCore.HttpOverrides;
using PeopleDesk.Api.Extensions;
using PeopleDesk.Api.Middleware;
using PeopleDesk.Application;
using PeopleDesk.Application.Auth;
using PeopleDesk.Application.Common;
using PeopleDesk.Infrastructure;
using PeopleDesk.Infrastructure.Persistence;
using Serilog;

namespace PeopleDesk.Api;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File("logs/peopledesk-.log", rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30, outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .CreateLogger();

        var builder = WebApplication.CreateBuilder(args);
        builder.Host.UseSerilog();

        // EF command logging in development (toggled, off by default).
        if (builder.Configuration.GetValue<bool>("DebugSql"))
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Information)
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

        // ─── Layers ──────────────────────────────────────────────────────────────
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructure(builder.Configuration);

        // ─── Controllers & JSON ──────────────────────────────────────────────────
        builder.Services.AddControllers().AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });
        builder.Services.AddEndpointsApiExplorer();

        // ─── Auth: JWT bearer + permission policies ──────────────────────────────
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "peopledesk",
                    ValidateAudience = true,
                    ValidAudience = builder.Configuration["Jwt:Audience"] ?? "peopledesk-web",
                    ValidateIssuerSigningKey = true,
                    // Read lazily: test factories append configuration sources after service registration.
                    IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"] ?? "")),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = "role",
                    NameClaimType = System.Security.Claims.ClaimTypes.Email
                };

                o.Events = new JwtBearerEvents
                {
                    OnChallenge = async ctx =>
                    {
                        ctx.HandleResponse();
                        ctx.Response.StatusCode = 401;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync("""{"error":{"code":"unauthorized","message":"Authentication required."}}""");
                    }
                };
            });
        builder.Services.AddPermissionAuthorization();

        // ─── Rate limiting (skipped in integration tests via Testing:DisableRateLimiting) ───
        if (builder.Configuration.GetValue<bool>("Testing:DisableRateLimiting") != true)
        {
            builder.Services.AddRateLimiter(o =>
            {
            o.RejectionStatusCode = 429;
            o.AddPolicy<string>("auth", httpContext =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(ip,
                    _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                    });
            });
            o.AddPolicy<string>("api", httpContext =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(ip,
                    _ => new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 300, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0
                    });
            });
            });
        }

        // ─── CORS (dev direct API access; SPA normally calls same-origin /api) ───
        builder.Services.AddCors(o => o.AddPolicy("spa", p => p
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        // ─── OpenAPI + Swagger UI ────────────────────────────────────────────────
        builder.Services.AddOpenApi();

        // ─── App ─────────────────────────────────────────────────────────────────
        var app = builder.Build();

        app.UseSerilogRequestLogging(o => o.EnrichDiagnosticContext = (diag, http) =>
            diag.Set("RemoteIp", http.Connection.RemoteIpAddress?.ToString()));

        // Honor reverse-proxy TLS so secure cookies and redirect URLs behave correctly.
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedProto
        });

        // Development convenience: apply migrations (PostgreSQL) or create schema (SQLite dev fallback).
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (app.Configuration["Database:Provider"]?.Equals("sqlite", StringComparison.OrdinalIgnoreCase) == true)
            {
                await db.Database.EnsureCreatedAsync();
            }
            else
            {
                await db.Database.MigrateAsync();
            }

            await new DbSeeder(
                scope.ServiceProvider.GetRequiredService<IAppDbContext>(),
                scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
                app.Configuration,
                scope.ServiceProvider.GetRequiredService<ILogger<DbSeeder>>()).SeedAsync();

            await new DemoSeeder(
                scope.ServiceProvider.GetRequiredService<IAppDbContext>(),
                scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
                app.Configuration,
                scope.ServiceProvider.GetRequiredService<IDateTime>(),
                scope.ServiceProvider.GetRequiredService<ILogger<DemoSeeder>>()).SeedAsync();
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUI(o =>
            {
                o.SwaggerEndpoint("/openapi/v1.json", "PeopleDesk API v1");
                o.DocumentTitle = "PeopleDesk API";
            });
        }

        app.UseCors("spa");
        app.UseAuthentication();
        app.UseMiddleware<TenantResolutionMiddleware>();
        app.UseAuthorization();
        if (app.Configuration.GetValue<bool>("Testing:DisableRateLimiting") != true)
        {
            app.UseRateLimiter();
        }
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        app.MapControllers();

        // Health check: liveness + database reachability.
        app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
        {
            try
            {
                var ok = await db.Database.CanConnectAsync(ct);
                return ok ? Results.Ok(new { status = "healthy", database = "up" })
                          : Results.StatusCode(503);
            }
            catch
            {
                return Results.StatusCode(503);
            }
        });

        await app.RunAsync();
    }
}
