using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
                    metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
                }
            });

        return services;
    }
}
