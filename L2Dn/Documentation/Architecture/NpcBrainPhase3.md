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
