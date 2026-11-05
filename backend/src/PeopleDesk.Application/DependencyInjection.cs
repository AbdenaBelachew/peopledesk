using Microsoft.Extensions.DependencyInjection;
using PeopleDesk.Application.Auth;

namespace PeopleDesk.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<Organizations.OrganizationService>();
        services.AddScoped<Employees.EmployeeService>();
        services.AddScoped<Attendance.ScheduleProvider>();
        services.AddScoped<Attendance.AttendanceService>();
        services.AddScoped<Leave.LeaveService>();
        services.AddScoped<Notifications.NotificationService>();
        services.AddScoped<Payroll.PayrollService>();
        services.AddScoped<Payroll.SalaryStructureService>();
        services.AddScoped<Payslips.PayslipService>();
        services.AddScoped<SelfService.SelfServiceService>();
        services.AddScoped<Reports.ReportsService>();
        services.AddScoped<Platform.PlatformService>();
        return services;
    }
}
