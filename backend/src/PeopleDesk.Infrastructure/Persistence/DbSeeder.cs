using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PeopleDesk.Domain.Common;
using PeopleDesk.Application.Common;
using PeopleDesk.Application.Organizations;
using PeopleDesk.Domain.Identity;
using PeopleDesk.Domain.Organizations;

namespace PeopleDesk.Infrastructure.Persistence;

/// <summary>
/// Idempotent seeder for platform-level data: permission catalog, subscription plans,
/// the platform organization and its super admin. Demo tenant data is seeded separately
/// (DemoSeeder, development only).
/// </summary>
public class DbSeeder(IAppDbContext db, IPasswordHasher hasher, IConfiguration config, ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await OrganizationDefaults.EnsurePermissionCatalogAsync(db, ct);

        if (!await db.SubscriptionPlans.AnyAsync(ct))
        {
            db.SubscriptionPlans.AddRange(
                new SubscriptionPlan { Code = "trial", Name = "Trial", MaxEmployees = 10, PricePerMonth = 0, Description = "14-day evaluation" },
                new SubscriptionPlan { Code = "starter", Name = "Starter", MaxEmployees = 25, PricePerMonth = 49, Description = "Up to 25 employees" },
                new SubscriptionPlan { Code = "growth", Name = "Growth", MaxEmployees = 100, PricePerMonth = 129, Description = "Up to 100 employees" },
                new SubscriptionPlan { Code = "enterprise", Name = "Enterprise", MaxEmployees = 1000, PricePerMonth = 349, Description = "Up to 1000 employees" });
            await db.SaveChangesAsync(ct);
        }

        await SeedPlatformAdminAsync(ct);
    }

    private async Task SeedPlatformAdminAsync(CancellationToken ct)
    {
        var platform = await db.Organizations.FirstOrDefaultAsync(o => o.Type == OrganizationType.Platform, ct);
        if (platform is null)
        {
            platform = new Organization
            {
                Name = "PeopleDesk Platform",
                Slug = "peopledesk-platform",
                Type = OrganizationType.Platform,
                TimeZone = "UTC",
                Currency = "USD",
                IsActive = true
            };
            db.Organizations.Add(platform);
            await db.SaveChangesAsync(ct);
        }

        var email = (config["Platform:SuperAdminEmail"] ?? "superadmin@peopledesk.local").ToUpperInvariant();
        var password = config["Platform:SuperAdminPassword"] ?? "ChangeMe_Platform#2026";

        if (!await db.Users.AnyAsync(u => u.NormalizedEmail == email, ct))
        {
            var admin = new User
            {
                OrganizationId = platform.Id,
                Email = email.Trim(),
                NormalizedEmail = email,
                PasswordHash = hasher.Hash(password),
                FirstName = "Platform",
                LastName = "Admin",
                EmailVerifiedAtUtc = DateTime.UtcNow,
                IsActive = true
            };
            db.Users.Add(admin);

            var role = new Role
            {
                OrganizationId = platform.Id,
                Name = "SaaS Super Admin",
                Description = "Platform-level administration",
                IsSystem = true
            };
            foreach (var perm in await db.Permissions.Where(p => p.Code == Permissions.PlatformManage).ToListAsync(ct))
                role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
            db.Roles.Add(role);
            db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = role.Id });

            await db.SaveChangesAsync(ct);
            logger.LogWarning("Platform super admin created: {Email} — change the default password immediately.", email);
        }
    }
}
