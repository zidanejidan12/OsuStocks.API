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
    IInactivityDecaySettings inactivityDecaySettings,
    IApplicationDbContext dbContext)
    : IRequestHandler<EvaluateInactivityDecayCommand, Result<EvaluateInactivityDecayResponse>>
{
    public async Task<Result<EvaluateInactivityDecayResponse>> Handle(
        EvaluateInactivityDecayCommand request,
        CancellationToken cancellationToken)
    {
        var thresholdDays = inactivityDecaySettings.InactivityThresholdDays;
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddDays(-thresholdDays);

        var activePlayers = await trackedPlayerRepository.GetActiveAsync(cancellationToken);

        if (activePlayers.Count == 0)
        {
            return Result.Success(new EvaluateInactivityDecayResponse(0, 0));
        }

        var playerIds = activePlayers.Select(p => p.Id).ToList();

        // Inactivity is judged by pp movement, NOT snapshot recency. The sync job writes a fresh
        // snapshot for every active player each cycle, so a player's latest snapshot is never stale
        // — keying off its age (the old behaviour) meant decay could never fire. Instead compare the
        // player's current pp to their pp as of the cutoff (thresholdDays ago): no gain over that
        // window means they set no new top plays and are inactive.
        var latestSnapshots = await playerSnapshotRepository
            .GetLatestByTrackedPlayerIdsAsync(playerIds, cancellationToken);
        var baselineSnapshots = await playerSnapshotRepository
            .GetLatestAtOrBeforeByTrackedPlayerIdsAsync(playerIds, cutoff, cancellationToken);

        var decayCount = 0;

        foreach (var player in activePlayers)
        {
            if (!latestSnapshots.TryGetValue(player.Id, out var latest))
            {
                continue;
            }

            // A baseline from >= thresholdDays ago is required to measure movement; players tracked
            // for less than the window have none yet and can't be judged inactive.
            if (!baselineSnapshots.TryGetValue(player.Id, out var baseline))
            {
                continue;
            }

            // Gained pp over the window → active, skip.
            if (latest.CurrentPp > baseline.CurrentPp)
            {
                continue;
            }

            if (player.LastInactivityDecayAt is { } lastDecay
                && lastDecay.UtcDateTime.Date == now.UtcDateTime.Date)
            {
                continue;
            }

            await publisher.Publish(
                new PlayerInactiveNotification(new PlayerInactive(player.Id, now)),
                cancellationToken);

            player.LastInactivityDecayAt = now;
            trackedPlayerRepository.Update(player);

            decayCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new EvaluateInactivityDecayResponse(activePlayers.Count, decayCount));
    }
}
