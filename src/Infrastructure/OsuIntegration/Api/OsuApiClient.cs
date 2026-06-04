using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.OsuIntegration.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace OsuStocks.Infrastructure.OsuIntegration.Api;

internal sealed class OsuApiClient(HttpClient httpClient) : IOsuApiClient
{
    public Task<OsuUserProfile> GetCurrentUserAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        return GetUserInternalAsync("me", accessToken, cancellationToken);
    }

    public Task<OsuUserProfile> GetUserAsync(
        long osuUserId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        return GetUserInternalAsync($"users/{osuUserId}", accessToken, cancellationToken);
    }

    private async Task<OsuUserProfile> GetUserInternalAsync(
        string path,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var user = await SendAsync<OsuUserResponse>(path, accessToken, cancellationToken)
            ?? throw new InvalidOperationException($"osu! API returned empty user response for '{path}'.");

        var topScore = await GetTopScoreAsync(user.Id, accessToken, cancellationToken);

        return new OsuUserProfile(
            user.Id,
            user.Username,
            user.AvatarUrl,
            user.Statistics?.Pp ?? 0m,
            user.Statistics?.GlobalRank,
            topScore?.Id,
            topScore?.Pp);
    }

    private async Task<OsuTopScoreResponse?> GetTopScoreAsync(
        long osuUserId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var bestScores = await SendAsync<List<OsuTopScoreResponse>>(
            $"users/{osuUserId}/scores/best?mode=osu&limit=1",
            accessToken,
            cancellationToken);

        return bestScores?.FirstOrDefault();
    }

    private async Task<T?> SendAsync<T>(string relativeUrl, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"osu! API request '{relativeUrl}' failed with status {(int)response.StatusCode}: {body}",
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private sealed class OsuUserResponse
    {
        public long Id { get; init; }

        public string Username { get; init; } = string.Empty;

        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; init; }

        public OsuStatisticsResponse? Statistics { get; init; }
    }

    private sealed class OsuStatisticsResponse
    {
        [JsonPropertyName("pp")]
        public decimal Pp { get; init; }

        [JsonPropertyName("global_rank")]
        public int? GlobalRank { get; init; }
    }

    private sealed class OsuTopScoreResponse
    {
        public long Id { get; init; }

        [JsonPropertyName("pp")]
        public decimal? Pp { get; init; }
    }
}
