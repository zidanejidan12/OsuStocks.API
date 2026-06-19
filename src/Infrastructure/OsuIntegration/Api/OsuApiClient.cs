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
        // Always the osu! standard ruleset — otherwise the user's default mode (taiko/fruits/mania)
        // is returned and we'd track the wrong pp/rank for players who default to another mode.
        return GetUserInternalAsync("me/osu", accessToken, includeTopScore: true, cancellationToken);
    }

    public Task<OsuUserProfile> GetUserAsync(
        long osuUserId,
        string accessToken,
        bool includeTopScore = true,
        CancellationToken cancellationToken = default)
    {
        // Pin to the osu! standard ruleset (see GetCurrentUserAsync) so a player's default mode
        // never skews their tracked pp/rank.
        return GetUserInternalAsync($"users/{osuUserId}/osu", accessToken, includeTopScore, cancellationToken);
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

    public async Task<IReadOnlyList<OsuTopScore>> GetTopScoresAsync(
        long osuUserId,
        string accessToken,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, 100);
        var scores = await SendAsync<List<OsuTopScoreResponse>>(
            $"users/{osuUserId}/scores/best?mode=osu&limit={boundedLimit}",
            accessToken,
            cancellationToken);

        if (scores is null)
        {
            return [];
        }

        return scores
            .Select(static score => new OsuTopScore(
                score.Id,
                score.Pp,
                score.Beatmapset?.Covers?.Cover2x ?? score.Beatmapset?.Covers?.Cover,
                score.Beatmapset?.Title,
                score.CreatedAt))
            .ToList();
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

    public async Task<IReadOnlyList<OsuUserProfile>> GetPerformanceRankingsAsync(
        int page,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        // osu! exposes the standard performance leaderboard 50 per page, up to page 200 (top 10k).
        var boundedPage = Math.Clamp(page, 1, 200);

        // The cursor[page] query param must be percent-encoded so it survives URI parsing.
        var response = await SendAsync<OsuRankingsResponse>(
            $"rankings/osu/performance?cursor%5Bpage%5D={boundedPage}",
            accessToken,
            cancellationToken);

        var ranking = response?.Ranking;
        if (ranking is null)
        {
            return [];
        }

        return ranking
            .Where(static entry => entry.User is { Id: > 0 })
            .Select(static entry => new OsuUserProfile(
                entry.User!.Id,
                entry.User.Username,
                entry.User.AvatarUrl,
                entry.Pp,
                entry.GlobalRank ?? entry.User.Statistics?.GlobalRank,
                null,
                null,
                entry.User.CountryCode))
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

    private sealed class OsuRankingsResponse
    {
        [JsonPropertyName("ranking")]
        public List<OsuRankingEntryResponse> Ranking { get; init; } = [];
    }

    private sealed class OsuRankingEntryResponse
    {
        // Rankings put the rank/pp at the statistics level and nest a compact user object.
        [JsonPropertyName("global_rank")]
        public int? GlobalRank { get; init; }

        [JsonPropertyName("pp")]
        public decimal Pp { get; init; }

        [JsonPropertyName("user")]
        public OsuUserResponse? User { get; init; }
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

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; init; }

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
