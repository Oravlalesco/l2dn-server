# NPC Reactivity Phase 2.5

## Scope

Phase 2.5 hardens when an NPC may think. It does not introduce `NpcBrain`, `NpcIntent`, an intent gateway, remote transport, or new gameplay decisions. The authoritative execution path remains:

```text
NpcPerception -> AttackableAI -> legacy command execution
```

The periodic scheduler remains available as the disabled-mode fallback and becomes one wake-up source when reactive execution is enabled.

## Required properties

1. Relevant combat and spatial events can wake an NPC before the next periodic tick.
2. At most one `Think` executes for a given `NpcKey` at any instant.
3. Repeated events for one NPC are coalesced into a reason bitmask and at most one necessary follow-up.
4. Critical work is preferred under saturation while lower-priority work receives bounded fairness.
5. Queue memory is bounded and no wake-up creates an unbounded `Task.Run` workload.
6. The NPC generation is checked immediately before execution; stale work is discarded.
7. Capture or AI failures are isolated to one execution and do not terminate a scheduler worker.

## Architecture

```text
combat / visibility events                    periodic tick
            |                                      |
            +------------- wake-up ----------------+
                              |
                    NpcThinkCoordinator
                     /        |        \
                 critical   combat    normal
                     \        |        /
                       fair dequeue
                              |
                        single-flight
                              |
                    LegacyNpcThinkExecutor
                              |
                       NpcPerception
                              |
                         AttackableAI
```

The coordinator schedules opportunity to think; it does not decide gameplay.

## Wake event coverage

| Wake reason | Priority | Existing source | Phase 2.5 action |
|---|---:|---|---|
| `PeriodicDue` | Normal | `AttackableThinkTaskManager` fixed-rate pool | Route through coordinator in Enabled mode |
| `Attacked` | Critical | `AbstractAI.notifyEvent(EVT_ATTACKED)` | Wake after the synchronous legacy event handler |
| `ThreatChanged` | Combat | `AbstractAI.notifyEvent(EVT_AGGRESSION)` | Wake after the synchronous legacy event handler |
| `TargetLost` | Critical | `EVT_FORGET_OBJECT` when forgotten object was current target | Wake after legacy forget handling |
| `TargetDied` | Critical | Target forgotten while dead-like | Classify the same reliable forget source |
| `PlayerBecameRelevant` | Critical | Player spawn or player region transition already evaluated by `World` | Wake only candidate `Attackable` objects in newly relevant regions |
| `RegionActivated` | Normal | `WorldRegion.SetActive(true)` | Wake resident attackables once on activation |
| `Respawned` | Normal | NPC spawn lifecycle | Defer until the actor is registered with its AI scheduler |
| `CombatStarted` | Combat | Derivable from intention changes | Observe through existing combat events; no duplicate source in v1 |
| `CombatEnded` | Normal | No single authoritative event | Deferred |
| `AllyAttacked` | Combat | Existing aggression/minion assist propagation | Covered as `ThreatChanged`; dedicated classification deferred |

Movement inside one region does not emit `PlayerBecameRelevant`. Region transitions reuse the set of newly surrounding regions already traversed by `World.switchRegion`; the implementation never scans the whole world.

## Priorities and fairness

- Critical: attacked, target lost/died, player became relevant.
- Combat: threat changes and combat propagation.
- Normal: periodic maintenance, region activation, respawn.

Workers use weighted service. A bounded burst of Critical work is followed by an opportunity for Combat and Normal, preventing starvation without putting Critical behind an existing Normal backlog.

## Coalescing and backpressure

Each registered NPC has a small runtime state. Pending reasons are flags, pending priority keeps the highest urgency, and queued/running transitions are coordinated per NPC. Events received while running create pending work, not another concurrent execution.

The three active queues are bounded. Critical overflow is represented by the already bounded per-NPC runtime state and is retried as queue capacity becomes available; it is never silently discarded. Normal periodic wake-ups may be delayed or coalesced during overload. All overload outcomes are telemetry-visible.

## Operating modes

`NPC_REACTIVE_SCHEDULER_MODE` accepts:

- `Disabled`: current direct periodic execution; event hooks are inert.
- `Observe`: current direct periodic execution plus measurement of event-to-next-periodic delay.
- `Shadow`: queueing, priorities, coalescing, generation checks, and single-flight execute against a no-op shadow executor; gameplay remains on the direct periodic path.
- `Enabled`: periodic and reactive wake-ups share the coordinator and legacy executor.

Event flags are independently controlled with `NPC_WAKE_ON_ATTACKED`, `NPC_WAKE_ON_TARGET_LOST`, `NPC_WAKE_ON_PLAYER_RELEVANT`, and `NPC_WAKE_ON_THREAT_CHANGED`.

## Initial configuration

| Variable | Default |
|---|---:|
| `NPC_REACTIVE_WORKER_COUNT` | `0` (automatic) |
| `NPC_REACTIVE_QUEUE_CAPACITY` | `20000` |
| `NPC_CRITICAL_MIN_THINK_INTERVAL_MS` | `50` |
| `NPC_COMBAT_MIN_THINK_INTERVAL_MS` | `100` |
| `NPC_NORMAL_MIN_THINK_INTERVAL_MS` | `500` |
| `NPC_REACTION_TELEMETRY_ENABLED` | `true` |

The initial reaction SLO under nominal load is Critical P50 below 100 ms, P95 below 250 ms, and P99 below 500 ms. These are operational targets to calibrate with the real A/B/C scenarios, not universal CI timing gates.

## Validation gates

- Single-flight and event-during-think concurrency tests.
- Coalescing, priority, fairness, stale-generation, exception-isolation, rollback, and overload tests.
- Synthetic R1-R5 benchmark with bounded queue depth and allocations reported.
- Existing Phase 2 tests remain green.
- Release build succeeds.
- Real A/B/C and visual player-passes-near-mob validation are recorded when the DataPack, database, load generator, and telemetry backend are available.

## Rollback

Set `NPC_REACTIVE_SCHEDULER_MODE=Disabled` and restart. `AttackableThinkTaskManager` then executes the same legacy periodic path directly. Per-event flags allow a noisy source to be disabled independently while the coordinator remains enabled.

