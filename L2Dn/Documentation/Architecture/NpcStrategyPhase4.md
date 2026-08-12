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
| `L2Dn.Npc.Brain.Tests` | 93/93 passed |
| `L2Dn.GameServer.Model.Tests` | 133/133 passed |
| focused NPC data/Talking Island/laboratory loading | 3/3 passed |
| GameServer Release build | succeeded, 0 errors |
| `git diff --check` | passed |

The full StaticData suite was not used as a Phase 4B gate because it did not finish within four minutes in the local Docker environment; the focused NPC loading test completed and validates templates 20001, 21101, and 20292 including their AI skill scopes. Release retains only the two pre-existing XML serializer warnings.

### Synthetic R1-R5

R1-R5 ran with 5,000 NPCs, 100,000 mixed/storm events, 16 workers, and identical loads. Timings are diagnostic on a shared local Docker host; drops, overflow, scheduler failures, and single-flight are hard gates.

| Pipeline | R1 P95/P99 | R2 P95/P99 | R3 P95/P99 | R4 P95/P99 | R5 P95/P99 | Worst Strategy P99 |
|---|---:|---:|---:|---:|---:|---:|
| Strategy Disabled | 25.14 / 25.28 ms | 30.89 / 30.91 ms | 11.21 / 11.54 ms | 33.49 / 34.64 ms | 3.94 / 3.96 ms | n/a |
| Strategy Shadow | 30.60 / 30.88 ms | 20.37 / 20.43 ms | 10.22 / 10.46 ms | 44.52 / 45.45 ms | 19.15 / 19.18 ms | 0.6806 ms |
| Strategy Enabled | 30.36 / 30.56 ms | 25.85 / 25.89 ms | 10.67 / 11.02 ms | 36.61 / 37.53 ms | 3.53 / 3.54 ms | 1.2590 ms |

All 15 scenarios reported zero dropped wakeups, zero overflow states, and exactly one maximum concurrent Think per NPC. Enabled Critical P95 stayed within 20.8% of Disabled in the worst comparable scenario, below the 25% regression budget. Enabled allocations/event were close to Disabled in every scenario; Shadow allocations were higher but bounded because it performs two decisions and comparison. The benchmark performs no network, database, or disk I/O in the decision pipeline.

R1-R5 were repeated after the fighter-pursuit and one-shot range-wakeup corrections. All 15 scenarios again reported zero drops, zero overflow, and maximum concurrent Think/NPC of one. Worst Strategy P99 was 0.3546 ms in Shadow and 0.8802 ms in Enabled.

### Shadow gameplay observation: repeated melee skill selection

The first Talking Island Shadow play pass made NPC skills visible after the `skillList` data correction, but exposed a Phase 3 tactical baseline issue: Crasher, another observed skill-capable mob, and Orc repeatedly selected their ready offensive/control skill, including at melee distance. This was not Strategy execution. Shadow executes only the baseline intent; Strategy remained hypothetical and never reached the Gateway.

The cause was the static Phase 3 offensive-skill score winning every evaluation while the skill remained ready. Tactical selection first suppressed an immediately repeated offensive skill at physical range. Further live validation with Crasher and Undine Noble established that both templates are data-defined `FIGHTER` NPCs despite their ranged magic: Crasher uses skill 4247 (`DDMagicSlow`/Dagger Storm), while Undine Noble uses skill 4001 (`DDMagic`/Windstrike). Both are loaded into the effective `ATTACK` AI scope because they deal magic damage. The shared visual did not mean they were the same skill or `MAGE` templates.

The final deterministic rule preserves that distinction. A `FIGHTER` may use a ready ranged skill as an opener or after its target runs out of melee, but then yields to physical pursuit; while continuing that approach it does not repeatedly choose the ranged skill, and inside physical reach it prioritizes `BasicAttack`. A data-defined `MAGE` or `HEALER` retains ranged spell preference and only suppresses an immediately repeated melee cast. This restores the legacy tactical intent without reintroducing its random skill-chance roll. The rule applies equally to Disabled, Shadow baseline, and Enabled; it adds no RNG, dynamic profile selection, direct execution, or Gateway bypass.

Replay diagnostics expose the condition as `RepeatedActionSuppressed`. Automated coverage proves the four profiles share the melee safety behavior, the exact Phase 3 path receives the same correction, and ranged casts are not suppressed merely for being consecutive.

The follow-up Crasher pass on 2026-08-12 confirmed immediate ranged casting, pursuit beyond Dagger Storm range, and resumed ranged attacks, but also exposed short idle-looking windows. The corresponding service-instance telemetry recorded 186 created casts, 115 executed casts, and 71 `skillunavailable` rejections, with no cast rejection for mana, cooldown, or range. Dagger Storm has a two-second hit time; reactive wakeups were evaluating Tactical again while the prior cast was still active.

Tactical now emits no intent while the perception carries `NpcCombatFlags.Casting`. A no-intent evaluation preserves the previous effective decision and its world tick, so the completion `ActionReady` wakeup still applies deterministic tactical continuity. This eliminates speculative cast/attack commands during an active cast without delaying or weakening Gateway validation.

The next live pass exposed a separate range-boundary pause: an intent follow reached its requested range but remained registered without waking Brain, so the NPC retained its target until the next periodic Think. Intent-created follows are now one-shot at the boundary: reaching range removes that follow and emits exactly one `ActionReady`. A new decision then uses current range, MP, cooldown, target, and geodata. Automated coverage includes Crasher's control skill, Undine Noble's direct skill, melee priority, ranged re-use after the player runs, active-cast suppression, and the Gateway request for the one-shot range wakeup.

### Accepted Shadow pass

The corrected Shadow gameplay pass on 2026-08-12 was accepted after retesting the ranged-skill cadence and the one-shot range wakeup. Its service instance produced 31,124 Strategy evaluations/comparisons, all `ExactMatch`, while only the Phase 3 baseline reached the Gateway. It created 323 intents and executed 318; the five rejections were bounded `dead_actor` races. All 14 created `CastSkill` intents executed. There were no cooldown, MP, range, blocked-movement, or `skillunavailable` rejections, and no scheduler drops or execution failures.

This closes the Shadow gameplay gate. It proves that the diagnostic path is safe and that the skill-loading/tactical corrections are a stable baseline. The zero changed-decision rate in that particular pass does not close Enabled validation: the next stage must deliberately exercise states where several legal actions compete.

## Talking Island Strategy validation laboratory

`spawns/TalkingIsland/StrategyValidationLab.xml` defines a dedicated four-lane laboratory around the existing Gatekeeper destination **Talking Island, Eastern Territory**. The normal arrival point remains clear:

`-95336, 240478, -3264`

Each lane contains three fixed-anchor copies with a 20-second respawn. Lanes are separated beyond the templates' normal aggro and clan-help radii so one profile can be tested without activating the complete laboratory.

| Lane | Template | NPC | Profile | Approximate lane centre |
|---|---:|---|---|---|
| north | 20016 | Stone Golem | Balanced | `-94450, 242050, -3400` |
| east | 20130 | Orc | AggressivePressure | `-92867, 240700, -3380` |
| south | 20115 | Undine Noble | RangedControl | `-94250, 238983, -3450` |
| west | 20292 | Enku Orc Shaman | Survival | `-96933, 240050, -3400` |

The AggressivePressure area-wave cohort occupies additional fixed anchors further east of the accepted Orc lane. Those copies stay outside the four-profile aggro and clan-help radii, including Orc Captain's 1000 aggro range:

| Wave lane | Templates | NPCs | Approximate lane centre |
|---|---|---|---|
| east+ | 20098 | Orc Captain | `-91200, 240700, -3380` |
| northeast | 20103, 20106, 20108 | Giant Spider / Fang / Blade | `-91000, 242500, -3380` |
| southeast | 20132, 20326, 20131 | Werewolf, Goblin Scout, Orc Soldier | `-91000, 239000, -3380` |

The laboratory is test infrastructure, not part of the normal Talking Island population used to derive the 24-template area rollout. Automated data coverage checks that the four profile lanes still contain exactly three unique spawn anchors each, that the AggressivePressure wave cohort uses only the listed templates, that every laboratory NPC is a base `Monster`, and that the file does not silently alter the production-area template inventory.

Deployment checkpoint on 2026-08-12: the focused data suite passed 3/3, the spawn XML passed schema validation, and the GameServer publish completed with zero errors (the two existing XML serializer-generator warnings remain). Startup loaded 29,151 spawns and completed initialization without rejecting a laboratory entry. The effective container environment contains exactly the four mappings above, while the latest OTLP mode gauges report reactive scheduler `Enabled`, Brain `Intent`, and Strategy `enabled`.

### First Enabled laboratory pass

The first four-lane client pass produced three accepted capability-level results:

- Balanced Stone Golem remained a pure physical control, as expected because its active AI skill set has no offensive cast.
- RangedControl Undine Noble used physical attacks at melee distance and Windstrike after the player opened range.
- Survival Enku Orc Shaman remained a caster and selected self-heal at low HP. OTLP recorded four `heal` selections for the observed pass.

AggressivePressure Orc was not accepted in that pass: it produced 64 `attack` selections and zero `offensive_skill` selections even though template 20130 owns skill 4072. Static-data inspection established that 4072 is a physical, point-blank stun represented with cast range `-1` and loaded through the effective `DEBUFF` scope. The Fighter safety rule introduced for Crasher/Undine ranged magic was suppressing this legitimate melee skill together with ranged casts.

Tactical eligibility now distinguishes physical point-blank/melee skills from ranged or magical Fighter skills. A ready physical contact skill may compete by Strategy score once the actor reaches physical range; immediate repetition is still suppressed, so the expected Orc cadence is `Stun -> BasicAttack`, followed by normal attacks until the six-second skill reuse permits another stun. A non-positive offensive cast range now means physical contact rather than unlimited range. Ranged magic still yields to physical attacks in melee, and Gateway cooldown, MP, target, range, geodata, and generation validation is unchanged.

Automated validation after this correction reports Contracts 17/17, Brain 93/93, GameServer.Model 133/133, and focused data/laboratory 3/3. Enabled R1-R5 at 5,000 NPCs/100,000 mixed events reports zero drops, zero overflow states, and maximum concurrent Think/NPC of one in all five scenarios; worst Strategy P99 was 1.0521 ms.

The focused Orc retest was accepted for **selection and Gateway execution**. The player observed stun attempts whenever reuse permitted, with a visible interval of physical attacks rather than continuous skill execution. The isolated AggressivePressure telemetry recorded 23 `offensive_skill`, 75 `attack`, and 54 `approach` selections. All 23 Orc casts reached execution; there were no skill, cooldown, MP, range, geodata, or blocked-movement rejections. The only two Gateway rejections in the session were bounded `dead_actor` races. No dropped wakeup or scheduler-execution-failure series was emitted.

That pass did not prove authoritative control. A later live session showed the stun icon while the player could still move. GameServer logged `callSkill() failed: Action blocked event requires a Creature argument` from `BlockActions.onStart` → `startParalyze` → `EVT_ACTION_BLOCKED` with no `Creature`. `SkillCaster.callSkill` swallowed the exception, so the abnormal could display without finishing immobilize. The defect is GameServer effect/AI notify, not Strategy or template 20130.

After passing the caster into control AI events (and skipping root/mute/confuse notify when the caster is missing, so those handlers cannot self-aggro), a live Orc retest on 2026-08-12 applied both the icon and the movement lock. Control skills are therefore accepted only when Gateway executes them **and** the target is actually action-blocked for the abnormal duration, with zero `callSkill() failed` of this family.

The four-profile laboratory gate is therefore PASS. Rollout proceeds to the low-risk Balanced wave while retaining the accepted Orc, Undine Noble, and Enku Orc Shaman templates as controls. The first area wave enables Balanced for templates 20016, 20120, 20121, 20432, 20442, 20481, and 20544; all other normal Talking Island templates remain Phase 3.

The seven-template Balanced area wave was accepted in the client on 2026-08-12. The observed normal mobs retained the Phase 3 control behavior, with no reported duplicate effects, pathological oscillation, or unexpected strategic action. During this gate, the control-effect implementation was also corrected so stun completes authoritative action blocking after Gateway execution and root/mute/confuse notifications cannot create self-threat when no caster exists. Dedicated contracts cover movement and skill blocking, current-movement cancellation, caster threat, and absence of stun threat. The complete post-fix automated gate remains green at Contracts 17/17, Brain 93/93, GameServer.Model 133/133, and focused data 3/3.

With Balanced accepted, the second area wave enables all 12 `AggressivePressure` templates: 20093, 20096, 20098, 20103, 20106, 20108, 20130, 20131, 20132, 20326, 20342, and 20343. Template 20130 remains the already accepted stun control; the other 11 are the only newly enabled area templates. The four remaining normal ranged templates stay on Phase 3, while 20115 and 20292 remain accepted laboratory controls.

The AggressivePressure area wave was accepted in the client on 2026-08-12 after a restored Intent/Strategy session (the preceding spawn-only restart had dropped those environment variables and is not evidence). Observed cases:

- Orc 20130 stunned when reuse permitted, interleaved with physical attacks, with authoritative movement lock;
- Undine Noble used Windstrike after the player opened range and physical attacks at contact;
- Enku Orc Shaman used offensive magic and self-heal at low HP;
- Orc Captain, Giant Spider / Fang / Blade, Werewolf, Goblin Scout, and Orc Soldier applied melee pressure, approached, and returned at leash without restoring full HP.

The restored service instance recorded 37,943 Strategy evaluations, all `success`, with no fallback series. Selected actions included AggressivePressure 26 `offensive_skill` / 188 `attack` / 526 `approach`; RangedControl 1 `offensive_skill` / 34 `attack` / 1 `approach`; Survival 3 `offensive_skill` / 2 `heal` / 2 `attack`. Gateway created 801 intents and executed 800. All 31 of 32 `CastSkill` intents executed; the single rejection was bounded `skillunavailable`. There were no cooldown, MP, range, geodata, or blocked-movement rejections, no `callSkill() failed`, no dropped wakeups, and no scheduler-execution-failure series. Single-flight collisions were 99 coalesced wakes, not concurrent Think. One `ReturnHome(PreserveThreat)` executed. Mean Strategy evaluation was 20.7 µs for AggressivePressure.

## Manual rollout gate

The development compose configuration now stages `NPC_STRATEGY_MODE=Enabled` for the accepted seven-template Balanced wave, the accepted 12 `AggressivePressure` area templates, and the accepted RangedControl/Survival laboratory controls. The remaining normal ranged templates stay on the Phase 3 pipeline. Crasher therefore remains a useful unmodified control while Undine Noble already exercises RangedControl.

The Balanced and AggressivePressure deployment gates are complete. The remaining required evidence is:

1. enable and validate the four remaining RangedControl area templates (20006 Orc Archer, 20101 Crasher, 20110 Undine, 20113 Undine Elder);
2. observe the complete Talking Island cohort under the same safety gates;
3. switch Strategy back to `Disabled` and verify Phase 3 rollback while retaining the `skillList`, fighter-cadence, range-wakeup, and control-effect corrections;
4. restore the accepted final mode, repeat final Release/tests and R1-R5 evidence, record live results, clean the worktree, then and only then create `npc-brain-phase4-complete`.

No Phase 5 work, live-result commit, or completion tag is authorized before this gate passes.
