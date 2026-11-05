namespace PeopleDesk.Application.Common;

/// <summary>Central permission catalog. Codes are stable strings embedded in JWTs and checked on every endpoint.</summary>
public static class Permissions
{
    // Organization
    public const string OrgView = "org.view";
    public const string OrgUpdate = "org.update";
    public const string SettingsManage = "settings.manage";

    // Structure
    public const string StructureView = "structure.view";
    public const string StructureManage = "structure.manage";   // departments, branches, positions, types, schedules, holidays

    // Employees
    public const string EmployeesView = "employees.view";
    public const string EmployeesViewSensitive = "employees.viewSensitive"; // salary/bank/national id
    public const string EmployeesCreate = "employees.create";
    public const string EmployeesUpdate = "employees.update";
    public const string EmployeesDelete = "employees.delete";

    // Attendance
    public const string AttendanceViewOwn = "attendance.viewOwn";
    public const string AttendanceViewAll = "attendance.viewAll";
    public const string AttendanceManage = "attendance.manage";   // manual entry + edits
    public const string AttendanceApprove = "attendance.approve"; // correction requests

    // Leave
    public const string LeaveViewOwn = "leave.viewOwn";
    public const string LeaveViewAll = "leave.viewAll";
    public const string LeaveApprove = "leave.approve";           // manager + HR
    public const string LeaveManage = "leave.manage";             // types, policies, HR override

    // Payroll
    public const string PayrollView = "payroll.view";
    public const string PayrollRun = "payroll.run";               // create runs + calculate + adjustments
    public const string PayrollApprove = "payroll.approve";
    public const string PayrollFinalize = "payroll.finalize";
    public const string PayrollReverse = "payroll.reverse";

    // Payslips
    public const string PayslipsViewOwn = "payslips.viewOwn";
    public const string PayslipsViewAll = "payslips.viewAll";
    public const string PayslipsPublish = "payslips.publish";

    // Users & roles
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";

    // Reports
    public const string ReportsView = "reports.view";

    // HR requests & profile changes
    public const string SelfService = "selfService";              // portal access (all org users)
    public const string HrRequestsHandle = "hrRequests.handle";   // approve profile changes, HR requests
    public const string AnnouncementsManage = "announcements.manage";

    // Platform (SaaS super admin)
    public const string PlatformManage = "platform.manage";

    /// <summary>Roles defined for each organization at registration.</summary>
    public static class Roles
    {
        public const string Owner = "Organization Owner";
        public const string HrManager = "HR Manager";
        public const string PayrollManager = "Payroll Manager";
        public const string DepartmentManager = "Department Manager";
        public const string Employee = "Employee";
    }

    /// <summary>All permission codes. Must be declared before DefaultRolePermissions (static init order).</summary>
    public static readonly string[] All =
    [
        OrgView, OrgUpdate, SettingsManage,
        StructureView, StructureManage,
        EmployeesView, EmployeesViewSensitive, EmployeesCreate, EmployeesUpdate, EmployeesDelete,
        AttendanceViewOwn, AttendanceViewAll, AttendanceManage, AttendanceApprove,
        LeaveViewOwn, LeaveViewAll, LeaveApprove, LeaveManage,
        PayrollView, PayrollRun, PayrollApprove, PayrollFinalize, PayrollReverse,
        PayslipsViewOwn, PayslipsViewAll, PayslipsPublish,
        UsersManage, RolesManage,
        ReportsView, SelfService, HrRequestsHandle, AnnouncementsManage,
        PlatformManage
    ];

    /// <summary>Default permission matrix per spec.</summary>
    public static IReadOnlyDictionary<string, string[]> DefaultRolePermissions { get; } =
        new Dictionary<string, string[]>
        {
            [Roles.Owner] = All,
            [Roles.HrManager] =
            [
                OrgView, OrgUpdate, SettingsManage, StructureView, StructureManage,
                EmployeesView, EmployeesViewSensitive, EmployeesCreate, EmployeesUpdate, EmployeesDelete,
                AttendanceViewOwn, AttendanceViewAll, AttendanceManage, AttendanceApprove,
                LeaveViewOwn, LeaveViewAll, LeaveApprove, LeaveManage,
                PayslipsViewAll, ReportsView, SelfService, HrRequestsHandle, AnnouncementsManage,
                UsersManage
            ],
            [Roles.PayrollManager] =
            [
                OrgView, StructureView, EmployeesView, EmployeesViewSensitive,
                AttendanceViewOwn, AttendanceViewAll,
                LeaveViewOwn, LeaveViewAll,
                PayrollView, PayrollRun, PayrollApprove, PayrollFinalize, PayrollReverse,
                PayslipsViewOwn, PayslipsViewAll, PayslipsPublish, ReportsView, SelfService
            ],
            [Roles.DepartmentManager] =
            [
                OrgView, StructureView,
                EmployeesView, AttendanceViewOwn, AttendanceViewAll, AttendanceApprove,
                LeaveViewOwn, LeaveViewAll, LeaveApprove,
                ReportsView, SelfService
            ],
            [Roles.Employee] =
            [
                OrgView, AttendanceViewOwn, LeaveViewOwn, PayslipsViewOwn, SelfService
            ]
        };

    public static string Category(string code) => code.Split('.')[0];
}
