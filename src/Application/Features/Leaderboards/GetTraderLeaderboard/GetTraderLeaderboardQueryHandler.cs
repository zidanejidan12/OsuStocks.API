using MediatR;
using OsuStocks.Application.Common.Caching;
using OsuStocks.Application.Common.Models;
using OsuStocks.Application.Features.Leaderboards.Common;
using OsuStocks.Domain.Achievements.Interfaces;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Leaderboards.GetTraderLeaderboard;

public sealed class GetTraderLeaderboardQueryHandler(
    ILeaderboardReadRepository leaderboardReadRepository,
    IReadModelCache readModelCache,
    IAchievementCatalog achievementCatalog)
    : IRequestHandler<GetTraderLeaderboardQuery, Result<GetTraderLeaderboardResponse>>
{
    public async Task<Result<GetTraderLeaderboardResponse>> Handle(
        GetTraderLeaderboardQuery request,
        CancellationToken cancellationToken)
    {
        var period = LeaderboardPeriod.Normalize(request.Period);
        var periodStart = LeaderboardPeriod.ToPeriodStart(period);
        var skip = (request.Page - 1) * request.PageSize;

        var entries = await readModelCache.GetOrSetAsync(
            $"lb:traders:{period}:{request.Page}:{request.PageSize}",
            TimeSpan.FromSeconds(30),
            ct => leaderboardReadRepository.GetTradersAsync(periodStart, skip, request.PageSize, ct),
            cancellationToken);

        var titleByCode = achievementCatalog.All.ToDictionary(a => a.Code, a => a.Name);

        return Result.Success(new GetTraderLeaderboardResponse(
            entries.Select(x => new LeaderboardEntryResponse(
                x.Rank,
                x.UserId,
                x.Username,
                x.AvatarUrl,
                x.CountryCode,
                x.Value,
                x.PeriodChange,
                x.EquippedTitleCode is not null && titleByCode.TryGetValue(x.EquippedTitleCode, out var t) ? t : null)).ToList(),
            period,
            request.Page,
            request.PageSize));
    }
}
