using System.Net;
using System.Net.Http.Json;
using PulseBoard.Api.Contracts;
using PulseBoard.Api.Domain;
using PulseBoard.Api.Infrastructure;

namespace PulseBoard.Api.Tests;

public sealed class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Login_returns_token_and_role()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("operator", DbSeeder.DemoPassword));
        var body = await response.Content.ReadJsonAsync<LoginResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("operator", body.Username);
        Assert.Equal(Roles.Operator, body.Role);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        Assert.Equal(factory.Clock.UtcNow.AddMinutes(480), body.ExpiresAt);
    }

    [Theory]
    [InlineData("operator", "wrong-password")]
    [InlineData("nobody", "pulse123")]
    public async Task Login_rejects_bad_credentials(string username, string password)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_validates_empty_fields()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("", ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoints_require_a_token()
    {
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/devices")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/alerts")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/alerts/1/acknowledge", null)).StatusCode);
    }

    [Fact]
    public async Task Me_reflects_the_token_claims()
    {
        using var client = await factory.ClientAsAsync("admin");

        var me = await client.GetJsonAsync<Dictionary<string, string>>("/api/auth/me");

        Assert.NotNull(me);
        Assert.Equal("admin", me["username"]);
        Assert.Equal(Roles.Admin, me["role"]);
    }

    [Fact]
    public async Task Viewer_can_read_but_not_operate()
    {
        using var client = await factory.ClientAsAsync("viewer");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/devices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/alerts/1/acknowledge", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/alerts/1/resolve", null)).StatusCode);
    }

    [Fact]
    public async Task Tampered_token_is_rejected()
    {
        using var client = factory.CreateClient();
        var token = await factory.TokenForAsync("viewer");
        var tampered = token[..^4] + "AAAA";
        client.DefaultRequestHeaders.Authorization = new("Bearer", tampered);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/devices")).StatusCode);
    }
}
