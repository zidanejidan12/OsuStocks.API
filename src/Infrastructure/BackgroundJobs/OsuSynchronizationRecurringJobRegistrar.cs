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

        recurringJobManager.AddOrUpdate<OsuSynchronizationRecurringJob>(
            "osu-sync-tier2",
            job => job.RunTier2Async(),
            "*/5 * * * *");

        recurringJobManager.AddOrUpdate<OsuSynchronizationRecurringJob>(
            "osu-sync-tier3",
            job => job.RunTier3Async(),
            "*/15 * * * *");

        recurringJobManager.AddOrUpdate<InactivityDecayRecurringJob>(
            "inactivity-decay",
            job => job.RunAsync(),
            Cron.Daily(3, 0));
    }
}
