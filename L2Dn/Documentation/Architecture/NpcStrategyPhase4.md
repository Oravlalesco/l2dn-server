# NPC Strategy Phase 4

## Objective

Phase 4 adds deterministic strategic policy above the Phase 3 Reflex/Tactical pipeline. Strategy changes priorities and thresholds; it never reads mutable GameServer objects, creates side effects, bypasses the Intent Gateway, or performs network/database I/O.

```text
NpcPerception + immutable profiles
                |
                v
          StrategyBrain
                |
        bounded directives
                |
        +-------+--------+
        |                |
    ReflexBrain     TacticalBrain
        |                |
        +-------+--------+
                |
             NpcIntent
                |
         Intent Gateway
                |
       authoritative world
```

The Phase 3 actor boundary remains unchanged: only an exact base `Monster` using the exact base `AttackableAI` can execute through Intent. Guards, raids, minions, scripted actors, and derived AIs remain on Legacy until they receive a separately certified vertical.

## Phase 4A: strategy foundation

The first increment introduces:

- immutable `NpcStrategyProfile` values;
- a resolver with built-in cached profiles and copied `TemplateId` overrides;
- a pure `StrategyBrain` that derives effective flee thresholds and preferred ranges;
- deterministic tactical scores for basic attack, approach, offensive skills, healing, and flee;
- `Balanced`, `AggressivePressure`, `RangedControl`, and `Survival` archetypes;
- the selected bounded strategy as part of Brain decisions and OTLP decision tags.

`Balanced` preserves the Phase 3 scores and thresholds. An explicitly supplied legacy intelligence profile also defaults to `Balanced` unless the caller explicitly supplies a strategy, preventing implicit behavior changes in scripts/tests that override the old profile.

## Ownership

| Concern | Owner |
|---|---|
| profile selection, tactical weights, strategic thresholds | Strategy Brain |
| immediate target validity, lifecycle reflexes, leash state | Reflex Brain |
| concrete action selection | Tactical Brain |
| targetability, range, geodata, cooldown, mana, movement and execution | GameServer / Intent Gateway |
| guard, raid, minion and scripted coordination until migrated | Legacy AI |

## Rollout

Phase 4 code is not deployed merely because its offline tests pass. Rollout proceeds per profile:

1. deterministic Brain tests;
2. replay comparison against the Phase 3 checkpoint;
3. Shadow observation with bounded `strategy` telemetry;
4. focused gameplay test;
5. explicit Intent eligibility expansion only after certification.

Template overrides are copied into an immutable dictionary at resolver construction. Profiles are never loaded from a database or file inside the think path.

## Out of scope for Phase 4A

- LLM or cognitive decisions;
- memory service;
- gRPC or remote Brain;
- faction, squad, commander, raid, or World Director behavior;
- dynamic profile reload;
- expanding Intent eligibility beyond the Phase 3 base-monster boundary.

## Completion gates for Phase 4A

- Brain still references only `L2Dn.Npc.Contracts` among L2Dn assemblies;
- `Balanced` keeps Phase 3 decisions stable;
- profile resolution is deterministic and immutable;
- explicit profile overrides do not implicitly inherit a non-balanced strategy;
- ranged strategy controls desired skill approach range;
- survival strategy can prefer healing earlier;
- aggressive strategy can lower flee sensitivity without bypassing Reflex;
- selected strategy is observable with bounded cardinality;
- Contracts, Brain, and GameServer.Model suites remain green;
- Release build has no new errors;
- no GameServer deployment until Shadow/replay validation is prepared.

## Phase 4A foundation validation

Validated locally on 2026-08-11 from branch `feature/npc-strategy-brain`:

| Gate | Result |
|---|---:|
| `L2Dn.Npc.Contracts.Tests` | 17/17 passed |
| `L2Dn.Npc.Brain.Tests` | 53/53 passed |
| `L2Dn.GameServer.Model.Tests` | 115/115 passed |
| GameServer Release build | succeeded, 0 errors |
| `git diff --check` | passed |

The Release build retains the two pre-existing XML serializer generation warnings from `L2Dn.Model`; this increment adds no build error. The deployed GameServer remains on the Phase 3 checkpoint until Phase 4 replay and Shadow evidence are available.

## Phase 4B: strategy validation and controlled rollout

Phase 4B validates the four static profiles without adding dynamic selection, memory, groups, special AIs, remote services, or generative models. The schema-compliant `skillList` NPC data correction is part of the Phase 4B baseline so Strategy is never credited with behavior that was actually caused by previously missing NPC skills.

### Operating modes and causal Shadow

`NPC_STRATEGY_MODE` is independent from `NPC_BRAIN_MODE` and accepts `Disabled`, `Shadow`, or `Enabled`. Strategy is supported only with reactive scheduling `Enabled` and Brain `Intent`; any other combination disables only Strategy and preserves the configured scheduler and Brain modes.

- `Disabled` bypasses `StrategyBrain` and its template registry. This is the exact Phase 3 Reflex/Tactical path plus the `skillList` data correction.
- `Shadow` clones one pre-decision state, evaluates baseline and Strategy from that same state, persists only the baseline transition, executes only baseline intents, and discards the hypothetical Strategy transition.
- `Enabled` evaluates Strategy only for templates present in the immutable startup registry. Every other eligible base monster remains on the Phase 3 path.

Offline comparative replay intentionally differs from online Shadow: it maintains one independent longitudinal coordinator per profile so profile-specific trajectories can be compared across the same perception sequence.

The initial four-template laboratory registry is:

| Template | NPC | Profile | Laboratory purpose |
|---:|---|---|---|
| 20003 | Goblin | Balanced | Phase 3 identity control |
| 20004 | Imp | AggressivePressure | melee pressure and approach |
| 21101 | Langk Lizardman Shaman | RangedControl | ranged/offensive skills |
| 20292 | Enku Orc Shaman | Survival | healing and low-HP behavior |

All four are `Monster` templates and remain subject to the exact base `AttackableAI` runtime eligibility check. Guards, raids, minions, scripted AIs, specialized AIs, and controllables remain excluded.

### Talking Island Shadow expansion

After the four-template automated checkpoint, Shadow coverage was expanded to every template referenced by XML under `DataPack/spawns/TalkingIsland`. This adds 24 templates while retaining the original four laboratory templates, for 28 configured entries total. The grouping is static and capability-based:

- `Balanced`: 20016 Stone Golem, 20120 Wolf, 20121 Giant Toad, 20432 Elpy, 20442 Elder Wolf, 20481 Bearded Keltir, and 20544 Elder Keltir;
- `AggressivePressure`: 20093 Orc Warrior, 20096 Orc Lieutenant, 20098 Orc Captain, 20103 Giant Spider, 20106 Giant Fang Spider, 20108 Giant Blade Spider, 20130 Orc, 20131 Orc Soldier, 20132 Werewolf, 20326 Goblin Scout, 20342 Werewolf Chieftain, and 20343 Werewolf Hunter;
- `RangedControl`: 20006 Orc Archer, 20101 Crasher, 20110 Undine, 20113 Undine Elder, and 20115 Undine Noble.

No Talking Island template is assigned `Survival`: the loaded AI scopes contain no heal-capable mob and the current intelligence profiles do not make flee eligible there. Assigning Survival merely to distribute labels would not create meaningful validation. Template 20292 remains the dedicated Survival laboratory NPC.

All 24 area templates declare `type="Monster"`, and none is referenced by an ID-specific script. Runtime eligibility still requires the exact base `Monster` instance and exact base `AttackableAI`; a future specialized runtime instance therefore falls back safely even if its template appears in the registry. A data test derives the complete template set from the Talking Island spawn XML, prevents omissions, verifies every template loads as `Monster`, verifies the ranged group has Archer or long-range AI capability, and verifies the absence of heal scopes.

Registry parsing occurs only at startup. It trims whitespace, accepts profile names case-insensitively, requires positive template IDs, ignores empty entries, rejects malformed or unknown-profile entries with warnings, and uses last-wins plus a warning for duplicate template IDs. An invalid configuration entry never silently enables Balanced. A non-resolvable runtime archetype uses Balanced and increments bounded fallback telemetry.

### Diagnostics, comparison, and telemetry

The production decision carries only selected action, reason, and modifier flags. Candidate score and applied-modifier arrays are opt-in replay diagnostics and are not allocated on the Enabled hot path.

Semantic comparison uses deterministic precedence: comparability, intent type, target, skill ID/level, movement semantics, semantic equivalence, then exact equivalence. The bounded result set is `ExactMatch`, `SemanticMatch`, `DifferentAction`, `DifferentTarget`, `DifferentSkill`, `DifferentMovement`, and `NotComparable`.

Strategy telemetry contains only bounded `profile`, `action`, `comparison`, `modifier`, `mode`, `outcome`, and fallback-reason values. `TemplateId` and runtime object IDs are deliberately absent from metric tags. The instruments are:

- `l2dn.npc.strategy.evaluation.total` and `.duration`;
- `l2dn.npc.strategy.profile.resolved`;
- `l2dn.npc.strategy.modifier.applied`;
- `l2dn.npc.strategy.action.selected`;
- `l2dn.npc.strategy.shadow.comparison` and `.changed_decision`;
- `l2dn.npc.strategy.fallback`;
- observable `l2dn.npc.strategy.mode`.

### Automated validation — 2026-08-11

The isolated determinism gate creates a new coordinator from identical perception, state, profile, and inputs 1,000 times for each profile and compares the complete decision: actor, sequence, layer, selected intent payload, target, skill, reason, and modifier flags. A separate gate replays the same multi-frame sequence twice per profile and requires exact output sequences. No RNG was introduced; if nondeterminism is added later it must be an explicit injected dependency with independent streams for comparison branches.

S1-S7 cover melee/offensive skill, low HP/heal, ranged range, outside all ranges, cooldown, insufficient mana, and eligible flee. S8-S9 mutate actor/target lifecycle and generation after decision. Gateway tests separately prove that Strategy cannot bypass cooldown, mana, range, geodata, dead actor/target, or generation validation.

| Gate | Result |
|---|---:|
| `L2Dn.Npc.Contracts.Tests` | 17/17 passed |
| `L2Dn.Npc.Brain.Tests` | 75/75 passed |
| `L2Dn.GameServer.Model.Tests` | 124/124 passed |
| focused NPC `skillList` loading | 1/1 passed |
| GameServer Release build | succeeded, 0 errors |
| `git diff --check` | passed |

The full StaticData suite was not used as a Phase 4B gate because it did not finish within four minutes in the local Docker environment; the focused NPC loading test completed and validates templates 20001, 21101, and 20292 including their AI skill scopes. Release retains only the two pre-existing XML serializer warnings.

### Synthetic R1-R5

R1-R5 ran with 5,000 NPCs, 100,000 mixed/storm events, 16 workers, and identical loads. Timings are diagnostic on a shared local Docker host; drops, overflow, scheduler failures, and single-flight are hard gates.

| Pipeline | R1 P95/P99 | R2 P95/P99 | R3 P95/P99 | R4 P95/P99 | R5 P95/P99 | Worst Strategy P99 |
|---|---:|---:|---:|---:|---:|---:|
| Strategy Disabled | 35.25 / 35.35 ms | 23.46 / 23.48 ms | 10.80 / 11.11 ms | 41.51 / 43.35 ms | 3.70 / 3.70 ms | n/a |
| Strategy Shadow | 30.12 / 30.31 ms | 23.57 / 23.62 ms | 10.74 / 11.33 ms | 38.82 / 40.00 ms | 19.41 / 19.42 ms | 0.3643 ms |
| Strategy Enabled | 22.42 / 22.54 ms | 17.59 / 17.62 ms | 10.40 / 10.75 ms | 41.48 / 42.83 ms | 18.82 / 18.85 ms | 0.5766 ms |

All 15 scenarios reported zero dropped wakeups, zero overflow states, and exactly one maximum concurrent Think per NPC. Enabled allocations/event were close to Disabled in every scenario; Shadow allocations were higher but bounded because it performs two decisions and comparison. The benchmark performs no network, database, or disk I/O in the decision pipeline.

## Manual rollout gate

The development compose configuration now stages `NPC_STRATEGY_MODE=Shadow` with the four-template registry above. Phase 4 is not complete yet. The next required evidence is:

1. publish/restart the local GameServer and play normally for 15–30 minutes in Shadow;
2. verify Strategy evaluations/comparisons are non-zero, Strategy Gateway executions remain zero, drops/failures remain zero, single-flight remains one, and gameplay matches Phase 3 plus the NPC skill correction;
3. switch to `Enabled`, test only the four laboratory templates and inspect intent rejection ratios and oscillations;
4. switch Strategy back to `Disabled` and verify Phase 3 rollback;
5. record live results, then and only then create `npc-brain-phase4-complete`.

No Phase 5 work, live-result commit, or completion tag is authorized before this gate passes.
