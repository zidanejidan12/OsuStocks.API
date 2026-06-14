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
        return GetUserInternalAsync("me", accessToken, includeTopScore: true, cancellationToken);
    }

    public Task<OsuUserProfile> GetUserAsync(
        long osuUserId,
        string accessToken,
        bool includeTopScore = true,
        CancellationToken cancellationToken = default)
    {
        return GetUserInternalAsync($"users/{osuUserId}", accessToken, includeTopScore, cancellationToken);
    }

    public async Task<OsuTopScore?> GetTopScoreAsync(
        long osuUserId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var topScore = await GetTopScoreInternalAsync(osuUserId, accessToken, cancellationToken);

        return topScore is null
            ? null
            : new OsuTopScore(
                topScore.Id,
                topScore.Pp,
                topScore.Beatmapset?.Covers?.Cover2x ?? topScore.Beatmapset?.Covers?.Cover,
                topScore.Beatmapset?.Title);
    }

    public async Task<IReadOnlyList<OsuUserProfile>> SearchUsersAsync(
        string query,
        string accessToken,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, 50);
        var searchResponse = await SendAsync<OsuSearchResponse>(
            $"search?mode=user&query={Uri.EscapeDataString(query)}",
            accessToken,
            cancellationToken);

        var users = searchResponse?.User?.Data ?? [];

        return users
            .Where(static user => user.Id > 0)
            .Take(boundedLimit)
            .Select(static user => new OsuUserProfile(
                user.Id,
                user.Username,
                user.AvatarUrl,
                user.Statistics?.Pp ?? 0m,
                user.Statistics?.GlobalRank,
                null,
                null,
                user.CountryCode))
            .ToList();
    }

    private async Task<OsuUserProfile> GetUserInternalAsync(
        string path,
        string accessToken,
        bool includeTopScore,
        CancellationToken cancellationToken)
    {
        var user = await SendAsync<OsuUserResponse>(path, accessToken, cancellationToken)
            ?? throw new InvalidOperationException($"osu! API returned empty user response for '{path}'.");

        var topScore = includeTopScore
            ? await GetTopScoreInternalAsync(user.Id, accessToken, cancellationToken)
            : null;

        return new OsuUserProfile(
            user.Id,
            user.Username,
            user.AvatarUrl,
            user.Statistics?.Pp ?? 0m,
            user.Statistics?.GlobalRank,
            topScore?.Id,
            topScore?.Pp,
            user.CountryCode,
            topScore?.Beatmapset?.Covers?.Cover2x ?? topScore?.Beatmapset?.Covers?.Cover,
            topScore?.Beatmapset?.Title);
    }

    private async Task<OsuTopScoreResponse?> GetTopScoreInternalAsync(
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

    private sealed class OsuSearchResponse
    {
        [JsonPropertyName("user")]
        public OsuSearchUsersContainerResponse? User { get; init; }
    }

    private sealed class OsuSearchUsersContainerResponse
    {
        [JsonPropertyName("data")]
        public List<OsuUserResponse> Data { get; init; } = [];
    }

    private sealed class OsuUserResponse
    {
        public long Id { get; init; }

        public string Username { get; init; } = string.Empty;

        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; init; }

        [JsonPropertyName("country_code")]
        public string? CountryCode { get; init; }

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

        [JsonPropertyName("beatmapset")]
        public OsuBeatmapsetResponse? Beatmapset { get; init; }
    }

    private sealed class OsuBeatmapsetResponse
    {
        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("covers")]
        public OsuBeatmapCoversResponse? Covers { get; init; }
    }

    private sealed class OsuBeatmapCoversResponse
    {
        [JsonPropertyName("cover@2x")]
        public string? Cover2x { get; init; }

        [JsonPropertyName("cover")]
        public string? Cover { get; init; }
    }
}
