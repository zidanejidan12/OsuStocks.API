using Microsoft.Extensions.Options;
using OsuStocks.Application.Common.Interfaces;

namespace OsuStocks.Infrastructure.DailyLogin;

internal sealed class DailyRewardSettings : IDailyRewardSettings
{
    // Built-in 7-day schedule (a full week totals ~34,500 — about a third of a starting wallet, so the
    // daily login is a small floor, not a comeback fund: "if you lose, you lose"). Overridable via the
    // "DailyReward:DailyAmounts" configuration section.
    private static readonly IReadOnlyList<decimal> DefaultAmounts =
        new[] { 1500m, 2500m, 3500m, 4500m, 5500m, 7000m, 10000m };

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
