using System.Net;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

public sealed class CorsTests : IAsyncLifetime
{
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        var overrides = new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "https://allowed.example.com"
        };
        _factory = new CustomWebApplicationFactory(overrides);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task PreflightRequest_AllowedOrigin_ReturnsAccessControlHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/market");
        request.Headers.Add("Origin", "https://allowed.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Authorization");

        var response = await _client.SendAsync(request);

        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Expected Access-Control-Allow-Origin header");

        var allowedOrigin = response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault();
        Assert.Equal("https://allowed.example.com", allowedOrigin);
    }

    [Fact]
    public async Task PreflightRequest_DisallowedOrigin_DoesNotReturnAccessControlHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/market");
        request.Headers.Add("Origin", "https://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request);

        Assert.False(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "Expected no Access-Control-Allow-Origin header for disallowed origin");
    }
}
