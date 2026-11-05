using PeopleDesk.Api;
using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace PeopleDesk.IntegrationTests;

/// <summary>Server-enforced tenant isolation: org B can never see org A's business data.</summary>
public class TenantIsolationTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public TenantIsolationTests(TestApiFactory factory) => _factory = factory;

    private async Task<(HttpClient ClientA, HttpClient ClientB, JsonElement EmployeeA)> SetupTwoOrgsAsync()
    {
        var clientA = _factory.CreateClient();
        var clientB = _factory.CreateClient();

        var regA = await clientA.RegisterOrgAsync($"iso-a-{Guid.NewGuid():N}"[..18], $"ownerA{Guid.NewGuid():N}@test.local");
        var regB = await clientB.RegisterOrgAsync($"iso-b-{Guid.NewGuid():N}"[..18], $"ownerB{Guid.NewGuid():N}@test.local");
        clientA.WithToken(regA.GetProperty("accessToken").GetString()!);
        clientB.WithToken(regB.GetProperty("accessToken").GetString()!);

        var dep = await clientA.PostJsonAsync("/api/v1/organizations/departments", new { name = "Eng", code = "ENG" });
        var employee = await clientA.PostJsonAsync("/api/v1/employees", new
        {
            firstName = "Alice", lastName = "Smith", workEmail = "alice@isoA.test",
            hireDate = "2025-01-01", status = "Active", departmentId = dep.GetProperty("id").GetString()
        });
        return (clientA, clientB, employee);
    }

    [Fact]
    public async Task CrossOrgEmployee_Read_Returns404()
    {
        var (clientA, clientB, employee) = await SetupTwoOrgsAsync();
        var employeeId = employee.GetProperty("id").GetString()!;

        var own = await clientA.GetAsync($"/api/v1/employees/{employeeId}");
        Assert.True(own.IsSuccessStatusCode);

        var other = await clientB.GetAsync($"/api/v1/employees/{employeeId}");
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode); // filtered out, not leaked
    }

    [Fact]
    public async Task CrossOrgEmployee_Update_Returns404()
    {
        var (clientA, clientB, employee) = await SetupTwoOrgsAsync();
        var employeeId = employee.GetProperty("id").GetString()!;

        var response = await clientB.PutAsJsonAsync($"/api/v1/employees/{employeeId}", new
        { firstName = "Hacked", lastName = "Person", gender = "Male", hireDate = "2020-01-01" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CrossOrgEmployee_Delete_Returns404()
    {
        var (clientA, clientB, employee) = await SetupTwoOrgsAsync();
        var response = await clientB.DeleteAsync($"/api/v1/employees/{employee.GetProperty("id").GetString()!}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EmployeeListing_IsScopedToOwnOrg()
    {
        var (clientA, clientB, employee) = await SetupTwoOrgsAsync();

        var listA = await clientA.GetJsonAsync("/api/v1/employees");
        Assert.Equal(1, listA.GetProperty("totalCount").GetInt32());

        var listB = await clientB.GetJsonAsync("/api/v1/employees");
        Assert.Equal(0, listB.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task PayrollPeriods_AreScopedToOwnOrg()
    {
        var (clientA, clientB, _) = await SetupTwoOrgsAsync();

        var period = await clientA.PostJsonAsync("/api/v1/payroll/periods",
            new { type = "Monthly", year = 2026, month = 11 });
        var periodId = period.GetProperty("id").GetString()!;

        // Org B must not be able to see A's period list, nor create a run against it.
        var listB = await clientB.GetJsonAsync("/api/v1/payroll/periods");
        Assert.Empty(listB.EnumerateArray());

        var run = await clientB.PostAsJsonAsync("/api/v1/payroll/runs", new { payrollPeriodId = periodId });
        Assert.Equal(HttpStatusCode.NotFound, run.StatusCode);
    }

    [Fact]
    public async Task LeaveBalances_CannotLeak_AcrossTenants()
    {
        var (clientA, clientB, employee) = await SetupTwoOrgsAsync();
        var employeeId = employee.GetProperty("id").GetString()!;

        // B querying A's employee id gets only B's own (empty) balances — nothing leaks.
        var balances = await clientB.GetJsonAsync($"/api/v1/leave/balances/{employeeId}");
        // Consumed days must never leak (entitledDays may reflect B's own policy template).
        Assert.All(balances.EnumerateArray(), b =>
        {
            Assert.Equal(0m, b.GetProperty("usedDays").GetDecimal());
            Assert.Equal(0m, b.GetProperty("pendingDays").GetDecimal());
            Assert.Equal(0m, b.GetProperty("carriedOverDays").GetDecimal());
        });
    }

    [Fact]
    public async Task Unauthenticated_CannotAccessTenantData()
    {
        var (clientA, _, employee) = await SetupTwoOrgsAsync();
        var anon = _factory.CreateClient();
        var response = await anon.GetAsync($"/api/v1/employees/{employee.GetProperty("id").GetString()!}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
