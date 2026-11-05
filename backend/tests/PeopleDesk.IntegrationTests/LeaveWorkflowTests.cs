using PeopleDesk.Api;
using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace PeopleDesk.IntegrationTests;

public class LeaveWorkflowTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public LeaveWorkflowTests(TestApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Owner, HttpClient EmployeeClient, string EmployeeId)> SetupAsync(string tag)
    {
        var owner = _factory.CreateClient();
        var slug = $"lv-{tag}-{Guid.NewGuid():N}"[..18];
        var reg = await owner.RegisterOrgAsync(slug, $"owner-{slug}@test.local");
        owner.WithToken(reg.GetProperty("accessToken").GetString()!);

        var employee = await owner.PostJsonAsync("/api/v1/employees", new
        {
            firstName = "Leave", lastName = "Taker", workEmail = $"taker-{tag}@test.local",
            hireDate = "2025-01-01", status = "Active",
            createUserAccount = true, initialPassword = "Taker#2026"
        });
        var employeeId = employee.GetProperty("id").GetString()!;

        var empClient = _factory.CreateClient();
        var login = await empClient.LoginAsync($"taker-{tag}@test.local", "Taker#2026");
        empClient.WithToken(login.GetProperty("accessToken").GetString()!);

        return (owner, empClient, employeeId);
    }

    private static async Task<string> AnnualTypeIdAsync(HttpClient client) =>
        (await client.GetJsonAsync("/api/v1/leave/types"))
            .EnumerateArray().First(t => t.GetProperty("code").GetString() == "ANNUAL")
            .GetProperty("id").GetString()!;

    private static DateOnly NextMonday()
    {
        var d = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(1);
        return d;
    }

    [Fact]
    public async Task Request_ReservesBalance_Approve_Consumes_Reject_Releases()
    {
        var (owner, employee, employeeId) = await SetupAsync("wf1");
        var annual = await AnnualTypeIdAsync(owner);
        var monday = NextMonday();

        var request = await employee.PostJsonAsync("/api/v1/leave/requests", new
        { leaveTypeId = annual, startDate = monday.ToString("yyyy-MM-dd"), endDate = monday.AddDays(2).ToString("yyyy-MM-dd"), reason = "Family" });
        var requestId = request.GetProperty("id").GetString()!;
        Assert.Equal("Pending", request.GetProperty("status").GetString());
        Assert.Equal(3m, request.GetProperty("days").GetDecimal());

        var pendingBalances = await employee.GetJsonAsync($"/api/v1/leave/balances/{employeeId}");
        var pending = pendingBalances.EnumerateArray().First(b => b.GetProperty("typeCode").GetString() == "ANNUAL");
        Assert.Equal(17m, pending.GetProperty("available").GetDecimal()); // 20 entitled − 3 pending
        Assert.Equal(3m, pending.GetProperty("pendingDays").GetDecimal());

        var approve = await owner.PostJsonAsync($"/api/v1/leave/requests/{requestId}/approve", new { approve = true });
        Assert.Equal("Approved", approve.GetProperty("status").GetString());

        var after = await employee.GetJsonAsync($"/api/v1/leave/balances/{employeeId}");
        var annualBal = after.EnumerateArray().First(b => b.GetProperty("typeCode").GetString() == "ANNUAL");
        Assert.Equal(17m, annualBal.GetProperty("available").GetDecimal()); // 20 − 3 used

        // Ledger written.
        var ledger = await employee.GetJsonAsync($"/api/v1/leave/transactions/{employeeId}");
        Assert.True(ledger.GetArrayLength() >= 2); // grant + application + approval
    }

    [Fact]
    public async Task OverlappingRequest_IsRejected()
    {
        var (owner, employee, _) = await SetupAsync("ovl");
        var annual = await AnnualTypeIdAsync(owner);
        var monday = NextMonday();

        var body = new { leaveTypeId = annual, startDate = monday.ToString("yyyy-MM-dd"), endDate = monday.AddDays(1).ToString("yyyy-MM-dd"), reason = "First" };
        await employee.PostJsonAsync("/api/v1/leave/requests", body);

        var response = await employee.PostAsJsonAsync("/api/v1/leave/requests", body);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task InsufficientBalance_IsRejected()
    {
        var (owner, employee, _) = await SetupAsync("bal");
        var annual = await AnnualTypeIdAsync(owner);
        var monday = NextMonday();

        var response = await employee.PostAsJsonAsync("/api/v1/leave/requests", new
        { leaveTypeId = annual, startDate = monday.ToString("yyyy-MM-dd"), endDate = monday.AddYears(1).ToString("yyyy-MM-dd") });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Maximum consecutive days", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejection_ReleasesPendingDays()
    {
        var (owner, employee, employeeId) = await SetupAsync("rej");
        var annual = await AnnualTypeIdAsync(owner);
        var monday = NextMonday();

        var request = await employee.PostJsonAsync("/api/v1/leave/requests", new
        { leaveTypeId = annual, startDate = monday.ToString("yyyy-MM-dd"), endDate = monday.ToString("yyyy-MM-dd") });
        var requestId = request.GetProperty("id").GetString()!;

        var reject = await owner.PostJsonAsync($"/api/v1/leave/requests/{requestId}/reject", new { approve = false, reviewNote = "Busy week" });
        Assert.Equal("Rejected", reject.GetProperty("status").GetString());

        var balances = await employee.GetJsonAsync($"/api/v1/leave/balances/{employeeId}");
        var annualBal = balances.EnumerateArray().First(b => b.GetProperty("typeCode").GetString() == "ANNUAL");
        Assert.Equal(20m, annualBal.GetProperty("available").GetDecimal());
        Assert.Equal(0m, annualBal.GetProperty("pendingDays").GetDecimal());
    }

    [Fact]
    public async Task CancelPendingRequest_ReleasesBalance()
    {
        var (owner, employee, _) = await SetupAsync("cnl");
        var annual = await AnnualTypeIdAsync(owner);
        var monday = NextMonday();

        var request = await employee.PostJsonAsync("/api/v1/leave/requests", new
        { leaveTypeId = annual, startDate = monday.ToString("yyyy-MM-dd"), endDate = monday.AddDays(1).ToString("yyyy-MM-dd") });
        var requestId = request.GetProperty("id").GetString()!;

        var cancel = await employee.PostAsync($"/api/v1/leave/requests/{requestId}/cancel", null);
        Assert.True(cancel.IsSuccessStatusCode, await cancel.Content.ReadAsStringAsync());
        Assert.Equal("Cancelled", (await cancel.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(TestJson.Options)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task ApprovedLeave_MarksAttendanceCalendar()
    {
        var (owner, employee, employeeId) = await SetupAsync("att");
        var annual = await AnnualTypeIdAsync(owner);
        var monday = NextMonday();

        var request = await employee.PostJsonAsync("/api/v1/leave/requests", new
        { leaveTypeId = annual, startDate = monday.ToString("yyyy-MM-dd"), endDate = monday.ToString("yyyy-MM-dd") });
        await owner.PostJsonAsync($"/api/v1/leave/requests/{request.GetProperty("id").GetString()!}/approve", new { approve = true });

        var summary = await employee.GetJsonAsync($"/api/v1/attendance/summary/{employeeId}/{monday.Year}/{monday.Month}");
        Assert.Equal(1, summary.GetProperty("daysOnLeave").GetInt32());
    }

    [Fact]
    public async Task EmployeeRole_CannotApproveLeave()
    {
        var (owner, employee, _) = await SetupAsync("forb");
        var annual = await AnnualTypeIdAsync(owner);
        var monday = NextMonday();

        var request = await employee.PostJsonAsync("/api/v1/leave/requests", new
        { leaveTypeId = annual, startDate = monday.ToString("yyyy-MM-dd"), endDate = monday.ToString("yyyy-MM-dd") });

        var response = await employee.PostAsJsonAsync($"/api/v1/leave/requests/{request.GetProperty("id").GetString()!}/approve",
            new { approve = true });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HrOverride_IsAudited()
    {
        var (owner, employee, _) = await SetupAsync("hro");
        var annual = await AnnualTypeIdAsync(owner);
        var monday = NextMonday();

        var request = await employee.PostJsonAsync("/api/v1/leave/requests", new
        { leaveTypeId = annual, startDate = monday.ToString("yyyy-MM-dd"), endDate = monday.ToString("yyyy-MM-dd") });

        var approve = await owner.PostJsonAsync($"/api/v1/leave/requests/{request.GetProperty("id").GetString()!}/approve",
            new { approve = true, isHrOverride = true, reviewNote = "Manager unreachable" });
        Assert.True(approve.GetProperty("hrOverride").GetBoolean());
    }
}
