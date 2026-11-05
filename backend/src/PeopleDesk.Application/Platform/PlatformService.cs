using Microsoft.EntityFrameworkCore;
using PeopleDesk.Application.Common;
using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.Identity;
using PeopleDesk.Domain.Organizations;

namespace PeopleDesk.Application.Platform;

/// <summary>SaaS platform administration. Requires PlatformManage; every method validates it.</summary>
public class PlatformService(IAppDbContext db, ICurrentUser user, IDateTime clock, IPasswordHasher hasher)
{
    private void EnsurePlatform() => user.Ensure(Permissions.PlatformManage);

    // ─── Tenants ─────────────────────────────────────────────────────────────────

    public record TenantDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = default!;
        public string Slug { get; init; } = default!;
        public string? Email { get; init; }
        public string? Country { get; init; }
        public bool IsActive { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public string? PlanName { get; init; }
        public string? SubscriptionStatus { get; init; }
        public int EmployeeCount { get; init; }
        public int UserCount { get; init; }
    }

    public async Task<PagedResult<TenantDto>> ListTenantsAsync(PagedRequest page, CancellationToken ct = default)
    {
        EnsurePlatform();

        var query = db.Organizations.AsNoTracking().Where(o => o.Type == OrganizationType.Customer);
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim().ToLower();
            query = query.Where(o => o.Name.ToLower().Contains(term) || o.Slug.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var orgs = await query.OrderByDescending(o => o.CreatedAtUtc)
            .Skip(page.Skip).Take(Math.Clamp(page.PageSize, 1, 100))
            .ToListAsync(ct);

        var orgIds = orgs.Select(o => o.Id).ToList();
        var subs = await db.Subscriptions.AsNoTracking().Include(s => s.Plan)
            .Where(s => orgIds.Contains(s.OrganizationId))
            .ToDictionaryAsync(s => s.OrganizationId, ct);
        var employeeCounts = await db.Employees.IgnoreQueryFilters().AsNoTracking()
            .Where(e => orgIds.Contains(e.OrganizationId) && !e.IsDeleted)
            .GroupBy(e => e.OrganizationId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
        var userCounts = await db.Users.AsNoTracking()
            .Where(u => orgIds.Contains(u.OrganizationId) && u.IsActive)
            .GroupBy(u => u.OrganizationId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

        var items = orgs.Select(o =>
        {
            subs.TryGetValue(o.Id, out var sub);
            return new TenantDto
            {
                Id = o.Id,
                Name = o.Name,
                Slug = o.Slug,
                Email = o.Email,
                Country = o.Country,
                IsActive = o.IsActive,
                CreatedAtUtc = o.CreatedAtUtc,
                PlanName = sub?.Plan.Name,
                SubscriptionStatus = sub?.Status.ToString(),
                EmployeeCount = employeeCounts.GetValueOrDefault(o.Id),
                UserCount = userCounts.GetValueOrDefault(o.Id)
            };
        }).ToList();

        return new PagedResult<TenantDto>(items, page.Page, page.PageSize, total);
    }

    public async Task SuspendTenantAsync(Guid orgId, bool suspend, CancellationToken ct = default)
    {
        EnsurePlatform();
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct)
            ?? throw new NotFoundException("Organization", orgId);
        if (org.Type == OrganizationType.Platform)
            throw new BadRequestException("The platform organization cannot be suspended.");

        org.IsActive = !suspend;
        await db.SaveChangesAsync(ct);
    }

    // ─── Plans & subscriptions ───────────────────────────────────────────────────

    public record PlanDto
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = default!;
        public string Name { get; init; } = default!;
        public int MaxEmployees { get; init; }
        public decimal PricePerMonth { get; init; }
        public string? Description { get; init; }
        public bool IsActive { get; init; }
    }

    public record SavePlanRequest
    {
        public Guid? Id { get; init; }
        public string Code { get; init; } = default!;
        public string Name { get; init; } = default!;
        public int MaxEmployees { get; init; }
        public decimal PricePerMonth { get; init; }
        public string? Description { get; init; }
        public bool IsActive { get; init; } = true;
    }

    public async Task<List<PlanDto>> ListPlansAsync(CancellationToken ct = default)
    {
        EnsurePlatform();
        var plans = await db.SubscriptionPlans.AsNoTracking().OrderBy(p => p.PricePerMonth).ToListAsync(ct);
        return plans.Select(p => new PlanDto
        { Id = p.Id, Code = p.Code, Name = p.Name, MaxEmployees = p.MaxEmployees, PricePerMonth = p.PricePerMonth, Description = p.Description, IsActive = p.IsActive }).ToList();
    }

    public async Task<PlanDto> SavePlanAsync(SavePlanRequest request, CancellationToken ct = default)
    {
        EnsurePlatform();
        SubscriptionPlan plan;
        if (request.Id is { } id)
        {
            plan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id, ct)
                ?? throw new NotFoundException("Plan", id);
        }
        else
        {
            if (await db.SubscriptionPlans.AnyAsync(p => p.Code == request.Code, ct))
                throw new ConflictException($"Plan code '{request.Code}' already exists.");
            plan = new SubscriptionPlan { Code = request.Code };
            db.SubscriptionPlans.Add(plan);
        }

        plan.Name = request.Name.Trim();
        plan.MaxEmployees = Math.Max(1, request.MaxEmployees);
        plan.PricePerMonth = request.PricePerMonth;
        plan.Description = request.Description;
        plan.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);
        return new PlanDto
        { Id = plan.Id, Code = plan.Code, Name = plan.Name, MaxEmployees = plan.MaxEmployees, PricePerMonth = plan.PricePerMonth, Description = plan.Description, IsActive = plan.IsActive };
    }

    public async Task<object> UpdateSubscriptionAsync(Guid orgId, Guid planId, string status, CancellationToken ct = default)
    {
        EnsurePlatform();
        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgId, ct)
            ?? throw new NotFoundException("Organization", orgId);
        var plan = await db.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new NotFoundException("Plan", planId);

        if (!Enum.TryParse<SubscriptionStatus>(status, ignoreCase: true, out var parsed))
            throw new BadRequestException($"Unknown subscription status '{status}'.");

        var sub = await db.Subscriptions.FirstOrDefaultAsync(s => s.OrganizationId == orgId, ct);
        if (sub is null)
        {
            sub = new Subscription { OrganizationId = orgId, StartedAtUtc = clock.UtcNow };
            db.Subscriptions.Add(sub);
        }
        sub.PlanId = planId;
        sub.Status = parsed;
        if (parsed == SubscriptionStatus.Active)
            sub.CurrentPeriodEndUtc = clock.UtcNow.AddMonths(1);

        await db.SaveChangesAsync(ct);
        return new { orgId = org.Id, orgName = org.Name, planId = plan.Id, planName = plan.Name, status = parsed.ToString() };
    }

    // ─── Platform stats ──────────────────────────────────────────────────────────

    public async Task<object> StatsAsync(CancellationToken ct = default)
    {
        EnsurePlatform();

        var tenants = await db.Organizations.CountAsync(o => o.Type == OrganizationType.Customer, ct);
        var activeTenants = await db.Organizations.CountAsync(o => o.Type == OrganizationType.Customer && o.IsActive, ct);
        var users = await db.Users.CountAsync(u => u.IsActive, ct);
        var employees = await db.Employees.IgnoreQueryFilters().CountAsync(e => !e.IsDeleted, ct);
        var runs = await db.PayrollRuns.IgnoreQueryFilters().CountAsync(r => r.Status == PayrollRunStatus.Finalized, ct);
        var trialSubs = await db.Subscriptions.CountAsync(s => s.Status == SubscriptionStatus.Trialing, ct);

        return new
        {
            tenants,
            activeTenants,
            suspendedTenants = tenants - activeTenants,
            users,
            employees,
            finalizedPayrollRuns = runs,
            trialSubscriptions = trialSubs
        };
    }
}
