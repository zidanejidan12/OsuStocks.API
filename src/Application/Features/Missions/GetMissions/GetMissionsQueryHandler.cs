using MediatR;
using OsuStocks.Application.Common.Models;
using OsuStocks.Domain.Missions.Interfaces;
using OsuStocks.Domain.Missions.Models;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.Missions.GetMissions;

public sealed class GetMissionsQueryHandler(
    IMissionCatalog catalog,
    IMissionPeriodCalculator periodCalculator,
    IProgressionMetricsReadRepository metricsRepository,
    IUserMissionCompletionRepository completionRepository)
    : IRequestHandler<GetMissionsQuery, Result<GetMissionsResponse>>
{
    public async Task<Result<GetMissionsResponse>> Handle(
        GetMissionsQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // Resolve the current window per cadence and the in-period metrics once per cadence.
        var periods = catalog.All
            .Select(m => m.Period)
            .Distinct()
            .ToDictionary(type => type, type => periodCalculator.GetPeriod(type, now));

        var metricsByPeriod = new Dictionary<MissionPeriodType, MissionMetricsSnapshot>();
        foreach (var (type, period) in periods)
        {
            metricsByPeriod[type] = await metricsRepository.GetMissionMetricsAsync(
                request.UserId, period.Start, period.End, cancellationToken);
        }

        var completions = (await completionRepository.GetCompletionsAsync(
                request.UserId, periods.Values.Select(p => p.Key).ToList(), cancellationToken))
            .ToDictionary(c => (c.MissionCode, c.PeriodKey));

        var items = catalog.All
            .Select(m =>
            {
                var period = periods[m.Period];
                var current = Math.Min(metricsByPeriod[m.Period].ValueOf(m.Metric), m.Target);
                var completed = completions.TryGetValue((m.Code, period.Key), out var completion);

                return new MissionItemResponse(
                    m.Code,
                    m.Name,
                    m.Description,
                    m.Period.ToString(),
                    period.Key,
                    m.Metric.ToString(),
                    m.Target,
                    current,
                    m.RewardCredits,
                    completed,
                    completed ? completion!.CompletedAt : null,
                    period.End);
            })
            .ToList();

        return Result.Success(new GetMissionsResponse(items));
    }
}
