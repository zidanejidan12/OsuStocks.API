using OsuStocks.Api.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

public sealed class OAuthLoginEndpointsTests
{
    [Fact]
    public async Task Login_WithAllowListedOrigin_ReturnsRedirect()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/v1/auth/login?returnUrl=https://app.osustocks.example/dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("state=", response.Headers.Location!.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithUnknownOrigin_ReturnsBadRequest()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/v1/auth/login?returnUrl=https://evil.example/steal");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("VALIDATION_ERROR", payload.Code);
    }

    [Fact]
    public async Task Login_WithLocalhostOrigin_InDevelopment_ReturnsRedirect()
    {
        await using var factory = new CustomWebApplicationFactory(new Dictionary<string, string?>
        {
            ["Security:OAuthReturnUrl:AllowedOrigins:0"] = "https://app.osustocks.example",
            ["Security:OAuthReturnUrl:AllowedOrigins:1"] = null
        });

        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/v1/auth/login?returnUrl=http://localhost:5173/callback");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private sealed record ErrorResponse(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("traceId")] string TraceId);
}

