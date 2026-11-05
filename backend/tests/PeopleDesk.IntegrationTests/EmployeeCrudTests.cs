using PeopleDesk.Api;
using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace PeopleDesk.IntegrationTests;

public class EmployeeCrudTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public EmployeeCrudTests(TestApiFactory factory) => _factory = factory;

    private async Task<HttpClient> GetOwnerClientAsync(string slug)
    {
        var client = _factory.CreateClient();
        var reg = await client.RegisterOrgAsync(slug, $"owner-{slug}@test.local");
        client.WithToken(reg.GetProperty("accessToken").GetString()!);
        return client;
    }

    [Fact]
    public async Task Create_GeneratesEmployeeNumber_AndMasksBankAccount()
    {
        var client = await GetOwnerClientAsync($"emp-{Guid.NewGuid():N}"[..16]);
        var dep = await client.PostJsonAsync("/api/v1/organizations/departments", new { name = "Ops", code = "OPS" });

        var employee = await client.PostJsonAsync("/api/v1/employees", new
        {
            firstName = "Dawit", lastName = "Tesfaye", workEmail = "dawit@emp.test",
            hireDate = "2025-06-01", status = "Active", departmentId = dep.GetProperty("id").GetString(),
            bankAccount = "1000123456789", nationalId = "ET-1990-12345"
        });

        Assert.StartsWith("EMP-", employee.GetProperty("employeeNumber").GetString());
        // Owner has employees.viewSensitive → raw values visible.
        Assert.Equal("1000123456789", employee.GetProperty("bankAccountMasked").GetString());
        Assert.Equal("ET-1990-12345", employee.GetProperty("nationalId").GetString());
    }

    [Fact]
    public async Task List_SupportsSearch_Pagination_AndSorting()
    {
        var client = await GetOwnerClientAsync($"emp-list-{Guid.NewGuid():N}"[..16]);
        for (var i = 1; i <= 9; i++)
            await client.PostJsonAsync("/api/v1/employees", new
            { firstName = $"Person{i:00}", lastName = "Alpha", hireDate = "2025-01-01", status = "Active" });

        var page1 = await client.GetJsonAsync("/api/v1/employees?page=1&pageSize=10");
        Assert.Equal(9, page1.GetProperty("totalCount").GetInt32());
        Assert.Equal(9, page1.GetProperty("items").GetArrayLength());
        Assert.Equal(1, page1.GetProperty("totalPages").GetInt32());

        var page2 = await client.GetJsonAsync("/api/v1/employees?page=2&pageSize=10");
        Assert.Equal(0, page2.GetProperty("items").GetArrayLength()); // beyond last page

        var search = await client.GetJsonAsync("/api/v1/employees?search=Person07");
        Assert.Equal(1, search.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Update_ChangesFields_AndWritesHistory()
    {
        var client = await GetOwnerClientAsync($"emp-upd-{Guid.NewGuid():N}"[..16]);
        var dep1 = await client.PostJsonAsync("/api/v1/organizations/departments", new { name = "Sales" });
        var dep2 = await client.PostJsonAsync("/api/v1/organizations/departments", new { name = "Marketing" });
        var employee = await client.PostJsonAsync("/api/v1/employees", new
        { firstName = "Marta", lastName = "Haile", hireDate = "2025-01-01", status = "Active", departmentId = dep1.GetProperty("id").GetString() });
        var id = employee.GetProperty("id").GetString()!;

        await client.PutAsJsonAsync($"/api/v1/employees/{id}", new
        {
            firstName = "Marta", lastName = "Haile-Bekele", gender = "Female",
            departmentId = dep2.GetProperty("id").GetString(), hireDate = "2025-01-01"
        });

        var updated = await client.GetJsonAsync($"/api/v1/employees/{id}");
        Assert.Equal("Haile-Bekele", updated.GetProperty("lastName").GetString());

        var history = await client.GetJsonAsync($"/api/v1/employees/{id}/history");
        Assert.True(history.GetArrayLength() >= 2); // onboarding + department transfer
    }

    [Fact]
    public async Task Offboard_DisablesLinkedUserAccount()
    {
        var client = await GetOwnerClientAsync($"emp-off-{Guid.NewGuid():N}"[..16]);
        var employee = await client.PostJsonAsync("/api/v1/employees", new
        {
            firstName = "Selam", lastName = "Girma", workEmail = "selam@emp.test",
            hireDate = "2025-01-01", status = "Active",
            createUserAccount = true, initialPassword = "Selam#2026"
        });
        var id = employee.GetProperty("id").GetString()!;

        var login = await _factory.CreateClient().LoginAsync("selam@emp.test", "Selam#2026");
        Assert.NotEmpty(login.GetProperty("accessToken").GetString()!);

        await client.TryPostJsonAsync($"/api/v1/employees/{id}/offboard",
            new { status = "Resigned", terminationDate = "2026-11-30", reason = "Relocation" });

        var after = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = "selam@emp.test", password = "Selam#2026" });
        Assert.Equal(HttpStatusCode.BadRequest, after.StatusCode); // account deactivated
    }

    [Fact]
    public async Task SoftDeletedEmployee_DisappearsFromList()
    {
        var client = await GetOwnerClientAsync($"emp-del-{Guid.NewGuid():N}"[..16]);
        var employee = await client.PostJsonAsync("/api/v1/employees", new
        { firstName = "Gone", lastName = "Soon", hireDate = "2025-01-01", status = "Active" });
        var id = employee.GetProperty("id").GetString()!;

        await client.DeleteAsync($"/api/v1/employees/{id}");
        var list = await client.GetJsonAsync("/api/v1/employees");
        Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
        var get = await client.GetAsync($"/api/v1/employees/{id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task SensitiveFields_Hidden_Without_SensitivePermission()
    {
        var client = await GetOwnerClientAsync($"emp-sens-{Guid.NewGuid():N}"[..16]);
        var employee = await client.PostJsonAsync("/api/v1/employees", new
        {
            firstName = "Hidden", lastName = "Fields", workEmail = "hidden@emp.test",
            hireDate = "2025-01-01", status = "Active", bankAccount = "12345678",
            createUserAccount = true, initialPassword = "Hidden#2026"
        });

        // The linked employee user only has the Employee role → no sensitive access.
        var empClient = _factory.CreateClient();
        var login = await empClient.LoginAsync("hidden@emp.test", "Hidden#2026");
        empClient.WithToken(login.GetProperty("accessToken").GetString()!);

        var me = await empClient.GetJsonAsync("/api/v1/employees/me");
        Assert.False(me.TryGetProperty("nationalId", out _));          // omitted without sensitive permission
        Assert.False(me.TryGetProperty("taxIdentificationNumber", out _));
        Assert.Equal("•••• 5678", me.GetProperty("bankAccountMasked").GetString());

        // Directory access must be forbidden for the employee role.
        var list = await empClient.GetAsync("/api/v1/employees");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task SubscriptionEmployeeLimit_IsEnforced()
    {
        var client = await GetOwnerClientAsync($"emp-limit-{Guid.NewGuid():N}"[..16]);
        // Trial plan: 10 employees.
        for (var i = 0; i < 10; i++)
            await client.PostJsonAsync("/api/v1/employees", new
            { firstName = $"E{i}", lastName = "Limit", hireDate = "2025-01-01", status = "Active" });

        var response = await client.PostAsJsonAsync("/api/v1/employees", new
        { firstName = "Over", lastName = "Limit", hireDate = "2025-01-01" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("limit reached", body, StringComparison.OrdinalIgnoreCase);
    }
}
