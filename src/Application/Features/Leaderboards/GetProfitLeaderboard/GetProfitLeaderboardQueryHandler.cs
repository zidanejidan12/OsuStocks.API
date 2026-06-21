using MediatR;
using OsuStocks.Application.Common.Caching;
using OsuStocks.Application.Common.Models;
using OsuStocks.Application.Features.Leaderboards.Common;
using OsuStocks.Domain.Achievements.Interfaces;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Leaderboards.GetProfitLeaderboard;

public sealed class GetProfitLeaderboardQueryHandler(
    ILeaderboardReadRepository leaderboardReadRepository,
    IReadModelCache readModelCache,
    IAchievementCatalog achievementCatalog)
    : IRequestHandler<GetProfitLeaderboardQuery, Result<GetProfitLeaderboardResponse>>
{
    public async Task<Result<GetProfitLeaderboardResponse>> Handle(
        GetProfitLeaderboardQuery request,
        CancellationToken cancellationToken)
    {
        var period = LeaderboardPeriod.Normalize(request.Period);
        var periodStart = LeaderboardPeriod.ToPeriodStart(period);
        var skip = (request.Page - 1) * request.PageSize;

        var entries = await readModelCache.GetOrSetAsync(
            $"lb:profit:{period}:{request.Page}:{request.PageSize}",
            TimeSpan.FromSeconds(30),
            ct => leaderboardReadRepository.GetProfitAsync(periodStart, skip, request.PageSize, ct),
            cancellationToken);

        var titleByCode = achievementCatalog.All.ToDictionary(a => a.Code, a => a.Name);

        return Result.Success(new GetProfitLeaderboardResponse(
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
