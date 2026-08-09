# NPC telemetry capture

The development-code compose profile starts an OpenTelemetry Collector that receives GameServer metrics over OTLP gRPC.

- Current Prometheus exposition: `http://localhost:9464/metrics`
- Persistent OTLP JSON stream: `Docker/telemetry/metrics.json`
- Saved test snapshots: `Docker/telemetry/snapshots/`

After a test session, run from `Docker`:

```powershell
.\telemetry-snapshot.cmd -Label dvc
```

The snapshot contains the cumulative reaction latency and queue-delay histograms, queue depth by priority, wake-up/coalescing/drop counters, single-flight collisions, pending follow-ups, and scheduler execution failures.

Starting a fresh Collector truncates `metrics.json`; rotated files are retained for seven days. Prometheus exposition remains in memory until the Collector is recreated.
