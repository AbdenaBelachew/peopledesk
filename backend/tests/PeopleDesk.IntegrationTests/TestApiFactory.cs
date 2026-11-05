using PeopleDesk.Api;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace PeopleDesk.IntegrationTests;

/// <summary>Isolated API host per test class: private SQLite database, no rate limiting, fast seed.</summary>
public class TestApiFactory : WebApplicationFactory<Program>
{
    public string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"pd_test_{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        Console.WriteLine("[TESTFACTORY] ConfigureWebHost called");
        builder.ConfigureAppConfiguration(cfg => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "sqlite",
            ["Database:SqlitePath"] = $"Data Source={DbPath}",
            ["Jwt:Secret"] = "test-secret-key-0123456789abcdef0123456789abcdef",
            ["Testing:DisableRateLimiting"] = "true",
            ["Auth:FrontendBaseUrl"] = "http://test.local",
            ["Email:Mode"] = "FileSink",
            ["Email:SinkPath"] = "test-email-sink",
            ["Platform:SuperAdminEmail"] = "platform@test.local",
            ["Platform:SuperAdminPassword"] = "Super#2026"
        }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { if (File.Exists(DbPath)) File.Delete(DbPath); } catch { /* best effort */ }
    }
}

/// <summary>Shared JSON options matching the API (enums as strings).</summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public static class TestClientExtensions
{
    public static async Task<JsonElement> RegisterOrgAsync(this HttpClient client, string slug, string email = "owner@test.local")
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            organizationName = $"Org {slug}",
            slug,
            firstName = "Test",
            lastName = "Owner",
            email,
            password = "Owner#2026",
            country = "Ethiopia"
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options);
    }

    public static async Task<JsonElement> LoginAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestJson.Options));
    }

    public static HttpClient WithToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<JsonElement> PostJsonAsync(this HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body, TestJson.Options);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{(int)response.StatusCode} {url}: {content}");
        return JsonSerializer.Deserialize<JsonElement>(content, TestJson.Options);
    }

    public static async Task<HttpResponseMessage> TryPostJsonAsync(this HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body, TestJson.Options);
        response.EnsureSuccessStatusCode();
        return response;
    }

    public static async Task<JsonElement> GetJsonAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{(int)response.StatusCode} {url}: {content}");
        return JsonSerializer.Deserialize<JsonElement>(content, TestJson.Options);
    }
}
