using Microsoft.Extensions.Options;
using OsuStocks.Application.Common.Interfaces;

namespace OsuStocks.Infrastructure.DailyLogin;

internal sealed class DailyRewardSettings : IDailyRewardSettings
{
    // Built-in 7-day schedule (a full week totals ~100,000 — one starting wallet). Overridable via the
    // "DailyReward:DailyAmounts" configuration section.
    private static readonly IReadOnlyList<decimal> DefaultAmounts =
        new[] { 5000m, 7500m, 10000m, 12500m, 15000m, 20000m, 30000m };

    public DailyRewardSettings(IOptions<DailyRewardOptions> options)
    {
        var configured = options.Value.DailyAmounts;

        if (configured is { Count: > 0 })
        {
            // Guard against a misconfiguration that would debit (rather than credit) wallets.
            if (configured.Any(amount => amount <= 0m))
            {
                throw new InvalidOperationException(
                    "DailyReward:DailyAmounts must contain only positive values.");
            }

            DailyAmounts = configured.ToArray();
        }
        else
        {
            DailyAmounts = DefaultAmounts;
        }
    }

    public IReadOnlyList<decimal> DailyAmounts { get; }
}
