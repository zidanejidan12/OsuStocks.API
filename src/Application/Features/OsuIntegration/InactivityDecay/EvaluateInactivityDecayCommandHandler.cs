using MediatR;
using OsuStocks.Application.Common.Interfaces;
using OsuStocks.Application.Common.Models;
using OsuStocks.Application.Features.Market.Notifications;
using OsuStocks.Domain.OsuIntegration.Events;
using OsuStocks.Domain.Repositories;

namespace OsuStocks.Application.Features.OsuIntegration.InactivityDecay;

public sealed class EvaluateInactivityDecayCommandHandler(
    ITrackedPlayerRepository trackedPlayerRepository,
    IPlayerSnapshotRepository playerSnapshotRepository,
    IPublisher publisher,
    IInactivityDecaySettings inactivityDecaySettings)
    : IRequestHandler<EvaluateInactivityDecayCommand, Result<EvaluateInactivityDecayResponse>>
{
    public async Task<Result<EvaluateInactivityDecayResponse>> Handle(
        EvaluateInactivityDecayCommand request,
        CancellationToken cancellationToken)
    {
        var thresholdDays = inactivityDecaySettings.InactivityThresholdDays;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-thresholdDays);
        var now = DateTimeOffset.UtcNow;

        var activePlayers = await trackedPlayerRepository.GetActiveAsync(cancellationToken);

        if (activePlayers.Count == 0)
        {
            return Result.Success(new EvaluateInactivityDecayResponse(0, 0));
        }

        var playerIds = activePlayers.Select(p => p.Id).ToList();
        var latestSnapshots = await playerSnapshotRepository
            .GetLatestByTrackedPlayerIdsAsync(playerIds, cancellationToken);

        var decayCount = 0;

        foreach (var player in activePlayers)
        {
            if (!latestSnapshots.TryGetValue(player.Id, out var snapshot))
            {
                continue;
            }

            if (snapshot.CapturedAt > cutoff)
            {
                continue;
            }

            await publisher.Publish(
                new PlayerInactiveNotification(new PlayerInactive(player.Id, now)),
                cancellationToken);

            decayCount++;
        }

        return Result.Success(new EvaluateInactivityDecayResponse(activePlayers.Count, decayCount));
    }
}
