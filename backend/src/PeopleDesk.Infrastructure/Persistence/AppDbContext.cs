using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PeopleDesk.Application.Common;
using PeopleDesk.Domain.Audit;
using PeopleDesk.Domain.Attendance;
using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.Employees;
using PeopleDesk.Domain.Identity;
using PeopleDesk.Domain.Leave;
using PeopleDesk.Domain.Notifications;
using PeopleDesk.Domain.Organizations;
using PeopleDesk.Domain.Payroll;
using PeopleDesk.Domain.SelfService;

namespace PeopleDesk.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
    : DbContext(options), IAppDbContext
{
    private readonly ITenantContext _tenant = tenantContext;

    // Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuthToken> AuthTokens => Set<AuthToken>();

    // Organizations
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationSettings> OrganizationSettings => Set<OrganizationSettings>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<EmploymentType> EmploymentTypes => Set<EmploymentType>();
    public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
    public DbSet<WorkScheduleDay> WorkScheduleDays => Set<WorkScheduleDay>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<Announcement> Announcements => Set<Announcement>();

    // Employees
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeContact> EmployeeContacts => Set<EmployeeContact>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<EmploymentHistory> EmploymentHistories => Set<EmploymentHistory>();

    // Attendance
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AttendanceCorrectionRequest> AttendanceCorrectionRequests => Set<AttendanceCorrectionRequest>();

    // Leave
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeavePolicy> LeavePolicies => Set<LeavePolicy>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveTransaction> LeaveTransactions => Set<LeaveTransaction>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    // Payroll
    public DbSet<SalaryStructure> SalaryStructures => Set<SalaryStructure>();
    public DbSet<SalaryComponent> SalaryComponents => Set<SalaryComponent>();
    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollRunAdjustment> PayrollRunAdjustments => Set<PayrollRunAdjustment>();
    public DbSet<PayrollItem> PayrollItems => Set<PayrollItem>();
    public DbSet<PayrollEarning> PayrollEarnings => Set<PayrollEarning>();
    public DbSet<PayrollDeduction> PayrollDeductions => Set<PayrollDeduction>();
    public DbSet<PayrollRuleSet> PayrollRuleSets => Set<PayrollRuleSet>();
    public DbSet<PayrollTaxBracket> PayrollTaxBrackets => Set<PayrollTaxBracket>();
    public DbSet<Payslip> Payslips => Set<Payslip>();

    // Notifications / audit / self-service
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<EmailOutbox> EmailOutbox => Set<EmailOutbox>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ProfileChangeRequest> ProfileChangeRequests => Set<ProfileChangeRequest>();
    public DbSet<HRRequest> HRRequests => Set<HRRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Uniform decimal precision — money is never floating point (numeric(18,2)).
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var prop in entity.GetProperties())
            {
                if (prop.ClrType == typeof(decimal))
                {
                    prop.SetPrecision(18);
                    prop.SetScale(2);
                }
            }
        }

        ConfigureIdentity(modelBuilder);
        ConfigureOrganizations(modelBuilder);
        ConfigureEmployees(modelBuilder);
        ConfigureAttendance(modelBuilder);
        ConfigureLeave(modelBuilder);
        ConfigurePayroll(modelBuilder);
        ConfigureCrossCutting(modelBuilder);
        ApplyTenantQueryFilters(modelBuilder);
    }

    private void ConfigureIdentity(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.NormalizedEmail).IsUnique();
            e.Property(u => u.Email).HasMaxLength(256);
            e.Property(u => u.PasswordHash).HasMaxLength(500);
            e.HasOne<Employee>().WithMany().HasForeignKey(u => u.EmployeeId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<Role>(e => e.HasIndex(r => new { r.OrganizationId, r.Name }).IsUnique());
        b.Entity<Permission>(e => e.HasIndex(p => p.Code).IsUnique());
        b.Entity<RolePermission>(e =>
        {
            e.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();
            e.HasOne(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(rp => rp.Permission).WithMany().HasForeignKey(rp => rp.PermissionId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<UserRole>(e =>
        {
            e.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();
            e.HasOne(ur => ur.Role).WithMany().HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => new { t.UserId, t.ExpiresAtUtc });
            e.HasOne<User>().WithMany(u => u.RefreshTokens).HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AuthToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private void ConfigureOrganizations(ModelBuilder b)
    {
        b.Entity<Organization>(e =>
        {
            e.HasIndex(o => o.Slug).IsUnique();
            e.Property(o => o.Name).HasMaxLength(120);
        });
        b.Entity<OrganizationSettings>(e => e.HasIndex(s => s.OrganizationId).IsUnique());
        b.Entity<SubscriptionPlan>(e => e.HasIndex(p => p.Code).IsUnique());
        b.Entity<Subscription>(e =>
        {
            e.HasIndex(s => s.OrganizationId).IsUnique();
            e.HasOne(s => s.Plan).WithMany().HasForeignKey(s => s.PlanId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Department>(e => e.HasIndex(d => new { d.OrganizationId, d.Name }).IsUnique());
        b.Entity<Branch>(e => e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique());
        b.Entity<Position>(e => e.HasIndex(x => new { x.OrganizationId, x.Title }).IsUnique());
        b.Entity<EmploymentType>(e => e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique());
        b.Entity<WorkSchedule>(e => e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique());
        b.Entity<WorkScheduleDay>(e =>
        {
            e.HasIndex(d => new { d.ScheduleId, d.DayOfWeek }).IsUnique();
            e.HasOne(d => d.Schedule).WithMany(s => s.Days).HasForeignKey(d => d.ScheduleId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Holiday>(e => e.HasIndex(h => new { h.OrganizationId, h.Date }));
    }

    private void ConfigureEmployees(ModelBuilder b)
    {
        b.Entity<Employee>(e =>
        {
            e.HasIndex(x => new { x.OrganizationId, x.EmployeeNumber }).IsUnique();
            e.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
            e.HasIndex(x => new { x.OrganizationId, x.Status });
            e.HasIndex(x => x.LastName);
            e.Property(x => x.EmployeeNumber).HasMaxLength(30);
            e.Property(x => x.FirstName).HasMaxLength(60);
            e.Property(x => x.LastName).HasMaxLength(60);
        });
        b.Entity<EmployeeContact>(e =>
        {
            e.HasIndex(c => c.EmployeeId);
            e.HasOne<Employee>().WithMany().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<EmployeeDocument>(e =>
        {
            e.HasIndex(d => d.EmployeeId);
            e.HasOne<Employee>().WithMany().HasForeignKey(d => d.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<EmploymentHistory>(e =>
        {
            e.HasIndex(h => new { h.EmployeeId, h.EffectiveFrom });
            e.HasOne<Employee>().WithMany().HasForeignKey(h => h.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private void ConfigureAttendance(ModelBuilder b)
    {
        b.Entity<AttendanceRecord>(e =>
        {
            e.HasIndex(a => new { a.OrganizationId, a.EmployeeId, a.WorkDate }).IsUnique();
            e.HasIndex(a => new { a.OrganizationId, a.WorkDate });
            e.HasOne<Employee>().WithMany().HasForeignKey(a => a.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AttendanceCorrectionRequest>(e =>
        {
            e.HasIndex(c => new { c.OrganizationId, c.Status });
            e.HasOne<Employee>().WithMany().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private void ConfigureLeave(ModelBuilder b)
    {
        b.Entity<LeaveType>(e => e.HasIndex(t => new { t.OrganizationId, t.Code }).IsUnique());
        b.Entity<LeavePolicy>(e =>
        {
            e.HasIndex(p => p.LeaveTypeId).IsUnique();
            e.HasOne(p => p.LeaveType).WithMany().HasForeignKey(p => p.LeaveTypeId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<LeaveBalance>(e =>
        {
            e.HasIndex(x => new { x.OrganizationId, x.EmployeeId, x.LeaveTypeId, x.Year }).IsUnique();
            e.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
        });
        b.Entity<LeaveTransaction>(e =>
        {
            e.HasIndex(t => new { t.EmployeeId, t.LeaveTypeId, t.Year });
            e.HasOne<Employee>().WithMany().HasForeignKey(t => t.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<LeaveRequest>(e =>
        {
            e.HasIndex(r => new { r.OrganizationId, r.Status });
            e.HasIndex(r => new { r.EmployeeId, r.StartDate, r.EndDate });
            e.HasOne(r => r.LeaveType).WithMany().HasForeignKey(r => r.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private void ConfigurePayroll(ModelBuilder b)
    {
        b.Entity<SalaryStructure>(e => e.HasIndex(s => new { s.EmployeeId, s.EffectiveFrom }));
        b.Entity<SalaryComponent>(e =>
        {
            e.HasOne(c => c.SalaryStructure).WithMany(s => s.Components).HasForeignKey(c => c.SalaryStructureId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PayrollPeriod>(e =>
        {
            e.HasIndex(p => new { p.OrganizationId, p.Year, p.Month, p.Type }).IsUnique();
        });
        b.Entity<PayrollRun>(e =>
        {
            // One relationship, both navigations declared — avoids a shadow FK column.
            e.HasOne(r => r.Period).WithOne(p => p.ActiveRun)
                .HasForeignKey<PayrollRun>(r => r.PayrollPeriodId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(r => r.ConcurrencyToken).IsConcurrencyToken();
            e.HasMany(r => r.Items).WithOne(i => i.PayrollRun).HasForeignKey(i => i.PayrollRunId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PayrollRunAdjustment>(e => e.HasIndex(a => new { a.PayrollRunId, a.EmployeeId }));
        b.Entity<PayrollItem>(e =>
        {
            e.HasIndex(i => new { i.PayrollRunId, i.EmployeeId }).IsUnique();
            e.HasMany(i => i.Earnings).WithOne(x => x.PayrollItem).HasForeignKey(x => x.PayrollItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(i => i.Deductions).WithOne(x => x.PayrollItem).HasForeignKey(x => x.PayrollItemId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PayrollRuleSet>(e => e.HasIndex(r => new { r.OrganizationId, r.JurisdictionCode, r.EffectiveFrom }));
        b.Entity<PayrollTaxBracket>(e =>
        {
            e.HasIndex(t => new { t.RuleSetId, t.LowerBound });
            e.HasOne(t => t.RuleSet).WithMany(r => r.TaxBrackets).HasForeignKey(t => t.RuleSetId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Payslip>(e =>
        {
            e.HasIndex(p => new { p.OrganizationId, p.PayslipNumber }).IsUnique();
            e.HasIndex(p => new { p.OrganizationId, p.EmployeeId, p.PayrollPeriodId });
        });
    }

    private void ConfigureCrossCutting(ModelBuilder b)
    {
        b.Entity<Notification>(e =>
        {
            e.HasIndex(n => new { n.UserId, n.ReadAtUtc });
            e.HasOne<User>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<EmailOutbox>(e => e.HasIndex(o => new { o.Status, o.CreatedAtUtc }));
        b.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => new { a.OrganizationId, a.TimestampUtc });
            e.HasIndex(a => new { a.EntityName, a.EntityId });
        });
        b.Entity<ProfileChangeRequest>(e => e.HasIndex(p => new { p.OrganizationId, p.Status }));
        b.Entity<HRRequest>(e => e.HasIndex(r => new { r.OrganizationId, r.Status }));
    }

    /// <summary>
    /// Read by tenant query filters. Referencing a member of THIS context instance (not a foreign
    /// service constant) is essential: EF caches the compiled model across DbContext instances, but
    /// re-binds context-instance members on every query execution — so this always reflects the
    /// current request's tenant, which the middleware mutates via the scoped ITenantContext.
    /// </summary>
    public Guid? CurrentTenantId => _tenant.OrganizationId;

    /// <summary>Server-enforced tenant isolation: every org-scoped table filters on the current tenant.</summary>
    private void ApplyTenantQueryFilters(ModelBuilder b)
    {
        foreach (var entityType in b.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;

            if (typeof(IOrganizationScoped).IsAssignableFrom(clr))
            {
                var param = Expression.Parameter(clr, "e");
                var orgProp = Expression.Property(param, nameof(IOrganizationScoped.OrganizationId));
                var tenantProp = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
                var equal = Expression.Equal(Expression.Convert(orgProp, typeof(Guid?)), tenantProp);

                Expression body = equal;
                if (typeof(ISoftDeletable).IsAssignableFrom(clr))
                {
                    var notDeleted = Expression.Equal(
                        Expression.Property(param, nameof(ISoftDeletable.IsDeleted)),
                        Expression.Constant(false));
                    body = Expression.AndAlso(body, notDeleted);
                }

                var lambda = Expression.Lambda(body, param);
                b.Entity(clr).HasQueryFilter(lambda);
            }
            else if (typeof(ISoftDeletable).IsAssignableFrom(clr))
            {
                var param = Expression.Parameter(clr, "e");
                var notDeleted = Expression.Equal(
                    Expression.Property(param, nameof(ISoftDeletable.IsDeleted)),
                    Expression.Constant(false));
                b.Entity(clr).HasQueryFilter(Expression.Lambda(notDeleted, param));
            }
        }
    }
}
