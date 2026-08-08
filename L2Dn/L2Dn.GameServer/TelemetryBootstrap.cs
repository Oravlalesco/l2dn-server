using System.Reflection;
using L2Dn.GameServer.AI.Runtime;
using NLog;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace L2Dn.GameServer;

internal static class TelemetryBootstrap
{
    public static MeterProvider? Start(Logger logger)
    {
        string? endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT") ??
                           Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            logger.Info("OpenTelemetry metrics export is disabled because no OTLP endpoint is configured.");
            return null;
        }

        try
        {
            string? version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            MeterProvider provider = Sdk.CreateMeterProviderBuilder()
                .ConfigureResource(resource => resource.AddService("L2Dn.GameServer", serviceVersion: version))
                .AddMeter(NpcAiTelemetry.MeterName)
                .AddMeter("System.Runtime")
                .AddView("l2dn.npc.think.duration", new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5]
                })
                .AddView("l2dn.npc.world_query.duration", new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [0.00001, 0.000025, 0.00005, 0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05]
                })
                .AddView("l2dn.npc.geo_query.duration", new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [0.00001, 0.000025, 0.00005, 0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1]
                })
                .AddView("l2dn.pathfinding.duration", new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5]
                })
                .AddView("l2dn.npc.region.loaded", new MetricStreamConfiguration
                {
                    CardinalityLimit = 8192
                })
                .AddOtlpExporter()
                .Build();

            logger.Info($"OpenTelemetry metrics export enabled for {endpoint}.");
            return provider;
        }
        catch (Exception exception)
        {
            logger.Warn($"OpenTelemetry metrics export could not be initialized and will remain disabled: {exception.Message}");
            return null;
        }
    }
}
