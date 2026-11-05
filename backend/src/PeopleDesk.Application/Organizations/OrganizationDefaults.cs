using Microsoft.EntityFrameworkCore;
using PeopleDesk.Application.Common;
using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.Identity;
using PeopleDesk.Domain.Leave;
using PeopleDesk.Domain.Organizations;
using PeopleDesk.Domain.Payroll;

namespace PeopleDesk.Application.Organizations;

/// <summary>
/// Provisioning applied when a new organization is created (used by registration and seeding):
/// settings, employment types, default work schedule, standard leave types/policies,
/// system roles with default permissions, and jurisdiction payroll rules.
/// </summary>
public static class OrganizationDefaults
{
    public static async Task CreateDefaultStructureAsync(IAppDbContext db, Organization org, string country, CancellationToken ct = default)
    {
        db.OrganizationSettings.Add(new OrganizationSettings { OrganizationId = org.Id });

        foreach (var name in new[] { "Full-Time", "Part-Time", "Contract", "Internship" })
            db.EmploymentTypes.Add(new EmploymentType { OrganizationId = org.Id, Name = name });

        var schedule = new WorkSchedule { OrganizationId = org.Id, Name = "Standard (Mon–Fri)", IsDefault = true };
        for (var d = DayOfWeek.Monday; d <= DayOfWeek.Friday; d++)
            schedule.Days.Add(new WorkScheduleDay { OrganizationId = org.Id, ScheduleId = schedule.Id, DayOfWeek = d, IsWorkingDay = true, StartMinutes = 9 * 60, EndMinutes = 17 * 60, BreakMinutes = 60 });
        schedule.Days.Add(new WorkScheduleDay { OrganizationId = org.Id, ScheduleId = schedule.Id, DayOfWeek = DayOfWeek.Saturday, IsWorkingDay = false });
        schedule.Days.Add(new WorkScheduleDay { OrganizationId = org.Id, ScheduleId = schedule.Id, DayOfWeek = DayOfWeek.Sunday, IsWorkingDay = false });
        db.WorkSchedules.Add(schedule);

        await CreateDefaultLeaveTypesAsync(db, org, ct);
        await CreatePayrollRuleSetAsync(db, org, country, ct);
    }

    public static Task CreateDefaultLeaveTypesAsync(IAppDbContext db, Organization org, CancellationToken ct = default)
    {
        (string Name, string Code, string Color, bool Paid, decimal Days)[] types =
        [
            ("Annual Leave", "ANNUAL", "#2563eb", true, 20),
            ("Sick Leave", "SICK", "#dc2626", true, 10),
            ("Maternity Leave", "MATERNITY", "#db2777", true, 90),
            ("Paternity Leave", "PATERNITY", "#7c3aed", true, 5),
            ("Unpaid Leave", "UNPAID", "#6b7280", false, 0)
        ];

        foreach (var (name, code, color, paid, days) in types)
        {
            var type = new LeaveType { OrganizationId = org.Id, Name = name, Code = code, ColorHex = color, IsPaid = paid };
            db.LeaveTypes.Add(type);
            db.LeavePolicies.Add(new LeavePolicy
            {
                OrganizationId = org.Id,
                LeaveTypeId = type.Id,
                AnnualEntitlementDays = days,
                AccrualFrequency = LeaveAccrualFrequency.YearlyUpfront,
                CarryOverMaxDays = paid ? 5 : 0,
                MaxConsecutiveDays = code == "MATERNITY" ? 90 : 30
            });
        }
        return Task.CompletedTask;
    }

    /// <summary>Seeds the jurisdiction payroll rule sets. Ethiopia gets both effective-dated versions (2016 legacy + 2025 reform).</summary>
    public static Task CreatePayrollRuleSetAsync(IAppDbContext db, Organization org, string country, CancellationToken ct = default)
    {
        if (!string.Equals(country?.Trim(), "Ethiopia", StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        void AddRules(string version, string name, DateOnly from, DateOnly? to,
            IReadOnlyList<IncomeTaxCalculator.Bracket> brackets)
        {
            var rules = new PayrollRuleSet
            {
                OrganizationId = org.Id,
                JurisdictionCode = EthiopiaPayroll.JurisdictionCode,
                Name = name,
                Version = version,
                EffectiveFrom = from,
                EffectiveTo = to,
                PensionEmployeeRate = EthiopiaPayroll.PensionEmployeeRate,
                PensionEmployerRate = EthiopiaPayroll.PensionEmployerRate
            };
            foreach (var b in brackets)
                rules.TaxBrackets.Add(new PayrollTaxBracket
                {
                    OrganizationId = org.Id,
                    RuleSetId = rules.Id,
                    LowerBound = b.LowerBound,
                    UpperBound = b.UpperBound,
                    Rate = b.Rate,
                    QuickDeduction = b.QuickDeduction
                });
            db.PayrollRuleSets.Add(rules);
        }

        AddRules(EthiopiaPayroll.Version2025, "Ethiopia Income Tax & Pension (Proc. 1395/2025)",
            EthiopiaPayroll.EffectiveFrom2025, null, EthiopiaPayroll.MonthlyIncomeTaxBrackets2025);
        AddRules(EthiopiaPayroll.Version2016, "Ethiopia Income Tax & Pension (Proc. 979/2016, amended)",
            EthiopiaPayroll.EffectiveFrom2016, EthiopiaPayroll.EffectiveTo2016, EthiopiaPayroll.MonthlyIncomeTaxBrackets2016);

        return Task.CompletedTask;
    }

    /// <summary>Creates the five system roles and wires default permissions; assigns owner role to the given user.</summary>
    public static async Task CreateSystemRolesAsync(IAppDbContext db, Organization org, Guid? ownerUserId = null, CancellationToken ct = default)
    {
        var permissions = await db.Permissions.ToListAsync(ct);
        var byCode = permissions.ToDictionary(p => p.Code);

        (string RoleName, string[] Codes)[] definitions =
        [
            (Permissions.Roles.Owner, Permissions.DefaultRolePermissions[Permissions.Roles.Owner]),
            (Permissions.Roles.HrManager, Permissions.DefaultRolePermissions[Permissions.Roles.HrManager]),
            (Permissions.Roles.PayrollManager, Permissions.DefaultRolePermissions[Permissions.Roles.PayrollManager]),
            (Permissions.Roles.DepartmentManager, Permissions.DefaultRolePermissions[Permissions.Roles.DepartmentManager]),
            (Permissions.Roles.Employee, Permissions.DefaultRolePermissions[Permissions.Roles.Employee])
        ];

        foreach (var (roleName, codes) in definitions)
        {
            var role = new Role { OrganizationId = org.Id, Name = roleName, IsSystem = true };
            foreach (var code in codes)
                if (byCode.TryGetValue(code, out var perm))
                    role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
            db.Roles.Add(role);

            if (roleName == Permissions.Roles.Owner && ownerUserId is { } uid)
                db.UserRoles.Add(new UserRole { UserId = uid, RoleId = role.Id });
        }
    }

    public static async Task EnsurePermissionCatalogAsync(IAppDbContext db, CancellationToken ct = default)
    {
        var existing = (await db.Permissions.ToListAsync(ct)).Select(p => p.Code).ToHashSet();
        foreach (var code in Permissions.All)
        {
            if (existing.Contains(code)) continue;
            db.Permissions.Add(new Permission
            {
                Code = code,
                Name = code[(code.IndexOf('.') + 1)..].Replace(".", " "),
                Category = Permissions.Category(code)
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
