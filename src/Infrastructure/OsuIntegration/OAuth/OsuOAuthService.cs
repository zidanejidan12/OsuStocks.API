using Microsoft.Extensions.Options;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.OsuIntegration.Models;
using OsuStocks.Infrastructure.OsuIntegration.Options;
using System.Net.Http.Json;
using System.Text;

namespace OsuStocks.Infrastructure.OsuIntegration.OAuth;

internal sealed class OsuOAuthService(HttpClient httpClient, IOptions<OsuOAuthOptions> options)
    : IOsuOAuthService
{
    private readonly OsuOAuthOptions _options = options.Value;

    public string BuildAuthorizationUrl(string state)
    {
        EnsureOAuthOptions();

        var scope = string.Join(' ', _options.Scopes.Where(static value => !string.IsNullOrWhiteSpace(value)));

        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = scope,
            ["state"] = state
        };

        var queryString = string.Join(
            "&",
            queryParams.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        return $"{_options.AuthorizationEndpoint}?{queryString}";
    }

    public async Task<OsuOAuthToken> ExchangeCodeForTokenAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        EnsureOAuthOptions();

        var requestData = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = _options.RedirectUri
        };

        return await ExchangeTokenAsync(requestData, cancellationToken);
    }

    public async Task<OsuOAuthToken> GetClientCredentialsTokenAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureOAuthOptions();

        var requestData = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "client_credentials",
            ["scope"] = string.Join(' ', _options.Scopes.Where(static value => !string.IsNullOrWhiteSpace(value)))
        };

        return await ExchangeTokenAsync(requestData, cancellationToken);
    }

    private async Task<OsuOAuthToken> ExchangeTokenAsync(
        Dictionary<string, string> requestData,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(requestData)
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"osu! token endpoint returned {(int)response.StatusCode}: {body}",
                null,
                response.StatusCode);
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<OsuTokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("osu! token endpoint returned an empty response.");

        if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("osu! token endpoint did not return an access token.");
        }

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(tokenResponse.ExpiresIn, 60));
        var scope = string.IsNullOrWhiteSpace(tokenResponse.Scope)
            ? string.Join(' ', _options.Scopes)
            : tokenResponse.Scope;

        return new OsuOAuthToken(tokenResponse.AccessToken, tokenResponse.RefreshToken, expiresAt, scope);
    }

    private void EnsureOAuthOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new InvalidOperationException("OsuOAuth:ClientId must be configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new InvalidOperationException("OsuOAuth:ClientSecret must be configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.RedirectUri))
        {
            throw new InvalidOperationException("OsuOAuth:RedirectUri must be configured.");
        }
    }

    private sealed class OsuTokenResponse
    {
        public string AccessToken { get; init; } = string.Empty;
        public string? RefreshToken { get; init; }
        public int ExpiresIn { get; init; }
        public string Scope { get; init; } = string.Empty;
    }
}
