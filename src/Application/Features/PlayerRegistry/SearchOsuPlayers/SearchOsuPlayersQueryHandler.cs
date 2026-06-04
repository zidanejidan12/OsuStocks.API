using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.OsuIntegration.Interfaces;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.PlayerRegistry.SearchOsuPlayers;

public sealed class SearchOsuPlayersQueryHandler(
    IOsuOAuthService osuOAuthService,
    IOsuApiClient osuApiClient,
    ITrackedPlayerRepository trackedPlayerRepository)
    : IRequestHandler<SearchOsuPlayersQuery, Result<SearchOsuPlayersResponse>>
{
    public async Task<Result<SearchOsuPlayersResponse>> Handle(
        SearchOsuPlayersQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var token = await osuOAuthService.GetClientCredentialsTokenAsync(cancellationToken);
            var osuUsers = await osuApiClient.SearchUsersAsync(
                request.Query,
                token.AccessToken,
                request.Limit,
                cancellationToken);

            var osuUserIds = osuUsers.Select(x => x.OsuUserId).Distinct().ToArray();
            var trackedPlayers = await trackedPlayerRepository.GetByOsuUserIdsAsync(osuUserIds, cancellationToken);
            var trackedByOsuId = trackedPlayers.ToDictionary(x => x.OsuUserId, x => x);

            var items = osuUsers
                .Select(osuUser =>
                {
                    trackedByOsuId.TryGetValue(osuUser.OsuUserId, out var trackedPlayer);

                    return new SearchOsuPlayerItemResponse(
                        osuUser.OsuUserId,
                        osuUser.Username,
                        osuUser.AvatarUrl,
                        osuUser.CurrentPp,
                        osuUser.GlobalRank,
                        trackedPlayer is not null,
                        trackedPlayer?.Id,
                        trackedPlayer?.IsActive);
                })
                .ToList();

            return Result.Success(new SearchOsuPlayersResponse(items));
        }
        catch (HttpRequestException)
        {
            return Result.Failure<SearchOsuPlayersResponse>(
                "OSU_API_UNAVAILABLE",
                "Unable to reach osu! API.");
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<SearchOsuPlayersResponse>("OAUTH_PROCESSING_FAILED", ex.Message);
        }
    }
}
