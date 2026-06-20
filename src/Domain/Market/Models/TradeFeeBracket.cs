namespace OsuStocks.Domain.Market.Models;

/// <summary>
/// One bracket of the progressive (PPh-21-style) trade fee. <see cref="UpTo"/> is the upper bound of
/// the bracket's value range; <see cref="Rate"/> applies only to the portion of the trade value that
/// falls inside this bracket. The highest bracket is treated as unbounded (covers everything above the
/// previous bound), so its <see cref="UpTo"/> just needs to be the largest value in the schedule.
/// </summary>
public sealed record TradeFeeBracket(decimal UpTo, decimal Rate);
