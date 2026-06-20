using System.Diagnostics.Metrics;

namespace OsuStocks.Infrastructure.Observability;

/// <summary>
/// OpenTelemetry gauges for the credit economy (inflation monitoring), exported via OTLP → Prometheus.
/// Values are refreshed periodically by <c>EconomyMetricsRecurringJob</c>; the gauge callbacks just
/// return the last computed value, so scraping never hits the database. Registered as a singleton.
/// </summary>
public sealed class EconomyMetrics : IDisposable
{
    public const string MeterName = "OsuStocks.Economy";

    private readonly Meter _meter;
    private double _circulating;
    private double _minted;
    private double _burned;

    public EconomyMetrics()
    {
        _meter = new Meter(MeterName);

        _meter.CreateObservableGauge(
            "economy.credits.circulating",
            () => _circulating,
            unit: "{credit}",
            description: "Total credits held across all wallets — the headline inflation gauge.");

        _meter.CreateObservableGauge(
            "economy.credits.minted",
            () => _minted,
            unit: "{credit}",
            description: "Cumulative credits minted into circulation (initial grants + rewards) — the faucet.");

        _meter.CreateObservableGauge(
            "economy.credits.burned",
            () => _burned,
            unit: "{credit}",
            description: "Cumulative credits burned out of circulation (trade fees + admin deductions) — the sink.");
    }

    public void Update(decimal circulating, decimal minted, decimal burned)
    {
        Interlocked.Exchange(ref _circulating, (double)circulating);
        Interlocked.Exchange(ref _minted, (double)minted);
        Interlocked.Exchange(ref _burned, (double)burned);
    }

    public void Dispose() => _meter.Dispose();
}
