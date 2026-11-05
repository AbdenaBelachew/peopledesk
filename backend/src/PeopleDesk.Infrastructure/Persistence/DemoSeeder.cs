using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PeopleDesk.Application.Common;
using PeopleDesk.Domain.Organizations;
using PeopleDesk.Application.Organizations;
using PeopleDesk.Domain.Attendance;
using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.Employees;
using PeopleDesk.Domain.Identity;
using PeopleDesk.Domain.Leave;
using PeopleDesk.Domain.Payroll;

namespace PeopleDesk.Infrastructure.Persistence;

/// <summary>
/// DEVELOPMENT-ONLY demo data (SEED_DEMO_DATA=true): a tenant "Acme Trading PLC" with users for
/// every role, employees, salary structures, leave history and a finalized payroll run with payslips.
/// Credentials are printed to the log and documented in README — never enable in production.
/// </summary>
public class DemoSeeder(
    IAppDbContext db,
    IPasswordHasher hasher,
    IConfiguration config,
    IDateTime clock,
    ILogger<DemoSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var enabled = config.GetValue<bool>("Seed:DemoData")
            || string.Equals(config["SEED_DEMO_DATA"], "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(config["Seed:DemoData"], "true", StringComparison.OrdinalIgnoreCase);
        if (!enabled) return;
        if (await db.Organizations.IgnoreQueryFilters().AnyAsync(o => o.Slug == "acme-demo", ct)) return;

        logger.LogWarning("SEEDING DEMO DATA (development only)…");

        var org = new Organization
        {
            Name = "Acme Trading PLC",
            Slug = "acme-demo",
            Email = "hr@acme-demo.test",
            Country = "Ethiopia",
            City = "Addis Ababa",
            AddressLine = "Bole Road, Acme Tower",
            TimeZone = "Africa/Addis_Ababa",
            Currency = "ETB",
            BrandColor = "#2563eb"
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Organizations.Add(org);
        await OrganizationDefaults.CreateDefaultStructureAsync(db, org, "Ethiopia", ct);
        await OrganizationDefaults.CreateSystemRolesAsync(db, org, null, ct);
        await db.SaveChangesAsync(ct); // persist structure + roles before dependent lookups

        var trialPlan = await db.SubscriptionPlans.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Code == "growth", ct);
        db.Subscriptions.Add(new Subscription
        {
            OrganizationId = org.Id, PlanId = trialPlan!.Id,
            Status = SubscriptionStatus.Active, StartedAtUtc = clock.UtcNow,
            CurrentPeriodEndUtc = clock.UtcNow.AddMonths(1)
        });

        var roles = await db.Roles.IgnoreQueryFilters().Where(r => r.OrganizationId == org.Id)
            .ToDictionaryAsync(r => r.Name, ct);
        var schedule = await db.WorkSchedules.IgnoreQueryFilters()
            .FirstAsync(s => s.OrganizationId == org.Id && s.IsDefault, ct);

        // Demo departments (the default provisioning only seeds employment types/schedules/leave).
        foreach (var name in new[] { "Engineering", "Sales", "Finance", "Operations" })
            db.Departments.Add(new Department { OrganizationId = org.Id, Name = name });
        db.Branches.Add(new Branch { OrganizationId = org.Id, Name = "Addis HQ", City = "Addis Ababa", Country = "Ethiopia" });
        await db.SaveChangesAsync(ct);

        var departments = await db.Departments.IgnoreQueryFilters()
            .Where(d => d.OrganizationId == org.Id).ToDictionaryAsync(d => d.Name, ct);
        var branches = await db.Branches.IgnoreQueryFilters()
            .Where(b => b.OrganizationId == org.Id).ToDictionaryAsync(b => b.Name, ct);
        var leaveTypes = await db.LeaveTypes.IgnoreQueryFilters()
            .Where(t => t.OrganizationId == org.Id).ToDictionaryAsync(t => t.Code, ct);

        // ─── Employees + linked users ────────────────────────────────────────────
        (string First, string Last, string Role, decimal Basic, string Dept)[] people =
        [
            ("Meron", "Assefa", Permissions.Roles.Owner, 42000m, "Engineering"),
            ("Hanna", "Girma", Permissions.Roles.HrManager, 26000m, "Engineering"),
            ("Yonas", "Bekele", Permissions.Roles.PayrollManager, 24000m, "Engineering"),
            ("Kalkidan", "Tadesse", Permissions.Roles.DepartmentManager, 22000m, "Engineering"),
            ("Samuel", "Mengistu", Permissions.Roles.Employee, 14000m, "Engineering"),
            ("Liya", "Haile", Permissions.Roles.Employee, 12500m, "Engineering"),
            ("Abel", "Worku", Permissions.Roles.Employee, 11000m, "Engineering")
        ];

        var employees = new List<Employee>();
        var emailByIndex = new[] { "owner", "hr", "payroll", "manager", "employee", "employee2", "employee3" };
        for (var i = 0; i < people.Length; i++)
        {
            var (first, last, role, basic, dept) = people[i];
            var email = $"{emailByIndex[i]}@acme-demo.test";

            var user = new User
            {
                OrganizationId = org.Id,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                PasswordHash = hasher.Hash("Demo#2026"),
                FirstName = first,
                LastName = last,
                EmailVerifiedAtUtc = clock.UtcNow
            };
            db.Users.Add(user);
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roles[role].Id, User = user, Role = roles[role] });

            var employee = new Employee
            {
                OrganizationId = org.Id,
                EmployeeNumber = $"EMP-{clock.TodayUtc:yyyy}-{(i + 1):D4}",
                UserId = user.Id,
                FirstName = first,
                LastName = last,
                Gender = i % 2 == 0 ? Gender.Male : Gender.Female,
                DateOfBirth = new DateOnly(1990 + i, (i % 12) + 1, (i % 27) + 1),
                WorkEmail = email,
                Phone = $"+25191100000{i}",
                DepartmentId = departments[dept].Id,
                BranchId = branches["Addis HQ"].Id,
                EmploymentTypeId = (await db.EmploymentTypes.IgnoreQueryFilters()
                    .Where(t => t.OrganizationId == org.Id && t.Name == "Full-Time").FirstAsync(ct)).Id,
                ScheduleId = schedule.Id,
                HireDate = new DateOnly(2023 + (i % 3), ((i * 3) % 12) + 1, 1),
                Status = EmploymentStatus.Active,
                PaymentMethod = SalaryPaymentMethod.Bank,
                BankName = "Commercial Bank of Ethiopia",
                BankAccount = $"1000{i:D10}",
                TaxIdentificationNumber = $"TIN-{100000+i}"
            };
            db.Employees.Add(employee);
            employees.Add(employee);

            var structure = new SalaryStructure
            {
                OrganizationId = org.Id,
                EmployeeId = employee.Id,
                EffectiveFrom = employee.HireDate,
                Currency = "ETB"
            };
            structure.Components.Add(new SalaryComponent { OrganizationId = org.Id, SalaryStructureId = structure.Id, Kind = PayComponentKind.Earning, Code = "BASIC", Label = "Basic Salary", Type = SalaryComponentType.Basic, Amount = basic });
            structure.Components.Add(new SalaryComponent { OrganizationId = org.Id, SalaryStructureId = structure.Id, Kind = PayComponentKind.Earning, Code = "TRANSPORT", Label = "Transport Allowance", Type = SalaryComponentType.Allowance, CalculationType = SalaryCalculationType.PercentOfBasic, Amount = 10m, IsTaxable = true });
            structure.Components.Add(new SalaryComponent { OrganizationId = org.Id, SalaryStructureId = structure.Id, Kind = PayComponentKind.Earning, Code = "PHONE", Label = "Phone Allowance", Type = SalaryComponentType.Allowance, Amount = 400m, IsTaxable = false });
            db.SalaryStructures.Add(structure);

            // Grant annual leave balances + a bit of history.
            var annual = leaveTypes["ANNUAL"];
            var balance = new LeaveBalance
            {
                OrganizationId = org.Id, EmployeeId = employee.Id, LeaveTypeId = annual.Id,
                Year = clock.TodayUtc.Year, EntitledDays = 20, UsedDays = i % 5
            };
            db.LeaveBalances.Add(balance);
            db.LeaveTransactions.Add(new LeaveTransaction
            {
                OrganizationId = org.Id, EmployeeId = employee.Id, LeaveTypeId = annual.Id,
                Year = clock.TodayUtc.Year, Type = LeaveTransactionType.Grant,
                Days = 20, BalanceAfter = balance.Available, Description = $"Annual grant {clock.TodayUtc.Year}"
            });

            // Attendance: last 20 working days, mostly present with some late.
            var localToday = DateOnly.FromDateTime(DateTime.UtcNow);
            for (var d = 1; d <= 24; d++)
            {
                var date = localToday.AddDays(-d);
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                var lateMin = (d % 7 == 0) ? 15 + d : 0;
                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    OrganizationId = org.Id,
                    EmployeeId = employee.Id,
                    WorkDate = date,
                    ClockInAtUtc = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(8, lateMin > 0 ? 30 + d % 20 : 55)), DateTimeKind.Utc),
                    ClockOutAtUtc = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(17, 20)), DateTimeKind.Utc),
                    BreakMinutes = 60,
                    WorkedMinutes = 445,
                    LateMinutes = lateMin,
                    OvertimeMinutes = d % 9 == 0 ? 45 : 0,
                    Status = lateMin > 0 ? AttendanceStatus.Late : AttendanceStatus.Present,
                    Source = AttendanceSource.SelfClock
                });
            }
        }

        await db.SaveChangesAsync(ct);
        var structures = await db.SalaryStructures.IgnoreQueryFilters()
            .Include(s => s.Components)
            .Where(s => s.OrganizationId == org.Id).ToListAsync(ct);

        // ─── A finalized payroll run for last month ─────────────────────────────
        var lastMonth = clock.TodayUtc.AddMonths(-1);
        var period = new PayrollPeriod
        {
            OrganizationId = org.Id,
            Name = $"{lastMonth:yyyy-MM}",
            Type = PayrollPeriodType.Monthly,
            Year = lastMonth.Year,
            Month = lastMonth.Month,
            StartDate = new DateOnly(lastMonth.Year, lastMonth.Month, 1),
            EndDate = new DateOnly(lastMonth.Year, lastMonth.Month, DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month))
        };
        db.PayrollPeriods.Add(period);

        var ruleSet = await db.PayrollRuleSets.IgnoreQueryFilters()
            .Where(r => r.OrganizationId == org.Id && r.EffectiveFrom <= period.EndDate && (r.EffectiveTo == null || r.EffectiveTo >= period.StartDate))
            .OrderByDescending(r => r.EffectiveFrom).FirstAsync(ct);
        var brackets = await db.PayrollTaxBrackets.IgnoreQueryFilters()
            .Where(b => b.RuleSetId == ruleSet.Id).ToListAsync(ct);

        var run = new PayrollRun
        {
            OrganizationId = org.Id,
            PayrollPeriodId = period.Id,
            Status = PayrollRunStatus.Finalized,
            CalculatedAtUtc = clock.UtcNow.AddDays(-3),
            CalculatedBy = "Yonas Bekele",
            ApprovedBy = "Meron Assefa",
            FinalizedBy = "Meron Assefa",
            FinalizedAtUtc = clock.UtcNow.AddDays(-2)
        };
        db.PayrollRuns.Add(run);

        decimal totalGross = 0, totalDed = 0, totalNet = 0;
        var slipSeq = 0;
        foreach (var employee in employees)
        {
            var structure = structures.First(s => s.EmployeeId == employee.Id);
            var basic = structure.Components.First(c => c.Code == "BASIC").Amount;
            var transport = structure.Components.First(c => c.Code == "TRANSPORT").Amount * basic / 100m;
            var phone = structure.Components.First(c => c.Code == "PHONE").Amount;
            var gross = IncomeTaxCalculator.Round2(basic + transport + phone);
            var pension = IncomeTaxCalculator.Round2(gross * ruleSet.PensionEmployeeRate);
            var taxable = gross - pension;
            var tax = IncomeTaxCalculator.Compute(taxable,
                [.. brackets.Select(b => new IncomeTaxCalculator.Bracket(b.LowerBound, b.UpperBound, b.Rate, b.QuickDeduction))]);
            var deductions = IncomeTaxCalculator.Round2(pension + tax);
            var net = IncomeTaxCalculator.Round2(gross - deductions);

            var item = new PayrollItem
            {
                OrganizationId = org.Id, PayrollRunId = run.Id, EmployeeId = employee.Id,
                SalaryStructureId = structure.Id, Currency = "ETB",
                DaysWorked = 20, OvertimeHours = 2, UnpaidLeaveDays = 0,
                BasicSalary = basic, TotalEarnings = gross, TotalDeductions = deductions,
                GrossSalary = gross, NetSalary = net
            };
            db.PayrollItems.Add(item);
            db.PayrollEarnings.AddRange(
                new PayrollEarning { OrganizationId = org.Id, PayrollItemId = item.Id, Code = "BASIC", Label = "Basic Salary", Type = SalaryComponentType.Basic, Amount = basic },
                new PayrollEarning { OrganizationId = org.Id, PayrollItemId = item.Id, Code = "TRANSPORT", Label = "Transport Allowance", Type = SalaryComponentType.Allowance, Amount = IncomeTaxCalculator.Round2(transport) },
                new PayrollEarning { OrganizationId = org.Id, PayrollItemId = item.Id, Code = "PHONE", Label = "Phone Allowance", Type = SalaryComponentType.Allowance, Amount = phone });
            db.PayrollDeductions.AddRange(
                new PayrollDeduction { OrganizationId = org.Id, PayrollItemId = item.Id, Code = "PENSION", Label = "Pension (7% employee)", Type = SalaryComponentType.Pension, Amount = pension },
                new PayrollDeduction { OrganizationId = org.Id, PayrollItemId = item.Id, Code = "INCOME_TAX", Label = $"Income tax ({ruleSet.Version})", Type = SalaryComponentType.Tax, Amount = tax });

            totalGross += gross; totalDed += deductions; totalNet += net;
            slipSeq++;
            db.Payslips.Add(new Payslip
            {
                OrganizationId = org.Id,
                PayrollRunId = run.Id,
                PayrollPeriodId = period.Id,
                EmployeeId = employee.Id,
                PayrollItemId = item.Id,
                PayslipNumber = $"PS-{period.Name}-EMP-{lastMonth:yyyy}-{slipSeq:D4}",
                PeriodName = period.Name,
                Currency = "ETB",
                GrossPay = gross, TotalDeductions = deductions, NetPay = net,
                IssuedAtUtc = clock.UtcNow.AddDays(-2),
                PublishedAtUtc = clock.UtcNow.AddDays(-2),
                PublishedBy = "Meron Assefa",
                StorageKey = "demo/no-payslip-file.pdf",
                FileSizeBytes = 0
            });
        }

        run.TotalGross = IncomeTaxCalculator.Round2(totalGross);
        run.TotalDeductions = IncomeTaxCalculator.Round2(totalDed);
        run.TotalNet = IncomeTaxCalculator.Round2(totalNet);
        run.EmployeeCount = employees.Count;

        db.Announcements.Add(new Announcement
        {
            OrganizationId = org.Id,
            Title = "Welcome to PeopleDesk",
            Body = "Your HR workspace is live. Explore employees, attendance, leave and payroll from the sidebar.",
            PublishedAtUtc = clock.UtcNow
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogWarning("""
            DEMO DATA SEEDED — Organization: Acme Trading PLC (slug: acme-demo)
              owner@acme-demo.test   / Demo#2026  (Organization Owner)
              hr@acme-demo.test      / Demo#2026  (HR Manager)
              payroll@acme-demo.test / Demo#2026  (Payroll Manager)
              manager@acme-demo.test / Demo#2026  (Department Manager)
              employee@acme-demo.test/ Demo#2026  (Employee)
            NEVER enable demo seeding in production.
            """);
    }
}
