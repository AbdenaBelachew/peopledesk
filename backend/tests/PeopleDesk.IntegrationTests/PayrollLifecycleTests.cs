using PeopleDesk.Api;
using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace PeopleDesk.IntegrationTests;

/// <summary>
/// Full payroll lifecycle with exact Ethiopia (Proc. 1395/2025) math:
/// basic 12,000 + transport 1,500 = 13,500 gross → pension 945 → taxable 12,555 → tax 2,416.50 → net 10,138.50.
/// </summary>
public class PayrollLifecycleTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public PayrollLifecycleTests(TestApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Owner, string EmployeeId, string EmployeeEmail)> SetupAsync(string tag)
    {
        var owner = _factory.CreateClient();
        var slug = $"pr-{tag}-{Guid.NewGuid():N}"[..18];
        var reg = await owner.RegisterOrgAsync(slug, $"owner-{slug}@test.local");
        owner.WithToken(reg.GetProperty("accessToken").GetString()!);

        var employee = await owner.PostJsonAsync("/api/v1/employees", new
        {
            firstName = "Payroll", lastName = "Pro", workEmail = $"pro-{tag}@test.local",
            hireDate = "2025-01-01", status = "Active",
            createUserAccount = true, initialPassword = "Pro#2026"
        });
        var employeeId = employee.GetProperty("id").GetString()!;

        await owner.PostJsonAsync($"/api/v1/employees/{employeeId}/salary-structures", new
        {
            effectiveFrom = "2025-01-01",
            currency = "ETB",
            components = new object[]
            {
                new { kind = "Earning", code = "BASIC", label = "Basic Salary", type = "Basic", calculationType = "FixedAmount", amount = 12000m, isTaxable = true },
                new { kind = "Earning", code = "TRANSPORT", label = "Transport Allowance", type = "Allowance", calculationType = "FixedAmount", amount = 1500m, isTaxable = true }
            }
        });

        return (owner, employeeId, $"pro-{tag}@test.local");
    }

    [Fact]
    public async Task Calculate_Approve_Finalize_ProducesExactNet_AndPayslips()
    {
        var (owner, employeeId, employeeEmail) = await SetupAsync("calc");

        var now = DateTime.UtcNow;
        var period = await owner.PostJsonAsync("/api/v1/payroll/periods", new { type = "Monthly", year = now.Year, month = now.Month });
        var run = await owner.PostJsonAsync("/api/v1/payroll/runs", new { payrollPeriodId = period.GetProperty("id").GetString() });
        var runId = run.GetProperty("id").GetString()!;

        var calculated = await owner.PostJsonAsync($"/api/v1/payroll/runs/{runId}/calculate", new { });
        Assert.Equal("Calculated", calculated.GetProperty("status").GetString());
        Assert.Equal(13500m, calculated.GetProperty("totalGross").GetDecimal());
        Assert.Equal(945m + 2416.50m, calculated.GetProperty("totalDeductions").GetDecimal());
        Assert.Equal(10138.50m, calculated.GetProperty("totalNet").GetDecimal());
        Assert.Equal(1, calculated.GetProperty("employeeCount").GetInt32());

        // Inspect item lines.
        var items = await owner.GetJsonAsync($"/api/v1/payroll/runs/{runId}/items");
        var item = items.GetProperty("items").EnumerateArray().First();
        Assert.Equal(12000m, item.GetProperty("basicSalary").GetDecimal());
        var tax = item.GetProperty("deductions").EnumerateArray().First(d => d.GetProperty("code").GetString() == "INCOME_TAX");
        Assert.Equal(2416.50m, tax.GetProperty("amount").GetDecimal());
        var pension = item.GetProperty("deductions").EnumerateArray().First(d => d.GetProperty("code").GetString() == "PENSION");
        Assert.Equal(945m, pension.GetProperty("amount").GetDecimal());
        Assert.Equal(10138.50m, item.GetProperty("netSalary").GetDecimal());

        // Approve → finalize.
        var approved = await owner.PostJsonAsync($"/api/v1/payroll/runs/{runId}/approve", new { });
        Assert.Equal("Approved", approved.GetProperty("status").GetString());

        var finalized = await owner.PostJsonAsync($"/api/v1/payroll/runs/{runId}/finalize", new { });
        Assert.Equal("Finalized", finalized.GetProperty("status").GetString());

        // Payslip generated + downloadable by owner.
        var payslips = await owner.GetJsonAsync("/api/v1/payslips");
        Assert.Equal(1, payslips.GetProperty("totalCount").GetInt32());
        var payslipId = payslips.GetProperty("items").EnumerateArray().First().GetProperty("id").GetString()!;
        var pdf = await owner.GetAsync($"/api/v1/payslips/{payslipId}/download");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes[..4]));
    }

    [Fact]
    public async Task StateMachine_Guards_InvalidTransitions()
    {
        var (owner, _, _) = await SetupAsync("guard");
        var now = DateTime.UtcNow;
        var period = await owner.PostJsonAsync("/api/v1/payroll/periods", new { type = "Monthly", year = now.Year, month = now.Month });
        var run = await owner.PostJsonAsync("/api/v1/payroll/runs", new { payrollPeriodId = period.GetProperty("id").GetString() });
        var runId = run.GetProperty("id").GetString()!;

        // Draft → approve forbidden.
        var earlyApprove = await owner.PostAsJsonAsync($"/api/v1/payroll/runs/{runId}/approve", new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, earlyApprove.StatusCode);

        await owner.PostJsonAsync($"/api/v1/payroll/runs/{runId}/calculate", new { });

        // Second run for same period forbidden.
        var secondRun = await owner.PostAsJsonAsync("/api/v1/payroll/runs", new { payrollPeriodId = period.GetProperty("id").GetString() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondRun.StatusCode);

        await owner.PostJsonAsync($"/api/v1/payroll/runs/{runId}/approve", new { });

        // Recalculation after approval forbidden.
        var recalc = await owner.PostAsJsonAsync($"/api/v1/payroll/runs/{runId}/calculate", new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, recalc.StatusCode);
    }

    [Fact]
    public async Task Reverse_FinalizedRun_UnlocksPeriod_WithAuditNote()
    {
        var (owner, _, _) = await SetupAsync("rev");
        var now = DateTime.UtcNow;
        var period = await owner.PostJsonAsync("/api/v1/payroll/periods", new { type = "Monthly", year = now.Year, month = now.Month });
        var run = await owner.PostJsonAsync("/api/v1/payroll/runs", new { payrollPeriodId = period.GetProperty("id").GetString() });
        var runId = run.GetProperty("id").GetString()!;
        await owner.PostJsonAsync($"/api/v1/payroll/runs/{runId}/calculate", new { });
        await owner.PostJsonAsync($"/api/v1/payroll/runs/{runId}/approve", new { });
        await owner.PostJsonAsync($"/api/v1/payroll/runs/{runId}/finalize", new { });

        var noNote = await owner.PostAsJsonAsync($"/api/v1/payroll/runs/{runId}/reverse", new { note = "" });
        Assert.Equal(HttpStatusCode.BadRequest, noNote.StatusCode);

        var reversed = await owner.PostJsonAsync($"/api/v1/payroll/runs/{runId}/reverse", new { note = "Duplicate bonus included" });
        Assert.Equal("Reversed", reversed.GetProperty("status").GetString());

        // Period reopens → a fresh run can be created.
        var periods = await owner.GetJsonAsync("/api/v1/payroll/periods");
        var reopened = periods.EnumerateArray().First(p => p.GetProperty("id").GetString() == period.GetProperty("id").GetString());
        Assert.False(reopened.GetProperty("closed").GetBoolean());
    }

    [Fact]
    public async Task MissingSalaryStructure_SkipsEmployee_WithReason()
    {
        var owner = _factory.CreateClient();
        var slug = $"pr-skip-{Guid.NewGuid():N}"[..18];
        var reg = await owner.RegisterOrgAsync(slug, $"skip-{slug}@test.local");
        owner.WithToken(reg.GetProperty("accessToken").GetString()!);

        await owner.PostJsonAsync("/api/v1/employees", new
        { firstName = "No", lastName = "Salary", hireDate = "2025-01-01", status = "Active" });

        var now = DateTime.UtcNow;
        var period = await owner.PostJsonAsync("/api/v1/payroll/periods", new { type = "Monthly", year = now.Year, month = now.Month });
        var run = await owner.PostJsonAsync("/api/v1/payroll/runs", new { payrollPeriodId = period.GetProperty("id").GetString() });
        var calculated = await owner.PostJsonAsync($"/api/v1/payroll/runs/{run.GetProperty("id").GetString()!}/calculate", new { });

        Assert.Equal(0, calculated.GetProperty("employeeCount").GetInt32());
        Assert.Single(calculated.GetProperty("skippedEmployees").EnumerateArray());
    }

    [Fact]
    public async Task Employee_CannotAccess_OtherEmployeesPayslips()
    {
        var (owner, employeeId, employeeEmail) = await SetupAsync("acl");

        // Second employee with own account.
        var other = await owner.PostJsonAsync("/api/v1/employees", new
        {
            firstName = "Other", lastName = "Worker", workEmail = "other-acl@test.local",
            hireDate = "2025-01-01", status = "Active", createUserAccount = true, initialPassword = "Other#2026"
        });

        var now = DateTime.UtcNow;
        var period = await owner.PostJsonAsync("/api/v1/payroll/periods", new { type = "Monthly", year = now.Year, month = now.Month });
        var run = await owner.PostJsonAsync("/api/v1/payroll/runs", new { payrollPeriodId = period.GetProperty("id").GetString() });
        var runId = run.GetProperty("id").GetString()!;
        await owner.PostJsonAsync($"/api/v1/payroll/runs/{runId}/calculate", new { });
        await owner.PostJsonAsync($"/api/v1/payroll/runs/{runId}/approve", new { });
        await owner.PostJsonAsync($"/api/v1/payroll/runs/{runId}/finalize", new { });

        var otherClient = _factory.CreateClient();
        var login = await otherClient.LoginAsync("other-acl@test.local", "Other#2026");
        otherClient.WithToken(login.GetProperty("accessToken").GetString()!);

        // Second employee has no salary structure → no payslip; first employee's payslip is forbidden.
        var firstPayslip = (await owner.GetJsonAsync("/api/v1/payslips"))
            .GetProperty("items").EnumerateArray().First().GetProperty("id").GetString()!;

        var forbidden = await otherClient.GetAsync($"/api/v1/payslips/{firstPayslip}/download");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // Own list is empty.
        var own = await otherClient.GetJsonAsync("/api/v1/payslips");
        Assert.Equal(0, own.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Adjustments_AppliedOnRecalculation()
    {
        var (owner, employeeId, _) = await SetupAsync("adj");
        var now = DateTime.UtcNow;
        var period = await owner.PostJsonAsync("/api/v1/payroll/periods", new { type = "Monthly", year = now.Year, month = now.Month });
        var run = await owner.PostJsonAsync("/api/v1/payroll/runs", new { payrollPeriodId = period.GetProperty("id").GetString() });
        var runId = run.GetProperty("id").GetString()!;

        await owner.PostJsonAsync($"/api/v1/payroll/runs/{runId}/calculate", new { });

        // +5,000 taxable bonus → gross 18,500, pension 1,295, taxable 17,205,
        // tax 17,205 × 35% − 2,050 = 3,971.75, net = 18,500 − 5,266.75 = 13,233.25.
        await owner.TryPostJsonAsync($"/api/v1/payroll/runs/{runId}/adjustments",
            new { employeeId, kind = "Earning", code = "BONUS", label = "Performance bonus", amount = 5000m });
        await owner.PostJsonAsync($"/api/v1/payroll/runs/{runId}/calculate", new { });

        var after = await owner.GetJsonAsync($"/api/v1/payroll/runs/{runId}");
        Assert.Equal(13233.25m, after.GetProperty("totalNet").GetDecimal());
    }
}
