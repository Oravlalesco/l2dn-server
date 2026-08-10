# NPC Brain Phase 3

## Purpose

Phase 3 separates NPC decision-making from authoritative execution without changing the Phase 2.5 scheduling model. `NpcThinkCoordinator` continues to decide when an NPC may think and preserves priority, coalescing, bounded queues, fairness, and single-flight execution.

The target pipeline is:

```text
NpcWakeContext
    -> NpcPerception
    -> NpcBrainCoordinator
       -> ReflexBrain
       -> TacticalBrain
    -> typed NpcIntent
    -> NpcIntentGateway
    -> authoritative GameServer state
```

The architectural rule is: the Brain may request an action; only GameServer may validate and execute it.

## Dependency boundary

`L2Dn.Npc.Brain` may reference only `L2Dn.Npc.Contracts` and `System.*`. It must never reference `L2Dn.GameServer.Model`, `Attackable`, `Creature`, `Player`, `World`, `GeoEngine`, skill runtime objects, networking, task managers, database providers, or transport implementations.

`NpcPerception` remains an immutable read model of world facts. `NpcBrainState` remains private to the Brain and represents short-lived logical state such as the last target, last decision, decision sequence, return-home state, and flee state. A generation change replaces the previous brain state.

## Migration ownership

Phase 3 deliberately runs legacy and Intent paths side by side, but they do not both own the same NPC execution. The exact base `AttackableAI` profile may be assigned to the Intent path; specialized and scripted profiles remain entirely on Legacy until their own vertical is migrated. Shadow observes both decisions while only Legacy executes.

The migration must not copy authoritative GameServer mechanics into the Brain. Reputation, attack permission, zones, lifecycle, range, geodata, cooldowns, movement, damage, and skill execution remain GameServer rules exposed as perception facts/affordances and revalidated by the Intent Gateway. The Brain owns policy: target selection, approach, attack choice, flee, and return-home decisions. Legacy decision policy is retired incrementally by profile after deterministic replay, decision comparison, gameplay certification, and rollback validation; it is not rewritten wholesale in one step.

## Scope

Phase 3 includes:

- typed target, attack, movement, return, flee, stop-combat, and skill intents;
- a deterministic Reflex Brain for target acquisition, target loss, approach, basic attack, return-home, and base flee policy;
- a deterministic Tactical Brain and utility evaluator for base combat choices;
- independent `Legacy`, `Shadow`, and `Intent` brain modes;
- semantic Shadow comparison without executing legacy AI twice;
- an authoritative Intent Gateway with typed rejection reasons;
- bounded-cardinality metrics for decisions, intents, validation, execution, and Shadow comparison;
- offline decision, lifecycle, replay, architecture, race, and gateway tests.

Strategy, long-term memory, relationships, personality, generative models, remote transport, raid-specific mechanics, and specialized scripted AI remain outside this phase.

## Operating modes

`NPC_REACTIVE_SCHEDULER_MODE` controls when an NPC thinks. `NPC_BRAIN_MODE` controls how it thinks.

The supported rollout matrix is:

| Reactive scheduler | Brain | Meaning |
|---|---|---|
| `Disabled` | `Legacy` | total rollback |
| `Enabled` | `Legacy` | Phase 2.5 baseline |
| `Enabled` | `Shadow` | Brain produces intents; legacy executes once |
| `Enabled` | `Intent` | Brain produces intents; Gateway validates and executes |

Unsupported combinations fall back to Legacy and emit configuration telemetry. Production defaults remain `Disabled`/`Legacy`; development enables modes explicitly.

## Intent validity

Every intent contains `Actor`, `BasedOnStateRevision`, `DecisionSequence`, and `IntentType`.

- Generation mismatch is always rejected.
- Revision mismatch is never trusted, but is not automatically rejected. The Gateway revalidates live authoritative state.
- Targets, instance affinity, lifecycle, range, affordances, skill availability, cooldown, resource cost, collision, and line of sight are revalidated immediately before execution where applicable.
- Expected protocol races return typed results; they do not use exceptions for control flow.

## Shadow semantics

Shadow performs these steps exactly once per scheduled think:

1. Capture immutable perception.
2. Let the new Brain produce intents without side effects.
3. Execute legacy `AttackableAI` once.
4. Record legacy commands through an execution-edge observer.
5. Compare the Brain intents with legacy commands as `ExactMatch`, `SemanticMatch`, `Different`, or `NotComparable`.

Randomness used by the Brain must be injectable and deterministic in tests and replay.

## Initial vertical

The first Intent-mode vertical is restricted to normal base `AttackableAI` mobs:

```text
PlayerBecameRelevant
    -> AcquireTargetIntent
    -> BasicAttackIntent / ApproachTargetIntent
    -> ClearTargetIntent / ReturnHomeIntent
```

Friendly, controllable, raid-specialized, and scripted AI remain on Legacy until explicitly migrated.

## Phase 2.5 baseline

The immutable base tag is `npc-brain-phase2.5-validated` at `9dd6cb71`.

| Signal | P50 | P95 | P99 |
|---|---:|---:|---:|
| Critical reaction | 0.89 ms | 92.39 ms | 99.01 ms |
| `PlayerBecameRelevant` | 0.59 ms | 73.33 ms | 94.67 ms |
| Combat queue | 80.07 ms | 208.97 ms | 241.79 ms |

The baseline recorded zero drops and zero scheduler failures. Phase 3 must preserve single-flight exactly, keep Scenario A drops and failures at zero, avoid network/disk/database I/O in the decision and gateway hot paths, and prevent unexplained Critical P95 degradation beyond 25% in the same environment.

## Rollback

Set:

```text
NPC_BRAIN_MODE=Legacy
NPC_REACTIVE_SCHEDULER_MODE=Enabled
```

for the Phase 2.5 path, or disable the reactive scheduler as well for total legacy rollback.

## Completion gate

Phase 3 completes only after Contracts, Brain, and GameServer Model suites pass; architecture tests enforce the dependency boundary; R1-R5 are rerun in Legacy/Shadow/Intent; Scenario A and the Dragon Valley Cave visual sequence are repeated; Release builds without new errors; rollback is tested; and the checkpoint is tagged `npc-brain-phase3-complete` with a clean worktree.

## Implemented runtime

The local runtime now provides:

- `L2Dn.Npc.Brain`, referencing only `L2Dn.Npc.Contracts`, with generation-scoped Brain state;
- deterministic target acquisition, target clearing, approach, basic attack, return-home, flee, offensive skill, and low-HP heal decisions;
- immutable skill observations including readiness, cooldown, mana, category, and range;
- `LegacyNpcThinkExecutor`, `ShadowNpcThinkExecutor`, and `BrainNpcThinkExecutor` behind the unchanged single-flight coordinator;
- a single-pass legacy command observer for Shadow comparison;
- `NpcIntentGateway` live revalidation of generation, lifecycle, target, instance, range, geodata, movement, skill, cooldown, mana, and policy;
- typed rejection outcomes for expected races rather than exceptions;
- deterministic replay directly from captured `NpcPerceptionSnapshot` sequences;
- OTLP counters/histograms and nested `npc.brain.decide` / `npc.intent.validate` activities;
- a collector allowlist covering every bounded `l2dn.npc.*` metric so Brain, intent, perception, and scheduler signals remain visible together.

Only the exact base `AttackableAI` type uses Intent execution. Specialized/scripted AI remains on Legacy by design.

## Synthetic R1-R5 validation — 2026-08-09

The benchmark used 5,000 NPCs, 100,000 mixed/storm events, 16 workers, bounded coordinator queues, and the same deterministic workload for each pipeline. Timing on a shared local Docker runtime is diagnostic; single-flight, drops, bounds, and contract correctness are hard gates.

| Pipeline | R1 P95/P99 | R2 P95/P99 | R3 P95/P99 | R4 P95/P99 | R5 P95/P99 | Worst pipeline P99 | Max concurrent/NPC |
|---|---:|---:|---:|---:|---:|---:|---:|
| Legacy | 12.60 / 12.63 ms | 10.22 / 10.60 ms | 9.96 / 10.33 ms | 38.20 / 39.15 ms | 3.56 / 3.56 ms | 0.0004 ms | 1 |
| Shadow | 26.23 / 26.35 ms | 23.31 / 23.33 ms | 11.19 / 11.48 ms | 34.72 / 35.93 ms | 3.97 / 3.97 ms | 0.813 ms | 1 |
| Intent | 24.69 / 24.79 ms | 25.44 / 25.45 ms | 10.23 / 10.56 ms | 34.81 / 35.97 ms | 18.68 / 18.71 ms | 1.066 ms | 1 |

All pipelines stayed within bounded queues and preserved exactly one concurrent Think per NPC. R1 is an artificial simultaneous 5,000-NPC critical burst: Shadow/Intent add decision allocations and queue work, explaining the relative increase while remaining far below the Phase 2.5 live Critical P95 of 92.39 ms. R4 mixed-priority Critical P95 was 37.32 ms Legacy, 29.09 ms Shadow, and 29.02 ms Intent. No network, disk, or database I/O occurs in the Brain/Gateway hot path.

## First Intent-mode gameplay regression pass

The first broad gameplay pass was intentionally treated as a failed certification run. OTLP showed repeated approach decisions, rejected guard attacks, and stale combat retention. The following defects were corrected before repeating Scenario A:

- retaliation is authorized by authoritative positive hate as well as `isAutoAttackable`, preserving guard and ally-assist behavior;
- threat fallback requires a currently visible, valid target, preventing invisible stale threats from being reacquired;
- the authoritative combat leash is captured from the legacy GameServer rules and evaluated before target handling;
- return-home clears attack, hate, attack-by, and target state before movement, and reacquisition is suppressed while returning;
- approach follows at the requested stopping range and is not restarted while the same target is already moving;
- basic attack always cancels the legacy follow task before execution;
- ranged/caster profiles with no ready offensive skill approach physical attack range instead of stalling at preferred casting range;
- only the exact base `AttackableAI` uses the new Intent event semantics; derived and scripted AIs retain Legacy behavior.

The regression suite now covers visible-threat acquisition, combat leash with an active target, return-home reacquisition suppression, guard-style retaliation, cooldown fallback range, and legacy movement target synchronization. The corrected runtime must pass a new gameplay run before Phase 3 can be tagged complete.

A second gameplay pass exposed two additional authority-boundary defects:

- `Creature.doDie()` marked an `Attackable` dead before invoking `setTarget(null)`, while the override rejected every target update on dead actors. The actor target therefore survived death and was published again after respawn. Null clearing is now explicitly allowed without restarting AI, and respawn defensively clears target, hate, and attack-by state.
- acquisition treated every visible player as a hostile candidate and treated peace zones as universal protection. Visible perception now carries the GameServer's `AutoAttackable` affordance. New acquisition requires that authority (existing positive hate remains a valid retaliation path), ordinary monsters still respect protected peace zones, and guards preserve the legacy rule that permits acquiring negative-reputation players inside a city.

The new regression coverage verifies death-time target clearing, clean combat state for each respawn generation, rejection of visible but non-attackable entities, monster peace-zone behavior, and guard acquisition of authorized targets inside protected zones.

A third gameplay pass confirmed respawn target cleanup, guard retaliation/assistance, and city combat, but exposed intermittent negative-reputation acquisition. The broad `PlayerBecameRelevant` wake was emitted when a player entered World visibility, not when that already-visible player crossed an individual guard's smaller aggro radius. A fast pass through the radius could therefore be missed until the periodic tick, while leaving and re-entering broad visibility appeared to fix it.

Movement updates now reuse World visibility as the broad phase and track exact aggro-radius membership per player/NPC generation. A wake is emitted only on entry, re-entry, or a new NPC generation; continuous movement inside the radius is coalesced at the source. The hot path reuses its per-player delegate and collections, does not scan the world, and still leaves the authoritative attack decision to perception/Brain/Gateway validation.

A fourth gameplay pass verified karma acquisition, large-group proximity aggro, leash disengage, target loss, and guard assistance. It also exposed that the Intent Brain kept a valid current target even after another player accumulated more hate. Legacy `thinkAttack()` continuously follows `getMostHated()`. The Brain now performs the same deterministic threat preference, and damage publishes a coalescible `ThreatChanged` wake after the full damage-derived hate mutation.

The pass also clarified return semantics. Legacy `MOVE_TO` return ignores passive spectators until arrival, but a new attack/aggression may interrupt it; after arrival, `ACTIVE` may reacquire any eligible hostile still inside aggro range. Those behaviors are now explicit tests rather than accidental side effects. The detailed migration inventory is maintained in `NpcLegacyBehaviorCoverage.md` so remaining legacy branches are migrated profile-by-profile instead of copied wholesale.

## Verification status

Automated and complete:

- Contracts serialization, immutability, semantic comparison, and Diff/Apply;
- pure Brain architecture, behavior, lifecycle, tactical skill, and replay tests;
- Gateway stale revision and races for generation, death, disappearance, disabled actor, blocked movement, and cooldown;
- scheduler single-flight/coalescing/fairness suite retained;
- R1-R5 in Legacy, Shadow, and Intent;
- development GameServer published and recreated in `Intent` / `Enabled` / `CaptureOnly` mode;
- OTLP scrape verified the three configured modes, live Brain decisions, perception captures, and bounded queue-depth series;
- Contracts remain green at 15/15, Brain at 29/29, and GameServer.Model at 97/97 after the gameplay regression fixes.

Environment-dependent before the completion tag:

- repeat Dragon Valley Cave proximity aggro, guard melee/ranged retaliation, pursuit, leash disengage, target loss, death/return, and guard assistance on the corrected runtime;
- capture the OTLP window and confirm zero drops/scheduler failures;
- run explicit `Legacy` rollback once;
- Scenario B/C remain certification gates before Intent can become a production default.
