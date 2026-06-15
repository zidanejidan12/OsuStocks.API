using Hangfire;

namespace OsuStocks.Infrastructure.BackgroundJobs;

public sealed class OsuSynchronizationRecurringJobRegistrar(IRecurringJobManager recurringJobManager)
    : IOsuSynchronizationRecurringJobRegistrar
{
    public void Register()
    {
        recurringJobManager.AddOrUpdate<OsuSynchronizationRecurringJob>(
            "osu-sync-tier1",
            job => job.RunTier1Async(),
            Cron.Minutely());

        // Tier2/Tier3 are staggered onto disjoint minutes that never coincide with each other (15 is a
        // multiple of 5, so the naive */5 and */15 would always fire together) nor land on :00. This
        // keeps at most one heavy tier per minute so a burst doesn't blow past osu!'s rate limit.
        // Tier2 fires at :02,:07,:12,... ; Tier3 at :09,:24,:39,:54.
        recurringJobManager.AddOrUpdate<OsuSynchronizationRecurringJob>(
            "osu-sync-tier2",
            job => job.RunTier2Async(),
            "2-59/5 * * * *");

        recurringJobManager.AddOrUpdate<OsuSynchronizationRecurringJob>(
            "osu-sync-tier3",
            job => job.RunTier3Async(),
            "9-59/15 * * * *");

        // Tier4 is the long tail (~1000-5000): hourly at :13, a minute that doesn't coincide with the
        // Tier2 (:02,:07,:12,...) or Tier3 (:09,:24,:39,:54) ticks, so the big batch never piles onto
        // another heavy tier and blow past osu!'s rate limit.
        recurringJobManager.AddOrUpdate<OsuSynchronizationRecurringJob>(
            "osu-sync-tier4",
            job => job.RunTier4Async(),
            "13 * * * *");

        recurringJobManager.AddOrUpdate<SnapshotRetentionRecurringJob>(
            "snapshot-retention",
            job => job.RunAsync(),
            Cron.Daily(4, 0));

        recurringJobManager.AddOrUpdate<InactivityDecayRecurringJob>(
            "inactivity-decay",
            job => job.RunAsync(),
            Cron.Daily(3, 0));

        recurringJobManager.AddOrUpdate<WealthSnapshotRecurringJob>(
            "wealth-snapshot",
            job => job.RunAsync(),
            Cron.Daily(2, 30));
    }
}
