using System.Reflection;
using System.Runtime.CompilerServices;
using L2Dn.GameServer.AI.Runtime;
using NLog;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace L2Dn.GameServer;

internal static class TelemetryBootstrap
{
    public static IDisposable? Start(Logger logger)
    {
        string? commonEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        string? metricsEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT") ??
                                  commonEndpoint;
        string? tracesEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT") ??
                                 commonEndpoint;
        if (string.IsNullOrWhiteSpace(metricsEndpoint) && string.IsNullOrWhiteSpace(tracesEndpoint))
        {
            logger.Info("OpenTelemetry export is disabled because no OTLP endpoint is configured.");
            return null;
        }

        MeterProvider? metrics = null;
        TracerProvider? traces = null;
        try
        {
            string? version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            if (!string.IsNullOrWhiteSpace(metricsEndpoint))
            {
                RuntimeHelpers.RunClassConstructor(typeof(NpcAiTelemetry).TypeHandle);
                metrics = Sdk.CreateMeterProviderBuilder()
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
                    .AddView("l2dn.npc.perception.capture.duration", new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = [0.00001, 0.000025, 0.00005, 0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1]
                    })
                    .AddView("l2dn.pathfinding.duration", new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = [0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5]
                    })
                    .AddView("l2dn.npc.scheduler.queue_delay", new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = [0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
                    })
                    .AddView("l2dn.npc.reaction.latency", new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = [0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
                    })
                    .AddView("l2dn.npc.reaction.legacy_periodic_delay", new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = [0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
                    })
                    .AddView("l2dn.npc.region.loaded", new MetricStreamConfiguration
                    {
                        CardinalityLimit = 8192
                    })
                    .AddOtlpExporter(options => options.Endpoint = new Uri(metricsEndpoint))
                    .Build();
            }

            if (!string.IsNullOrWhiteSpace(tracesEndpoint))
            {
                traces = Sdk.CreateTracerProviderBuilder()
                    .ConfigureResource(resource => resource.AddService("L2Dn.GameServer", serviceVersion: version))
                    .AddSource(NpcAiTelemetry.ActivitySourceName)
                    .AddOtlpExporter(options => options.Endpoint = new Uri(tracesEndpoint))
                    .Build();
            }

            logger.Info($"OpenTelemetry export enabled (metrics={metricsEndpoint ?? "disabled"}, traces={tracesEndpoint ?? "disabled"}).");
            return new TelemetryProviders(metrics, traces);
        }
        catch (Exception exception)
        {
            traces?.Dispose();
            metrics?.Dispose();
            logger.Warn($"OpenTelemetry export could not be initialized and will remain disabled: {exception.Message}");
            return null;
        }
    }

    private sealed class TelemetryProviders(MeterProvider? metrics, TracerProvider? traces): IDisposable
    {
        public void Dispose()
        {
            traces?.Dispose();
            metrics?.Dispose();
        }
    }
}
