using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseBoard.Api.Contracts;
using PulseBoard.Api.Infrastructure;

namespace PulseBoard.Api.Tests;

/// <summary>
/// Boots the real API with the simulator switched off, a throwaway SQLite file and a
/// controllable clock, so tests drive the telemetry pipeline deterministically.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pulseboard-tests-{Guid.NewGuid():N}.db");

    public FakeClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        builder.UseSetting("Telemetry:SimulatorEnabled", "false");
        builder.UseSetting("Jwt:Key", "unit-test-signing-key-that-is-long-enough-0000");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
        });
    }

    public async Task<HttpClient> ClientAsAsync(string username)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, DbSeeder.DemoPassword));
        response.EnsureSuccessStatusCode();
        var login = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        return client;
    }

    public async Task<string> TokenForAsync(string username)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, DbSeeder.DemoPassword));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_dbPath))
        {
            SqliteConnection.ClearAllPools();
            File.Delete(_dbPath);
        }
    }
}

/// <summary>The API serialises enums as strings; the test clients must read them the same way.</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static Task<T?> GetJsonAsync<T>(this HttpClient client, string url) => client.GetFromJsonAsync<T>(url, Options);

    public static Task<T?> ReadJsonAsync<T>(this HttpContent content) => content.ReadFromJsonAsync<T>(Options);
}

public sealed class FakeClock : IClock
{
    // Starts at the real time (whole seconds, because JWT timestamps are second-precision) so
    // issued tokens are valid against the bearer middleware's own clock. Tests then advance it.
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    public void Advance(TimeSpan by) => UtcNow += by;
}
