namespace OsuStocks.Domain.Models;

/// <summary>
/// A point-in-time view of the credit economy for inflation monitoring.
/// <para><see cref="CirculatingCredits"/> = total wallet balances (the headline inflation gauge).</para>
/// <para><see cref="MintedCredits"/> = cumulative credits issued into circulation (grants + rewards) — the faucet.</para>
/// <para><see cref="BurnedCredits"/> = cumulative credits removed (trade fees + admin deductions) — the sink.</para>
/// </summary>
public sealed record EconomySnapshot(
    decimal CirculatingCredits,
    decimal MintedCredits,
    decimal BurnedCredits);
