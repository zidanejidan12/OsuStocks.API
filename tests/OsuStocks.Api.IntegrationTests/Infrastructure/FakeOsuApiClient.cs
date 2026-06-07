using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.OsuIntegration.Models;
using System.Net;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class FakeOsuApiClient : IOsuApiClient
{
    private static readonly IReadOnlyList<OsuUserProfile> Users =
    [
        new OsuUserProfile(1001, "mrekk", "https://avatar.example/mrekk", 14_200m, 1, null, null),
        new OsuUserProfile(1002, "whitecat", "https://avatar.example/whitecat", 12_300m, 2, null, null),
        new OsuUserProfile(1003, "vaxei", "https://avatar.example/vaxei", 10_500m, 4, null, null)
    ];

    public Task<OsuUserProfile> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Users[0]);
    }

    public Task<OsuUserProfile> GetUserAsync(
        long osuUserId,
        string accessToken,
        bool includeTopScore = true,
        CancellationToken cancellationToken = default)
    {
        var user = Users.FirstOrDefault(x => x.OsuUserId == osuUserId);
        if (user is null)
        {
            throw new HttpRequestException($"osu user '{osuUserId}' not found.", null, HttpStatusCode.NotFound);
        }

        return Task.FromResult(user);
    }

    public Task<OsuTopScore?> GetTopScoreAsync(
        long osuUserId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var user = Users.FirstOrDefault(x => x.OsuUserId == osuUserId);
        if (user?.TopScoreId is null)
        {
            return Task.FromResult<OsuTopScore?>(null);
        }

        return Task.FromResult<OsuTopScore?>(new OsuTopScore(user.TopScoreId.Value, user.TopScorePp));
    }

    public Task<IReadOnlyList<OsuUserProfile>> SearchUsersAsync(
        string query,
        string accessToken,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, 50);

        var users = Users
            .Where(user =>
                user.Username.Contains(query, StringComparison.OrdinalIgnoreCase)
                || user.OsuUserId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(boundedLimit)
            .ToList();

        return Task.FromResult<IReadOnlyList<OsuUserProfile>>(users);
    }
}
