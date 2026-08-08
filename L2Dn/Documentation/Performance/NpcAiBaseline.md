# NPC AI baseline runbook

This runbook captures a comparable baseline for the NPC brain modernization work. Run it first against the instrumentation-only checkpoint and repeat it after each architectural change.

## Preconditions

- Record commit SHA, build configuration, operating system, CPU count/model, memory and container limits.
- Record the exact DataPack, server configuration, geodata set and database snapshot.
- Export GameServer metrics to an OTLP collector. Running `L2Dn.Dashboard` supplies the Aspire dashboard endpoint; a direct launch must set `OTEL_EXPORTER_OTLP_ENDPOINT` or `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT`.
- Keep NPC spawns, client scripts and test routes identical between comparisons.

## Capture procedure

1. Start AuthServer, GameServer, database and the OTLP backend.
2. Load the scenario and wait 10 minutes for JIT, caches and populations to stabilize.
3. Capture 15 uninterrupted minutes. Do not restart or change configuration during the window.
4. Export the dashboard data and complete the result table below.
5. Repeat each scenario three times; report the median run and retain all raw captures.

## Scenarios

| Scenario | Players | Loaded NPCs | Required activity |
| --- | ---: | ---: | --- |
| A | 100 | 5,000 | Normal field activity; record thinking, visible and combat NPCs |
| B | 250 | 5,000 or production-equivalent | Same routes and spawn set as A |
| C | Production target | Record actual | Siege or raid with deterministic participant route |

## Result template

| Measurement | A | B | C |
| --- | ---: | ---: | ---: |
| Online players |  |  |  |
| NPC loaded / thinking / combat / visible / sleeping |  |  |  |
| Think calls/s |  |  |  |
| Think duration p50 / p95 / p99 |  |  |  |
| Think allocations p50 / p95 / p99 |  |  |  |
| World query calls/s and p99 |  |  |  |
| Geo query calls/s and p99 |  |  |  |
| Pathfinding calls/s and p99 |  |  |  |
| Process CPU time rate |  |  |  |
| GC pause and allocation rate |  |  |  |
| Thread-pool queue length |  |  |  |

Use `l2dn.npc.think.busy_time` as synchronous NPC AI demand, not as exclusive thread CPU. Correlate it with `dotnet.process.cpu.time`; other GameServer work shares the process.

## Comparison gate

- No player disconnects or AI scheduler stalls caused by telemetry.
- Compare the same 15-minute window and configuration.
- Flag a regression when think p99, process CPU rate or allocation rate increases by more than 5% in two of three runs.
- Attach decision/behavior observations separately; performance equivalence alone does not prove behavior equivalence.
