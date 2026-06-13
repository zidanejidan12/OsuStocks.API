using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Achievements.Interfaces;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Achievements.GetAchievements;

public sealed class GetAchievementsQueryHandler(
    IAchievementCatalog catalog,
    IProgressionMetricsReadRepository metricsRepository,
    IUserAchievementRepository achievementRepository)
    : IRequestHandler<GetAchievementsQuery, Result<GetAchievementsResponse>>
{
    public async Task<Result<GetAchievementsResponse>> Handle(
        GetAchievementsQuery request,
        CancellationToken cancellationToken)
    {
        var unlockedByCode = (await achievementRepository.GetUnlockedAsync(request.UserId, cancellationToken))
            .ToDictionary(u => u.AchievementCode, u => u.UnlockedAt);

        var metrics = await metricsRepository.GetAchievementMetricsAsync(request.UserId, cancellationToken);

        var items = catalog.All
            .Select(a =>
            {
                var unlocked = unlockedByCode.TryGetValue(a.Code, out var unlockedAt);
                // Cap displayed progress at the threshold so an unlocked achievement reads as complete.
                var current = Math.Min(metrics.ValueOf(a.Metric), a.Threshold);
                return new AchievementItemResponse(
                    a.Code,
                    a.Name,
                    a.Description,
                    a.Category,
                    a.Metric.ToString(),
                    a.Threshold,
                    current,
                    a.RewardCredits,
                    unlocked,
                    unlocked ? unlockedAt : null);
            })
            .ToList();

        return Result.Success(new GetAchievementsResponse(
            unlockedByCode.Count,
            catalog.All.Count,
            items));
    }
}
