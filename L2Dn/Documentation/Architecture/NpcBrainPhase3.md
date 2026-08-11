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

Phase 3 deliberately runs legacy and Intent paths side by side, but they do not both own the same NPC execution. Only the exact base `Monster` actor with the exact base `AttackableAI` profile may be assigned to the Intent path; guards, raids, minions, specialized actors, and scripted profiles remain entirely on Legacy until their own vertical is migrated. Shadow observes both decisions while only Legacy executes.

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

Only the exact base `Monster` actor paired with the exact base `AttackableAI` type uses Intent execution. Sharing `AttackableAI` is not sufficient: guards and every specialized/scripted actor remain on Legacy by design.

## Synthetic R1-R5 validation — 2026-08-09

The benchmark used 5,000 NPCs, 100,000 mixed/storm events, 16 workers, bounded coordinator queues, and the same deterministic workload for each pipeline. Timing on a shared local Docker runtime is diagnostic; single-flight, drops, bounds, and contract correctness are hard gates.

| Pipeline | R1 P95/P99 | R2 P95/P99 | R3 P95/P99 | R4 P95/P99 | R5 P95/P99 | Worst pipeline P99 | Max concurrent/NPC |
|---|---:|---:|---:|---:|---:|---:|---:|
| Legacy | 12.60 / 12.63 ms | 10.22 / 10.60 ms | 9.96 / 10.33 ms | 38.20 / 39.15 ms | 3.56 / 3.56 ms | 0.0004 ms | 1 |
| Shadow | 26.23 / 26.35 ms | 23.31 / 23.33 ms | 11.19 / 11.48 ms | 34.72 / 35.93 ms | 3.97 / 3.97 ms | 0.813 ms | 1 |
| Intent | 24.69 / 24.79 ms | 25.44 / 25.45 ms | 10.23 / 10.56 ms | 34.81 / 35.97 ms | 18.68 / 18.71 ms | 1.066 ms | 1 |

All pipelines stayed within bounded queues and preserved exactly one concurrent Think per NPC. R1 is an artificial simultaneous 5,000-NPC critical burst: Shadow/Intent add decision allocations and queue work, explaining the relative increase while remaining far below the Phase 2.5 live Critical P95 of 92.39 ms. R4 mixed-priority Critical P95 was 37.32 ms Legacy, 29.09 ms Shadow, and 29.02 ms Intent. No network, disk, or database I/O occurs in the Brain/Gateway hot path.

## Final scheduler and telemetry rerun — 2026-08-11

R1-R5 were repeated after the leash-grace, defensive-return, retarget, and anti-stall corrections, using 5,000 NPCs, 100,000 mixed/storm events, 16 workers, and all three execution pipelines. Timings are diagnostic because the run shared the local Docker host; boundedness, single-flight, drops, and overflow are hard gates.

| Pipeline | R1 P95/P99 | R2 P95/P99 | R3 P95/P99 | R4 P95/P99 | R5 P95/P99 | R4 Critical P95/P99 |
|---|---:|---:|---:|---:|---:|---:|
| Legacy | 21.33 / 21.37 ms | 19.89 / 20.60 ms | 19.56 / 20.15 ms | 127.89 / 129.64 ms | 6.96 / 6.99 ms | 126.77 / 127.71 ms |
| Shadow | 38.96 / 39.26 ms | 28.29 / 28.36 ms | 15.39 / 15.90 ms | 77.54 / 80.44 ms | 7.74 / 7.75 ms | 66.92 / 68.25 ms |
| Intent | 39.72 / 39.84 ms | 41.22 / 41.43 ms | 14.04 / 14.38 ms | 91.25 / 94.52 ms | 17.62 / 17.63 ms | 75.36 / 76.83 ms |

Every scenario and pipeline reported `MaximumConcurrentThinkPerNpc = 1`, `DroppedWakeups = 0`, and `PeakOverflowStates = 0`. Intent's worst synthetic pipeline P99 was 1.77 ms. The relative timing between modes is not treated as a stable performance ratio on this shared host; Intent Critical P95 remained below the Phase 2.5 live reference of 92.39 ms and the internal 250 ms nominal-load objective.

The focused two-player gameplay window was captured in `Docker/telemetry/snapshots/20260811-141950-phase3-pre-final.prom`. OTLP recorded:

- 75/75 `Approach(TowardSpawnOnly)` intents executed;
- 20/20 `ReturnHome(PreserveThreat)` intents executed;
- 2/2 `ReturnHome(TeleportReset)` intents executed;
- all Critical, Combat, and Normal queues at depth zero at the end of the window;
- no wake-up drops and no scheduler execution failures;
- four expected live-state races only: three `dead_actor` rejections and one `out_of_range` rejection.

Operational rollback was exercised by recreating the GameServer in `Legacy` / `Enabled`, confirming a successful startup and authentication-server connection, and then recreating it again in `Intent` / `Enabled`. The final Intent instance started successfully and exposed the game port on 7777. No Brain-mode configuration residue remained after the rollback test.

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

A fifth gameplay pass showed that interrupting `ReturnHome` was not sufficient by itself. The first wake acquired the new attacker, but the next Think observed the NPC outside its normal combat leash and immediately requested `ReturnHome` again. A higher-hate attacker could therefore become the visible target without receiving the follow-up attack.

The Brain now keeps generation-scoped `LeashGrace` and `DefensiveReturn` states. Crossing the configured soft leash opens one fixed 20-second combat window; incoming damage does not renew that window. A hard boundary 500 units beyond the soft leash ends the window immediately. Once grace ends, subsequent damage/threat changes may renew the two-minute defensive-return timeout and the highest visible valid threat may replace the current target without canceling the route home. In-range attacks and skills remain legal, while defensive approach is marked `TowardSpawnOnly` and revalidated by the GameServer. A target that moves outward, a blocked approach, or an outward geodata redirect falls back to `ReturnHome(PreserveThreat)` instead of leaving the NPC without an action. A third soft-leash excursion in the same combat emits `ReturnHome(TeleportReset)`, which teleports to spawn and clears target/hate. Arrival, death, generation replacement, timeout, or invalid target also terminates the state. `NPC_RETURN_DEFENSE_*` and `NPC_LEASH_*` control the policy; specialized NPCs remain on Legacy.

A sixth focused pass exposed an attack-envelope mismatch in guard assistance. The Brain compared center-to-center distance only with the configured physical or skill range, while the authoritative Gateway correctly added the actor and target collision radii. Inside that collision envelope the Brain repeatedly emitted `ApproachTarget`, the Gateway correctly considered the guard close enough and performed no movement, and no `BasicAttack`/`CastSkill` intent was ever produced. The same window accumulated 23,374 successful generic approach intents versus 4,448 basic attacks, with no blocked or out-of-range rejection capable of explaining the stall.

Tactical range evaluation now uses the same physical/skill range plus both collision radii as the Gateway. Guard assistance also publishes `AllyAttacked` after the authoritative hate mutation. In `Intent` / `Enabled`, the Brain exclusively owns acquisition and movement; the old immediate legacy follow is retained only outside that pipeline. This removes the one-second periodic dependency and prevents two movement owners from competing. Dedicated physical and skill collision-envelope regressions raise the Brain suite to 45/45; guard gameplay must be repeated once on the corrected deployment.

A seventh pass then demonstrated why actor-level eligibility is required. Guards at a second city entrance joined only after the player attacked again, whereas legacy `thinkAttack()` propagates an active guard's attacker through clan/faction calls and `EVT_AGGRESSION`. `Guard` uses the exact base `AttackableAI`, so the previous AI-type-only check incorrectly treated it as a migrated Brain profile even though faction coordination remains explicitly outside Phase 3.

`NpcBrainEligibility` now requires both the exact base `Monster` actor and exact base `AttackableAI`. `AttackableAI.UsesIntentBrain`, Intent/Shadow executors, and the authoritative Gateway share that single policy. Guards therefore retain the reactive scheduler and the post-hate `AllyAttacked` wake but execute the complete legacy guard policy, including cross-group clan assistance. The actor-boundary regression raises GameServer.Model to 114/114; the two-entrance guard sequence remains the focused gameplay gate.

An eighth guard-focused pass exposed a separate legacy timeout loop. When the 1,200-tick attack window expired, `AttackableAI` changed the guard to `ACTIVE` without clearing its target or hate. The following active cycle selected the same player again and opened another two-minute attack window, producing a long pursuit with an apparent idle pause between cycles. Guard timeout handling now aborts attack and follow, clears target, hate, and attack-by memory, switches to walking, and issues return-home immediately. Other actor timeout semantics remain unchanged. The dedicated regression raises GameServer.Model to 115/115, and `l2dn.npc.guard.pursuit_reset` exposes the bounded transition through OTLP.

## Verification status

Automated and complete:

- Contracts serialization, immutability, semantic comparison, and Diff/Apply;
- pure Brain architecture, behavior, lifecycle, tactical skill, and replay tests;
- Gateway stale revision and races for generation, death, disappearance, disabled actor, blocked movement, and cooldown;
- scheduler single-flight/coalescing/fairness suite retained;
- R1-R5 in Legacy, Shadow, and Intent;
- development GameServer published and recreated in `Intent` / `Enabled` / `CaptureOnly` mode;
- OTLP scrape verified the three configured modes, live Brain decisions, perception captures, and bounded queue-depth series;
- Contracts remain green at 17/17, Brain at 45/45, and GameServer.Model at 115/115 after the leash-grace, anti-stall, collision-envelope, actor-eligibility, and guard pursuit-reset hardening; the Release GameServer build completes with zero errors.

Gameplay and operational validation complete on the local two-player environment:

- Dragon Valley Cave proximity aggro, pursuit, leash disengage, target loss, death/respawn cleanup, return-home, highest-hate retargeting, guard retaliation, and negative-reputation acquisition;
- defensive return, spawn-directed approach, repeated leash excursion/teleport reset, and absence of the previous idle-limbo state;
- OTLP capture with zero wake-up drops and zero scheduler failures;
- explicit `Legacy` rollback followed by restoration to `Intent` / `Enabled`.

The local development gate is complete. The final client pass confirmed the corrected guard return behavior and emitted eight `l2dn.npc.guard.pursuit_reset{outcome="return_home"}` transitions. Critical, combat, and normal queue depths all returned to zero, with no wake-up drop or scheduler execution-failure series for the final service instance. The operator accepted the behavior and authorized progression to Phase 4.

Scenario B (250 players) and Scenario C (siege/raid hotspot) remain production certification gates. They cannot be certified from the local two-player environment and do not block isolated Strategy/Profile development; they continue to block declaring the pipeline a production default.

## Final focused gameplay matrix

These cases close the local functional gate without repeating broad free-form testing:

| ID | Setup and action | Required result |
|---|---|---|
| F3-01 | Pull an aggressive mob, teleport the target away, and leave a passive second player beside its return path. | The mob clears the lost target, returns home, and does not attack the spectator merely for crossing its path. |
| F3-02 | While that mob is returning, let the second player damage it. | The mob retaliates immediately when legal; it attacks in range or approaches only toward spawn, never stalls. |
| F3-03 | Let player 1 regain highest hate while positioned farther outward than player 2. | Target selection follows highest valid hate, but an illegal outward pursuit becomes `ReturnHome(PreserveThreat)` rather than idle limbo. |
| F3-04 | Kite a mob repeatedly across its soft leash with ranged attacks. | It receives the fixed grace window, respects the hard boundary, and the configured third excursion teleports it home and clears combat. |
| F3-05 | Kill the mob during/after return, wait for respawn, then approach with both players. | The new generation has no stale target or hate and performs only fresh, range-authorized acquisition. |
| F3-06 | Attack one of two nearby guards, then repeat after guard death/respawn; also cross their aggro boundary with negative reputation. | Retaliation and assistance work before death, respawn clears stale combat, and karma acquisition occurs on boundary entry inside or outside the city as authorized. |

For every case, the closing OTLP window must retain zero `wakeup.dropped` and zero scheduler execution failures. Typed `dead_actor`, `target_not_found`, or `out_of_range` rejections are acceptable only when they correspond to an observed lifecycle/range race and do not leave an NPC stalled.
