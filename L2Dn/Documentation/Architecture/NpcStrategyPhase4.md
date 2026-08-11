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
