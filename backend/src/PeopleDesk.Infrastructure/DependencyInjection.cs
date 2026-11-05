using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PeopleDesk.Application.Common;
using PeopleDesk.Infrastructure.Identity;
using PeopleDesk.Infrastructure.Persistence;
using PeopleDesk.Infrastructure.Services;

namespace PeopleDesk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            // Read lazily: test factories (WebApplicationFactory) append configuration sources
            // after service registration, so the value must be resolved at options-build time.
            var provider = config["Database:Provider"] ?? "postgresql";
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());

            if (provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var cs = config["Database:SqlitePath"] ?? "Data Source=peopledesk.db";
                options.UseSqlite(cs);
            }
            else
            {
                var cs = config.GetConnectionString("PostgreSQL")
                    ?? "Host=localhost;Port=5432;Database=peopledesk;Username=peopledesk;Password=peopledesk";
                options.UseNpgsql(cs);
            }
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ICurrentUser, CurrentUserService>();
        services.AddSingleton<IDateTime, DateTimeService>();
        services.AddScoped<IPasswordHasher, PasswordHasherService>();
        services.AddScoped<ITotpService, TotpService>();
        services.AddScoped<ITokenIssuer, JwtTokenService>();
        services.AddScoped<IEmailOutbox, EmailOutboxService>();
        services.AddScoped<IFileStorage, FileStorageService>();
        services.AddScoped<IXlsxExporter, XlsxExporter>();
        services.AddScoped<IPayslipPdfGenerator, Payroll.PayslipPdfGenerator>();

        services.AddHttpClient();

        services.Configure<BackgroundJobs.EmailOptions>(config.GetSection(BackgroundJobs.EmailOptions.SectionName));
        services.AddHostedService<BackgroundJobs.EmailDispatcherService>();

        return services;
    }
}
