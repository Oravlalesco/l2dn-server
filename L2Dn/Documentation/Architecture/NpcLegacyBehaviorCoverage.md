# NPC legacy behavior coverage

This matrix prevents Phase 3 from treating `AttackableAI` as a single opaque unit. Each behavior is assigned an owner and migrated as an independently testable vertical. The new Brain replaces decision policy; it does not copy authoritative world mechanics out of GameServer.

## Ownership rule

| Concern | Permanent owner |
|---|---|
| target selection, hate preference, approach, attack choice, flee, return decision | NPC Brain |
| reputation, zones, attack permission, lifecycle, distance, line of sight, geodata, movement, damage, cooldowns, skill execution | GameServer / Intent Gateway |
| specialized raid, minion, scripted, controllable, and quest behavior until explicitly migrated | Legacy AI |

`Legacy` and `Intent` may coexist by NPC profile, but only one path executes decisions for a particular NPC. `Shadow` compares both while only Legacy executes.

## Base AttackableAI compatibility matrix

| Legacy behavior | Current status | Evidence / next gate |
|---|---|---|
| clear target, hate, and attackers on death/respawn | Migrated and gameplay-verified | generation/lifecycle tests plus DVC respawn pass |
| proactive aggressive-mob acquisition | Migrated and gameplay-verified | exact aggro-boundary wake and proximity pass |
| negative-reputation guard acquisition in cities | Migrated and gameplay-verified | GameServer `AutoAttackable` authority and 500-unit guard boundary |
| guard retaliation and nearby/cross-group guard assistance | Legacy by design; focused gameplay retest required | post-hate `AllyAttacked` wake plus legacy `thinkAttack()` clan/faction call; Guard is excluded from Phase 3 Intent eligibility |
| guard pursuit expiry and return-home reset | Legacy hardened; focused gameplay retest required | timeout now aborts attack/follow, clears target/hate/attackers, and returns immediately; `l2dn.npc.guard.pursuit_reset` records the transition |
| select the currently most hated visible valid target | Migrated and gameplay-verified | deterministic higher/lower-hate tests, focused two-player retarget pass, and post-damage `ThreatChanged` wake |
| target death/disappearance | Migrated and gameplay-verified | typed clear-target path and teleport/target-loss pass |
| combat leash and combat-memory reset | Migrated and gameplay-verified | return-home intent and authoritative spawn distance |
| ignore passive spectators while returning with `MOVE_TO` | Intentionally preserved | legacy does not run `thinkActive()` until arrival |
| interrupt return when directly attacked | Migrated and gameplay-verified | `DefensiveReturn`, two-minute renewable timeout, no leash extension |
| defend while returning without outward kiting | Migrated and gameplay-verified | in-range action, `TowardSpawnOnly` movement, and `PreserveThreat` return; OTLP 75/75 and 20/20 executed |
| ranged kiting at the soft leash | Migrated and gameplay-verified | fixed 20-second grace, non-renewable deadline, and hard boundary |
| repeated soft-leash excursions | Migrated and gameplay-verified | third crossing emits authoritative teleport/reset; OTLP 2/2 executed |
| retarget while already returning | Migrated and gameplay-verified | `PreserveMovement` acquisition plus Gateway return fallback prevents an idle limbo |
| reacquire an eligible hostile at the spawn after arrival | Intentionally preserved | legacy changes to `ACTIVE` on arrival and evaluates aggro again |
| fresh acquisition range and line of sight | Migrated and gameplay-verified | 3D range in Brain plus authoritative Gateway revalidation |
| physical approach and attack | Migrated and gameplay-verified | stopping-range, follow cancellation, and attack tests |
| basic deterministic offensive/heal skill choice | Partially migrated | only the base tactical subset is certified |
| attack timeout/corpse camping policy | Partially migrated | `DefensiveReturn` uses the 1,200-tick duration; normal combat and corpse/teleport branches remain pending |
| ten-second post-spawn global aggro delay | Audit pending | `_globalAggro` has no final Brain-state equivalent yet |
| random combat-memory decay | Audit pending | requires deterministic random source and replay cases |
| random walking and active buffs | Legacy pending | keep out of the combat vertical until separately migrated |
| faction calls, minion leader/help behavior | Legacy pending | migrate as squad/commander verticals |
| raid/grand-boss chaos retargeting | Legacy pending | belongs to the raid vertical |
| complete skill scopes and target reconsideration | Legacy pending | healer/buffer/control/cancel/suicide policies need separate profiles |
| fake-player pickup and fake-player aggression | Legacy pending | separate compatibility profile |
| walker, controllable, derived, and scripted AI | Legacy by design | not eligible for exact-base `AttackableAI` Intent execution |

## Gameplay interpretation

The following observed sequence is legacy-equivalent and must not be "fixed" as a generic reacquisition feature:

1. A mob loses its target or exceeds its leash.
2. Combat memory is cleared and the mob receives a return `MOVE_TO`.
3. A nearby spectator does not interrupt that return.
4. A real attack or aggression event may interrupt it.
5. On arrival, the AI becomes active and may acquire any eligible player still inside its aggro range.

The two-character combat case is different: while fighting, a mob must continuously prefer the highest valid hate entry. A new attacker who wins hate must be able to replace the current target. This is a Brain policy and is covered independently from scheduler wake-up correctness.

## Migration gate per behavior

Every row moved from Legacy to Migrated requires:

1. an immutable perception fact or GameServer affordance;
2. a deterministic Brain test;
3. authoritative Gateway revalidation where execution is involved;
4. replay/Shadow comparison when the legacy command is comparable;
5. a focused gameplay scenario and rollback check;
6. OTLP verification for decisions, rejections, queue drops, and scheduler failures.
