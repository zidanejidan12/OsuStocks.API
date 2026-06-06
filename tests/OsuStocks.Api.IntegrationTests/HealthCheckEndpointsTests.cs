using OsuStocks.Api.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class HealthCheckEndpointsTests(PostgresTestcontainerFixture fixture)
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/api/v1/health")]
    public async Task HealthEndpoint_WithHealthyDependencies_ReturnsHealthyStatus(string path)
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsPostgresqlCheck()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var checks = json.GetProperty("checks").EnumerateArray().ToList();

        Assert.Contains(checks, c => c.GetProperty("name").GetString() == "postgresql");
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsRedisCheck()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var checks = json.GetProperty("checks").EnumerateArray().ToList();

        Assert.Contains(checks, c => c.GetProperty("name").GetString() == "redis");
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsDurationMetrics()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("totalDuration").GetDouble() >= 0);

        var checks = json.GetProperty("checks").EnumerateArray().ToList();
        foreach (var check in checks)
        {
            Assert.True(check.GetProperty("duration").GetDouble() >= 0);
        }
    }

    [Fact]
    public async Task HealthEndpoint_WithUnreachablePostgres_ReturnsUnhealthy()
    {
        await using var factory = new PostgresWebApplicationFactory(
            fixture,
            postgresConnectionOverride: "Host=localhost;Port=59999;Database=nonexistent;Username=bad;Password=bad;Timeout=1");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unhealthy", json.GetProperty("status").GetString());
    }
}
