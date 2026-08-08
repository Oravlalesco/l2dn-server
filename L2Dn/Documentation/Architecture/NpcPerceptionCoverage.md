# AttackableAI perception coverage matrix

This matrix is the migration checklist for the base `AttackableAI`. Every decision read must stay assigned to one category as the implementation evolves.

| Decision input | Phase 2 source | Status / boundary |
| --- | --- | --- |
| NPC object/template identity, level and legacy AI type | `Identity` | Snapshot |
| Guard, monster, raid, raid-minion, fake-player and movement capabilities | `Identity` / `Physical` | Snapshot |
| Position, heading, collision, HP/MP and movement state | `Physical` | Snapshot |
| In-combat, casting, attacking, disabled/confused flags and attack/aggro ranges | `CombatFacts` | Snapshot |
| Effective current target | `CombatFacts.CurrentTarget` | Snapshot; resolve only for legacy execution/skill APIs |
| Spawn position, instance, region, random-walk and return-to-spawn facts | `Environment` | Snapshot |
| Visible players, creatures, attackables and guards | `VisibleEntities` | Snapshot, preserving observation ordinal |
| Entity kind, life, invocation, zone and relationship facts | `VisibleEntity` | Snapshot |
| Auto-attack/target/assist capability | Pure relation/affordance evaluator | Snapshot fact; never calls side-effecting aggression logic |
| Hate, damage, most-hated selection and aggro enumeration | `Threats` | Snapshot; independent from visible list |
| Current-target/primary-threat line of sight and direct reachability | `SpatialObservations` | Snapshot |
| A position invented during a decision | `INpcGeoQuery` | Transitional dynamic query |
| Skill lists, `Skill` instances, target rules and `SkillCaster.checkUseConditions` | Live skill engine | Transitional until intent/brain phase |
| Brain intention, global aggro, timeouts, chaos counters and recursion guards | `AttackableAI` fields | Brain state; deliberately excluded from perception |
| Fake-player drop collection and item pickup effects | Legacy command boundary | Execution state |
| Clan-call events, minion control and event subscribers | Live event/command boundary | Transitional execution |
| Commands accepting `Creature`, `Skill`, `Item` or `Location` | `ILegacyNpcCommandExecutor` | Transitional execution |
| Entity-key to live-object conversion | Legacy resolver | Execution/skill boundary only; measured |
| Random walking, movement rolls, target changes and random selection | `INpcRandomSource` | Transitional deterministic brain dependency |
| `FriendlyNpcAI` overrides | Live state | Out of Phase 2 snapshot-read scope |
| `ControllableMobAI` overrides | Live state | Out of Phase 2 snapshot-read scope |

## Completion checks

- No direct world or threat query remains in base `AttackableAI` snapshot-enabled paths except explicit live fallback.
- Resolving an entity never permits reading a value already supplied by perception.
- Every remaining live read is listed above as geo, skill, brain state, execution or specialized override.
- Capture-only mode cannot change any authoritative state.
