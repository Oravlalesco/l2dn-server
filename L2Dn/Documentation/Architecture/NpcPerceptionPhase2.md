# NPC perception phase 2 semantics

Phase 2 introduces an immutable, process-independent description of what an NPC observes. GameServer remains authoritative and the legacy command executor remains the only write boundary.

The assembly remains named `L2Dn.Npc.Contracts`; its CLR namespace is `L2Dn.NpcContracts` to avoid colliding with the existing `Npc` actor type throughout namespaces rooted at `L2Dn.GameServer`.

## Delivery gates

Phase 2A ends with full perception capture and snapshot-backed reads in the base `AttackableAI`. Phase 2B starts only after 2A passes correctness, isolation, deterministic-decision and performance gates; it adds diff/apply, partial region batches and replay serialization.

The phase does not introduce intents, gRPC, a remote brain, a regional scheduler, LLM integration or directors.

## Snapshot semantics

`NpcPerceptionSnapshot` separates diagnostic publication metadata from semantic state:

- The envelope contains schema version, NPC key, state revision, world tick and the producer's monotonic capture time.
- The state contains identity, physical and combat facts, environment, visible entities, threats, affordances and spatial observations.
- Semantic comparison ignores the envelope. Exact comparison includes it.
- Producer monotonic time is diagnostic and consumers must not use it to calculate freshness across processes.
- Legacy intention, attack timeout, global aggro, chaos time and thinking guards belong to the brain implementation and are not perception.

`StateRevision` advances only when state is published. An unchanged capture emits nothing unless a periodic full snapshot is due. A changed state emits a full snapshot or delta and advances the revision once. A new generation begins with revision 1. Captures that fail lifecycle validation consume no revision.

Schema version is a major compatibility version. Additive fields with safe defaults remain in schema v1; breaking semantic changes require a new major version. JSON consumers ignore unknown fields.

## Lifecycle protocol

`Spawn.initializeNpc()` brackets `onRespawn()` with an internal lifecycle transition. An odd lifecycle sequence means reset is in progress; an even sequence means stable. Generation advances once per transition, including initial spawn.

Capture reads a stable `(generation, lifecycle sequence)` before and after copying state and checks it once more while committing. A changed or transitioning lifecycle discards the capture and retries once. A second failure falls back to legacy thinking without waiting.

## Pure observation rule

Perception capture is observationally pure. It must never change hate, damage, targets, movement, AI intention, skills, effects, timers, events or client packets. Relation and affordance evaluators are pure readers; legacy predicates with side effects are forbidden in the builder.

Cheap facts are captured for every visible entity. Potentially expensive affordances and geodata observations are restricted to the current target and primary threat. Threat state is captured independently from visibility and enriched with visibility when possible.

The array position of a visible entity is its legacy observation ordinal. It is preserved for decision equivalence and delta reconstruction but is not promised to be stable between captures.

## Transitional boundaries

The base `AttackableAI` reads lifecycle, region activity, current target and core HP/MP facts through the snapshot-backed decision context in `SnapshotRead` mode. World/threat slices that depend on commands performed earlier in the same legacy think callback remain behind their ACL until the intent phase can make the entire decision atomic. Dynamic geodata, skill-engine validation, specialized AI overrides and command execution remain transitional.

The legacy resolver may resolve an `EntityKey` only at execution and explicitly documented compatibility boundaries. It may not use the resolved object to reread state already present in perception. Resolver usage is measured as migration debt.

NPC entity keys validate exact generation. Generation zero is a temporary best-effort identity for non-NPC actors: resolution also validates entity kind and instance and never treats the key alone as authorization.

## Publication and batching

The first observation of a generation is a full snapshot. Further full snapshots are emitted every 60 seconds by default with a deterministic, centered jitter derived from the NPC key. State changes between fulls are represented as deltas in every enabled mode; `CaptureOnly` builds and measures them but no brain consumes them.

`NpcPerceptionRegionBatch` is a partial update batch produced by one legacy scheduler pool. It carries source pool and batch sequence. Multiple batches may cover the same region and world tick; it never claims to be a complete region frame.

## Operations

Configuration is read once when the perception coordinator starts:

| Variable | Default | Meaning |
| --- | --- | --- |
| `NPC_PERCEPTION_MODE` | `Disabled` | `Disabled`, `CaptureOnly`, `ShadowValidate` or `SnapshotRead` |
| `NPC_PERCEPTION_FULL_INTERVAL_SECONDS` | `60` | Periodic recovery full interval; `0` disables periodic fulls |
| `NPC_PERCEPTION_FULL_JITTER_SECONDS` | `10` | Deterministic centered staggering window |
| `NPC_PERCEPTION_REPLAY_DIRECTORY` | unset | Enables optional JSONL replay capture in this explicit directory |
| `NPC_PERCEPTION_REPLAY_CAPACITY` | `1000` | Bounded non-blocking replay queue capacity |

Replay is disabled by default. When enabled, serialization is queued through a bounded channel; a slow or failed replay consumer cannot block NPC thinking. JSON is a fixture/debug format, not an estimate of future protobuf wire bytes.

Delta receivers must require `BaseRevision == current StateRevision`. A gap or generation mismatch discards the replica and requires a full snapshot. Expected protocol conditions return typed statuses rather than exceptions.
