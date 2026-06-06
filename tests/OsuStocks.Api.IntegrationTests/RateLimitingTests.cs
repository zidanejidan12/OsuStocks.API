using System.Net;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

public sealed class RateLimitingTests : IAsyncLifetime
{
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AuthEndpoints_ExceedingRateLimit_Returns429()
    {
        // Auth rate limit: 10 requests per minute
        var tasks = Enumerable.Range(0, 15)
            .Select(_ => _client.GetAsync("/api/v1/auth/login?returnUrl=https://app.osustocks.example"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        var okCount = responses.Count(r => r.StatusCode != HttpStatusCode.TooManyRequests);
        var rateLimitedCount = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);

        Assert.True(okCount <= 10, $"Expected at most 10 non-429 responses but got {okCount}");
        Assert.True(rateLimitedCount > 0, "Expected at least one 429 response");
    }

    [Fact]
    public async Task TradingEndpoints_ExceedingRateLimit_Returns429()
    {
        // Trading rate limit: 30 requests per minute
        var tasks = Enumerable.Range(0, 35)
            .Select(_ => _client.PostAsync(
                "/api/v1/trading/buy",
                new StringContent("{\"stockId\":\"00000000-0000-0000-0000-000000000001\",\"quantity\":1}",
                    System.Text.Encoding.UTF8, "application/json")))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        var rateLimitedCount = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);

        Assert.True(rateLimitedCount > 0, "Expected at least one 429 response for trading");
    }
}
