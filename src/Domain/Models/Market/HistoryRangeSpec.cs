namespace OsuStocks.Domain.Models.Market;

/// <summary>
/// Translates a requested history range (1h|24h|7d|30d) into the time-bucketing
/// parameters used to build OHLC candles: a lower-bound timestamp and a PostgreSQL
/// <c>date_trunc</c> granularity / interval pair.
/// </summary>
public sealed record HistoryRangeSpec
{
    public const string Range1h = "1h";
    public const string Range24h = "24h";
    public const string Range7d = "7d";
    public const string Range30d = "30d";

    public static readonly IReadOnlyList<string> SupportedRanges =
        [Range1h, Range24h, Range7d, Range30d];

    private HistoryRangeSpec(string range, DateTimeOffset from, string truncUnit, string bucketInterval)
    {
        Range = range;
        From = from;
        TruncUnit = truncUnit;
        BucketInterval = bucketInterval;
    }

    /// <summary>The normalized range token (e.g. "24h").</summary>
    public string Range { get; }

    /// <summary>Inclusive lower bound for the candle window.</summary>
    public DateTimeOffset From { get; }

    /// <summary>
    /// PostgreSQL <c>date_trunc</c> base unit used to floor each row onto a coarse boundary
    /// ("minute", "hour", "day"). Combined with <see cref="BucketInterval"/> to land on the
    /// requested sub-multiple (e.g. 30-minute or 6-hour buckets).
    /// </summary>
    public string TruncUnit { get; }

    /// <summary>
    /// The full bucket width as a PostgreSQL interval literal
    /// ("1 minute", "30 minutes", "6 hours", "1 day").
    /// </summary>
    public string BucketInterval { get; }

    public static bool IsSupported(string? range) =>
        !string.IsNullOrWhiteSpace(range) &&
        SupportedRanges.Contains(range.Trim().ToLowerInvariant());

    /// <summary>
    /// Resolves the spec for a supported range relative to <paramref name="now"/>.
    /// Throws when the range is not supported — callers must validate first.
    /// </summary>
    public static HistoryRangeSpec FromRange(string range, DateTimeOffset now)
    {
        var normalized = range.Trim().ToLowerInvariant();

        return normalized switch
        {
            // 1h -> 1-minute candles
            Range1h => new HistoryRangeSpec(Range1h, now.AddHours(-1), "minute", "1 minute"),
            // 24h -> 30-minute candles
            Range24h => new HistoryRangeSpec(Range24h, now.AddHours(-24), "minute", "30 minutes"),
            // 7d -> 6-hour candles
            Range7d => new HistoryRangeSpec(Range7d, now.AddDays(-7), "hour", "6 hours"),
            // 30d -> 1-day candles
            Range30d => new HistoryRangeSpec(Range30d, now.AddDays(-30), "day", "1 day"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(range), range, "Unsupported history range.")
        };
    }
}
