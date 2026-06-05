using OsuStocks.Application.Features.OsuIntegration.Synchronization.SynchronizeTrackedPlayers;
using OsuStocks.Domain.Common.Enums;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OsuStocks.Infrastructure.BackgroundJobs;

internal static class OsuSynchronizationRecurringJobMetrics
{
    private static readonly Meter Meter = new(typeof(OsuSynchronizationRecurringJobMetrics).FullName!);
    private static readonly Counter<long> CompletionCounter = Meter.CreateCounter<long>(nameof(CompletionCounter));
    private static readonly Histogram<double> DurationHistogram = Meter.CreateHistogram<double>(nameof(DurationHistogram));
    private static readonly Counter<long> TrackedPlayersCounter = Meter.CreateCounter<long>(nameof(TrackedPlayersCounter));
    private static readonly Counter<long> SnapshotsCreatedCounter = Meter.CreateCounter<long>(nameof(SnapshotsCreatedCounter));
    private static readonly Counter<long> EventsDetectedCounter = Meter.CreateCounter<long>(nameof(EventsDetectedCounter));
    private static readonly Counter<long> RankImprovementsDetectedCounter = Meter.CreateCounter<long>(nameof(RankImprovementsDetectedCounter));

    public static void RecordCompletion(
        TrackingTier tier,
        bool isSuccess,
        TimeSpan duration,
        SynchronizeTrackedPlayersResponse? response)
    {
        var completionTags = new TagList
        {
            { nameof(TrackingTier), tier },
            { nameof(isSuccess), isSuccess }
        };

        CompletionCounter.Add(1, completionTags);
        DurationHistogram.Record(duration.TotalMilliseconds, completionTags);

        if (!isSuccess || response is null)
        {
            return;
        }

        var tierTag = new TagList
        {
            { nameof(TrackingTier), tier }
        };

        TrackedPlayersCounter.Add(response.TrackedPlayers, tierTag);
        SnapshotsCreatedCounter.Add(response.SnapshotsCreated, tierTag);
        EventsDetectedCounter.Add(response.EventsDetected, tierTag);
        RankImprovementsDetectedCounter.Add(response.RankImprovementsDetected, tierTag);
    }
}
