using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.OsuIntegration.Models;
using OsuStocks.Infrastructure.OsuIntegration.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OsuStocks.Infrastructure.OsuIntegration.OAuth;

internal sealed class OsuOAuthService(
    HttpClient httpClient,
    IOptions<OsuOAuthOptions> options,
    IDistributedCache cache)
    : IOsuOAuthService
{
    // The app-wide client-credentials token is identical for every caller, so cache it once instead
    // of POSTing /oauth/token on every sync cycle (which multiplied token-endpoint load and risked 429s).
    private const string ClientCredentialsCacheKey = "osu:client-token";

    // Refresh slightly before expiry so an in-flight request never races a just-expired token.
    private static readonly TimeSpan ExpiryGuard = TimeSpan.FromSeconds(60);

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
        var cached = await TryGetCachedClientTokenAsync(cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        EnsureOAuthOptions();

        var requestData = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "client_credentials",
            ["scope"] = string.Join(' ', _options.Scopes.Where(static value => !string.IsNullOrWhiteSpace(value)))
        };

        var token = await ExchangeTokenAsync(requestData, cancellationToken);
        await CacheClientTokenAsync(token, cancellationToken);
        return token;
    }

    private async Task<OsuOAuthToken?> TryGetCachedClientTokenAsync(CancellationToken cancellationToken)
    {
        string? payload;
        try
        {
            payload = await cache.GetStringAsync(ClientCredentialsCacheKey, cancellationToken);
        }
        catch
        {
            // A cache outage must never block token acquisition — fall through to a live fetch.
            return null;
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        var token = JsonSerializer.Deserialize<OsuOAuthToken>(payload);
        if (token is null || token.ExpiresAt - DateTimeOffset.UtcNow <= ExpiryGuard)
        {
            return null;
        }

        return token;
    }

    private async Task CacheClientTokenAsync(OsuOAuthToken token, CancellationToken cancellationToken)
    {
        var ttl = token.ExpiresAt - DateTimeOffset.UtcNow - ExpiryGuard;
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        try
        {
            await cache.SetStringAsync(
                ClientCredentialsCacheKey,
                JsonSerializer.Serialize(token),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                cancellationToken);
        }
        catch
        {
            // Best-effort: if the cache write fails we simply fetch again next time.
        }
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
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("scope")]
        public string Scope { get; init; } = string.Empty;
    }
}
