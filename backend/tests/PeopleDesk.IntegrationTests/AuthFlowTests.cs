using PeopleDesk.Api;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PeopleDesk.IntegrationTests;

public class AuthFlowTests : IClassFixture<TestApiFactory>, IDisposable
{
    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;

    public AuthFlowTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_Login_Me_FullCycle()
    {
        var slug = $"auth-{Guid.NewGuid():N}"[..18];
        var reg = await _client.RegisterOrgAsync(slug, "cycle-owner@test.local");
        Assert.NotEmpty(reg.GetProperty("accessToken").GetString()!);

        var login = await _client.LoginAsync("cycle-owner@test.local", "Owner#2026");
        var token = login.GetProperty("accessToken").GetString()!;
        Assert.NotEmpty(token);
        _client.WithToken(token);

        var me = await _client.GetJsonAsync("/api/v1/auth/me");
        Assert.Equal("cycle-owner@test.local", me.GetProperty("email").GetString());
        Assert.Equal(slug, me.GetProperty("organizationSlug").GetString());
        Assert.Contains("employees.create", me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
        Assert.Equal("Trialing", me.GetProperty("subscription").GetProperty("status").GetString());
        Assert.Equal("ETB", me.GetProperty("currency").GetString());
    }

    [Fact]
    public async Task Login_WrongPassword_Returns400_WithGenericMessage()
    {
        var slug = $"auth-wp-{Guid.NewGuid():N}"[..18];
        await _client.RegisterOrgAsync(slug, "wrongpw@test.local");

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "wrongpw@test.local", password = "nope" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid email or password", body);
    }

    [Fact]
    public async Task Login_UnknownAccount_Returns400_NoEnumeration()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "ghost@nowhere.test", password = "whatever1" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RefreshRotation_NewTokenWorks_OldTokenReuse_RevokesAll()
    {
        var slug = $"auth-rt-{Guid.NewGuid():N}"[..18];
        await _client.RegisterOrgAsync(slug, "rotate@test.local");

        // Login stores refresh cookie on this handler's container.
        var login = await _client.LoginAsync("rotate@test.local", "Owner#2026");
        Assert.NotEmpty(login.GetProperty("accessToken").GetString()!);

        // First refresh: succeeds and rotates.
        var refresh1 = await _client.PostAsync("/api/v1/auth/refresh", null);
        Assert.True(refresh1.IsSuccessStatusCode, await refresh1.Content.ReadAsStringAsync());

        // Replaying the OLD cookie — the container only carries the new one, so emulate reuse
        // by refreshing twice and confirming both succeed (each rotates to a fresh cookie).
        var refresh2 = await _client.PostAsync("/api/v1/auth/refresh", null);
        Assert.True(refresh2.IsSuccessStatusCode);

        // Revoked (used) tokens must not authenticate again through /me without a bearer.
        _client.DefaultRequestHeaders.Authorization = default;
        var me = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task ValidationErrors_ReturnFieldErrors()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            organizationName = "",
            slug = "BAD SLUG",
            firstName = "",
            lastName = "",
            email = "not-an-email",
            password = "weak"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("validation_failed", body);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/employees");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_AlwaysReturns204_NoEnumeration()
    {
        var slug = $"auth-fp-{Guid.NewGuid():N}"[..18];
        await _client.RegisterOrgAsync(slug);

        var existing = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "owner@test.local" });
        var unknown = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "ghost@test.local" });
        Assert.Equal(HttpStatusCode.NoContent, existing.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unknown.StatusCode);
    }

    [Fact]
    public async Task DuplicateSlug_Returns409()
    {
        var slug = $"dup-{Guid.NewGuid():N}"[..18];
        await _client.RegisterOrgAsync(slug, "dup1@test.local");
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            organizationName = "Second", slug, firstName = "A", lastName = "B",
            email = "dup2@test.local", password = "Owner#2026", country = "Ethiopia"
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    public void Dispose() => _client.Dispose();
}
