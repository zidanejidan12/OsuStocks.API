using Hangfire;
using OsuStocks.Domain.Common.Enums;

namespace OsuStocks.Infrastructure.BackgroundJobs;

public sealed class OsuSynchronizationRecurringJobRegistrar(IRecurringJobManager recurringJobManager)
    : IOsuSynchronizationRecurringJobRegistrar
{
    public void Register()
    {
        recurringJobManager.AddOrUpdate<OsuSynchronizationRecurringJob>(
            "osu-sync-tier1",
            job => job.RunTierAsync(TrackingTier.Tier1),
            Cron.Minutely());

        recurringJobManager.AddOrUpdate<OsuSynchronizationRecurringJob>(
            "osu-sync-tier2",
            job => job.RunTierAsync(TrackingTier.Tier2),
            "*/5 * * * *");

        recurringJobManager.AddOrUpdate<OsuSynchronizationRecurringJob>(
            "osu-sync-tier3",
            job => job.RunTierAsync(TrackingTier.Tier3),
            "*/15 * * * *");
    }
}
