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
