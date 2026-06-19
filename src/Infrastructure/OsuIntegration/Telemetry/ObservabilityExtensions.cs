using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OsuStocks.Infrastructure.OsuIntegration.Telemetry;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Registers osu! API metrics. The OTLP exporter is only added when an endpoint is configured
    /// (config key <c>OpenTelemetry:OtlpEndpoint</c> or env <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>), so
    /// local/dev runs collect nothing and stay a no-op until you point it at Grafana (or any OTLP
    /// backend). Auth headers/protocol come from the standard OTEL_EXPORTER_OTLP_* env vars.
    /// </summary>
    public static IServiceCollection AddOsuApiObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<OsuApiTelemetry>();

        var otlpEndpoint =
            Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
            ?? configuration["OpenTelemetry:OtlpEndpoint"];

        var serviceName =
            configuration["OpenTelemetry:ServiceName"]
            ?? Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")
            ?? "osustocks";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(OsuApiTelemetry.MeterName);

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    // Prometheus' OTLP receiver lives at <base>/api/v1/otlp and expects the per-signal
                    // path /v1/metrics. Because we set Endpoint in code, the SDK does NOT auto-append
                    // that path (it only does so for a bare OTEL_EXPORTER_OTLP_ENDPOINT env var), so we
                    // append it ourselves — otherwise the push 404s and no metrics are ingested. The
                    // receiver is HTTP/protobuf only, so pin the protocol regardless of env.
                    var metricsEndpoint =
                        otlpEndpoint.Contains("/v1/metrics", StringComparison.OrdinalIgnoreCase)
                            ? otlpEndpoint
                            : otlpEndpoint.TrimEnd('/') + "/v1/metrics";

                    metrics.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(metricsEndpoint);
                        options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    });
                }
            });

        return services;
    }
}
