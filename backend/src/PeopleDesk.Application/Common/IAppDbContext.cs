using Microsoft.EntityFrameworkCore;
using PeopleDesk.Domain.Audit;
using PeopleDesk.Domain.Attendance;
using PeopleDesk.Domain.Employees;
using PeopleDesk.Domain.Identity;
using PeopleDesk.Domain.Leave;
using PeopleDesk.Domain.Notifications;
using PeopleDesk.Domain.Organizations;
using PeopleDesk.Domain.Payroll;
using PeopleDesk.Domain.SelfService;

namespace PeopleDesk.Application.Common;

/// <summary>Persistence contract for application services. Implemented by Infrastructure.AppDbContext.</summary>
public interface IAppDbContext
{
    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }

    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<AuthToken> AuthTokens { get; }

    DbSet<Organization> Organizations { get; }
    DbSet<OrganizationSettings> OrganizationSettings { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<Department> Departments { get; }
    DbSet<Branch> Branches { get; }
    DbSet<Position> Positions { get; }
    DbSet<EmploymentType> EmploymentTypes { get; }
    DbSet<WorkSchedule> WorkSchedules { get; }
    DbSet<WorkScheduleDay> WorkScheduleDays { get; }
    DbSet<Holiday> Holidays { get; }
    DbSet<Announcement> Announcements { get; }

    DbSet<Employee> Employees { get; }
    DbSet<EmployeeContact> EmployeeContacts { get; }
    DbSet<EmployeeDocument> EmployeeDocuments { get; }
    DbSet<EmploymentHistory> EmploymentHistories { get; }

    DbSet<AttendanceRecord> AttendanceRecords { get; }
    DbSet<AttendanceCorrectionRequest> AttendanceCorrectionRequests { get; }

    DbSet<LeaveType> LeaveTypes { get; }
    DbSet<LeavePolicy> LeavePolicies { get; }
    DbSet<LeaveBalance> LeaveBalances { get; }
    DbSet<LeaveTransaction> LeaveTransactions { get; }
    DbSet<LeaveRequest> LeaveRequests { get; }

    DbSet<SalaryStructure> SalaryStructures { get; }
    DbSet<SalaryComponent> SalaryComponents { get; }
    DbSet<PayrollPeriod> PayrollPeriods { get; }
    DbSet<PayrollRun> PayrollRuns { get; }
    DbSet<PayrollRunAdjustment> PayrollRunAdjustments { get; }
    DbSet<PayrollItem> PayrollItems { get; }
    DbSet<PayrollEarning> PayrollEarnings { get; }
    DbSet<PayrollDeduction> PayrollDeductions { get; }
    DbSet<PayrollRuleSet> PayrollRuleSets { get; }
    DbSet<PayrollTaxBracket> PayrollTaxBrackets { get; }
    DbSet<Payslip> Payslips { get; }

    DbSet<Notification> Notifications { get; }
    DbSet<EmailOutbox> EmailOutbox { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<ProfileChangeRequest> ProfileChangeRequests { get; }
    DbSet<HRRequest> HRRequests { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
