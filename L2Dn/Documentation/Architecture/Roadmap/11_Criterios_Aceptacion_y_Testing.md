# Criterios de Aceptación y Estrategia de Testing — Revisión 2

Estado: diseño. Fecha: 2026-08-13.

Este documento define qué debe demostrarse en cada fase para considerarla completa, y qué tests lo demuestran. No lista "cosas que pasan"; lista **qué propiedades prueban y por qué cada test es necesario**.

**Revisión 3**: incorpora P0 de tercera auditoría — ADR-017 PolicyArbitrator semántica, CurrentTarget en ObservationV1, Generation no duplicado (ya en NpcKey), StateRevision exacta V1, Policy.Runtime fuera de Brain, Reflex→no evaluation, expert label correcto en 4G.

---

## Filosofía de testing

Un test inteligente demuestra una propiedad del sistema que, si se viola, rompe una garantía de la que depende algo posterior. No existe un test que "pasa por pasar".

Cada test tiene:
- **Propiedad**: qué invariante o comportamiento demuestra
- **Por qué importa**: qué se rompería si esta propiedad se viola
- **Cómo se verifica**: input concreto, output esperado, condición de fallo

Las categorías de test son:

| Categoría | Qué demuestra | Dónde vive |
|---|---|---|
| **Contrato** | Inmutabilidad, serialización, semántica de tipos | `L2Dn.Npc.Contracts.Tests` |
| **Comportamiento** | Que el Brain produce la decisión correcta para un estado dado | `L2Dn.Npc.Brain.Tests` |
| **Integración** | Que los componentes cooperan correctamente dentro del GameServer | `L2Dn.GameServer.Model.Tests` |
| **Arquitectura** | Que las fronteras de dependencia no se violan | Test estático de ensamblado |
| **Rendimiento** | Que no se degradan los SLO existentes | R1-R5, benchmark sintético |
| **Gameplay** | Que el jugador observa el comportamiento esperado | Sesión manual con OTLP |
| **Rollback** | Que se puede volver al estado anterior sin residuo | Deploy + rollback + verificación |

---

## Fase 4B.5 — Stateful Strategic Utility & Stability (rev 2)

### Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4B5-A1 | `NPC_STRATEGY_ADAPTIVE_MODE=Disabled`: same perception + same brain state + same wake reason → same `NpcBrainDecision` → same `NpcIntent` → same Gateway result que Phase 4B (Decision/Intent equivalence) | Comportamiento | Static V1 es baseline exacto |
| 4B5-A2 | NPC `AggressivePressure` con HP<25% y Heal disponible transiciona a `Recover` | Comportamiento | Postura cambia con situación |
| 4B5-A3 | NPC en `Recover` con HP>70% transiciona a `Pressure` o `ControlRange` | Comportamiento | Re-engagement funcional |
| 4B5-A4 | Oscilación HP 34%↔36% NO produce alternancia Recover↔Pressure | Comportamiento | Hysteresis funciona |
| 4B5-A5 | Postura recién adquirida no abandona antes de `MinimumPostureDurationTicks` (excepto Reflex Emergency) | Comportamiento | Commitment funciona |
| 4B5-A6 | `Survival` misma situación: `RecoverUtility >= AggressivePressure.RecoverUtility` | Comportamiento | Style es prior, no jaula |
| 4B5-A7 | Todo cambio de postura registra `NpcPostureTransitionReason` | Contrato | Explicabilidad |
| 4B5-A8 | Replay determinista: misma secuencia → mismas posturas, utilidades, transiciones, directivas, intents | Comportamiento | Reproducibilidad total |
| 4B5-A9 | Dos candidatos con score idéntico → `NpcTieBreakPolicy` (no orden de código) | Contrato | Desempate explícito |
| 4B5-A10 | Strategy V2 P99 < 1 ms, preferiblemente < 0.5 ms | Rendimiento | No degrada hot path |
| 4B5-A11 | Zero network/DB/disk I/O en Strategy evaluation | Rendimiento | Sin I/O en Think |
| 4B5-A12 | Hot-path allocations ≈ V1 | Rendimiento | Sin regresión GC |
| 4B5-A13 | Critical P95 no > +25% vs V1 | Rendimiento | SLO preservado |
| 4B5-A14 | Utility: media ponderada normalizada, resultado 0-1000 sin saturación artificial | Contrato | Información relativa preservada |
| 4B5-A15 | Weights son int 0-1000, cálculo con long accumulator | Contrato | Fixed-point real |
| 4B5-A16 | Reflex Emergency Flee usa SOLO `NpcReflexPolicy.EmergencyFleeHpPercent` (inmutable, resuelta al spawn). No depende de Strategy adaptativa, Neural ni Squad durante Think | Integración | Reflex inmutable durante encarnación |
| 4B5-A16b | `Adaptive Disabled` + `AggressivePressure` produce `NpcReflexPolicy.EmergencyFleeHpPercent = 5%` (idéntico a Phase 4B) | Backward compat | Phase 4B exacta |
| 4B5-A17 | `Neutral` solo fuera de combate; no compite en utility | Contrato | Semántica clara |
| 4B5-A18 | Postura ineligible nunca seleccionada (Disengage con FleeNotAllowed) | Contrato | Hard constraints |
| 4B5-A19 | Shadow V2 mantiene estado longitudinal durante toda la sesión | Comportamiento | Valida hysteresis/commitment en Shadow |
| 4B5-A20 | `MaintainRange` produce movimiento correcto (retroceder/acercar según distancia vs preferred) | Comportamiento | Movement mínimo funcional |
| 4B5-A21 | `Retreat` con todas direcciones bloqueadas → NPC mantiene posición, no se congela | Comportamiento | Fallback de movimiento |
| 4B5-A22 | `NpcUtilityConsideration` y `NpcUtilityCurve` en Brain internal, no en Contracts | Arquitectura | Frontera correcta |

### Tests concretos

**Contracts (`L2Dn.Npc.Contracts.Tests`):**

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `Posture_HasExactlyFiveValues` | Cardinalidad | Enum.GetValues → {Neutral, Pressure, ControlRange, Recover, Disengage} |
| `StrategyDirective_IsImmutable` | Sin mutación | readonly record struct → compilación falla si se modifica |
| `PostureUtilities_IsValueType` | Sin allocation | NpcPostureUtilities es readonly record struct, no class/array |
| `TransitionReason_HasExpectedValues` | Enum completo | ≥ 12 razones documentadas (incluye CombatStarted/CombatEnded) |

**Brain (`L2Dn.Npc.Brain.Tests`):**

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `Disabled_IdenticalToPhase4B` | Baseline exacto | Disabled + 100 percepciones → Decision/Intent equivalence con Phase 4B (ExactMatch en replay comparison) |
| `AggressivePressure_HighHp_Pressure` | Postura correcta | HP=100%, skill ready, in combat → Posture=Pressure |
| `AggressivePressure_LowHp_HealReady_Recover` | Transición | HP=24%, heal ready → Posture=Recover |
| `Survival_SameState_HigherRecoverUtility` | Style prior | Survival vs AP, misma situación → Survival.RecoverUtility ≥ AP.RecoverUtility |
| `Hysteresis_OscillatingHp_StablePosture` | Sin oscilación | HP=[34,36,34,36,34,36,50,70] → máximo 2 transiciones, no 8 |
| `Commitment_MinDuration_Respected` | Permanencia | Recover recién adquirida + HP 60% antes MinDuration → permanece Recover |
| `Commitment_ReflexEmergencyOverrides` | Reflex autoridad | En commitment + HP < EmergencyThreshold → Reflex Emergency Flee |
| `RangeTolerance_NoOscillation` | Tolerancia rango | Distance=[590,610,595,605] con PreferredRange=600±60 → estable |
| `TieBreak_ExplicitPolicy_NotCodeOrder` | Desempate | Attack=100, Skill=100 + PreferOffensive → Attack gana |
| `TieBreak_OrderIndependent` | Desempate | Orden evaluación invertido → mismo resultado |
| `Directive_Recover_IncreasesHealBias` | Biases correctos | Posture=Recover → HealBias > 0, AttackBias < 0 |
| `Neutral_OnlyOutsideCombat` | Semántica | No combat/no target → Neutral; combat started → utility evaluation |
| `Neutral_NeverComputedAsUtility` | Semántica | Utility evaluation no incluye Neutral en candidatos |
| `Disengage_Ineligible_FleeNotAllowed` | Eligibility | CanFlee=false → Disengage.Eligible=false |
| `Ineligible_NeverSelected` | Hard constraint | Disengage utility=950 pero ineligible → no seleccionada |
| `Utility_NeverSaturates` | Normalización | 1000 evaluaciones aleatorias → ningún utility = 1000 por saturación aritmética |
| `Utility_WeightedAverage_CorrectResult` | Cálculo | Input conocido → resultado exacto verificable |
| `Utility_PriorWeightZero_ConfigRejected` | Validación | PriorWeight=0 al startup → excepción / log error, no acepta |
| `Utility_NoActiveConsiderations_ReturnsPrior` | Fallback | Todas considerations weight=0 → utility = prior |
| `Utility_DenominatorNeverZero` | Invariante | 1000 evaluaciones aleatorias → denominador > 0 siempre |
| `Weights_AreIntegers` | Fixed-point | Reflection → todos los weights son int, no double/float |
| `Curve_DuplicateInput_RejectedAtStartup` | Validación | Points [(500,200),(500,600)] → excepción al construir |
| `Curve_UnorderedInput_RejectedAtStartup` | Validación | Points [(300,800),(200,400)] → excepción al construir |
| `Curve_UtilityOutOfRange_RejectedAtStartup` | Validación | Points [(100,-50)] → excepción al construir |
| `Curve_LessThanTwoPoints_RejectedAtStartup` | Validación | Points [(500,200)] → excepción al construir |
| `ReflexFlee_UsesOnlyNpcReflexPolicy` | Separación | ReflexBrain consume NpcReflexPolicy, no strategy.EffectiveFleeHpPercent |
| `ReflexPolicy_AggressivePressure_FleeFivePercent` | Backward compat | AggressivePressure → NpcReflexPolicy.EmergencyFleeHpPercent = 5% (idéntico Phase 4B) |
| `ReflexPolicy_Survival_FleeThirtyPercent` | Backward compat | Survival → NpcReflexPolicy.EmergencyFleeHpPercent = 30% (idéntico Phase 4B) |
| `MaintainRange_TooClose_Retreats` | Movement | Range=300, target a 100 → NPC retrocede |
| `MaintainRange_TooFar_Approaches` | Movement | Range=300, target a 500 → NPC avanza |
| `MaintainRange_InBand_NoMovement` | Tolerance | Range=300±60, target a 310 → no se mueve |
| `Retreat_AllBlocked_MaintainsPosition` | Fallback | Todas direcciones bloqueadas → posición, no freeze |
| `ShadowState_PersistsAcrossThinks` | Longitudinal | Shadow: Think1→Pressure, Think2→estado recuerda Pressure |
| `ShadowState_ValidatesHysteresis` | Longitudinal | Shadow 10 Thinks: V2 muestra transiciones estables |
| `ShadowState_GenerationMismatch_Discarded` | Lifecycle | Shadow con Gen=5, NPC respawned Gen=6 → estado descartado, fresh Neutral |
| `ContextBuilder_NoHeapAllocation` | Performance | Construir context → cero allocations |
| `UtilityEvaluator_SubMillisecond` | Performance | 1000 evaluaciones → P99 < 0.5 ms |

**Temporal (secuencias):**

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `Sequence_GradualHpDrop_SmoothTransition` | Inteligencia temporal | HP=[100→10] gradual → Pressure estable, luego Recover estable |
| `Sequence_HpRecovery_ReEngagement` | Re-engagement | HP=[30→80] tras Recover → transición a Pressure o ControlRange |
| `Sequence_CombatDuration_PostureStable` | Estabilidad | 200 ticks combate estable → máximo 4-5 transiciones |

**Property-based:**

| Test | Propiedad | Verificación |
|---|---|---|
| `AsHpDecreases_RecoverUtilityNeverDecreases` | Monotonía | ∀ hp1 < hp2: RecoverUtility(hp1) ≥ RecoverUtility(hp2) |
| `SurvivalRecover_GEQ_AggressivePressureRecover` | Style prior | ∀ estado: Survival.Recover ≥ AP.Recover |
| `UtilityScores_AlwaysInRange_0_1000` | Acotación | ∀ evaluación: 0 ≤ utility ≤ 1000 |
| `AllCombatPostures_Reachable_FromAllStyles` | No jaulas | ∀ style: ∃ estado que produce cada postura de combate como ganadora |
| `IneligiblePosture_NeverSelected` | Hard constraint | ∀ evaluación: postura ganadora siempre es eligible |

---



## Fase 4C — Style × Role

### Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4C-A1 | `Balanced × Mob` produce exactamente los mismos scores que Phase 4 `Balanced` sin rol | Comportamiento | Que el rol `Mob` es la identidad (delta cero) y no altera el baseline certificado |
| 4C-A2 | Cada combinación `style × role` produce scores deterministas e iguales en 1,000 evaluaciones idénticas | Comportamiento | Que la composición es pura y no introduce estado ni aleatoriedad |
| 4C-A3 | Un perfil con intelligence profile legacy explícito NO hereda un rol no-Balanced a menos que el registro lo declare | Comportamiento | Que scripts y tests existentes no cambian de comportamiento implícitamente |
| 4C-A4 | Una entrada de registro malformada o con rol desconocido emite warning y NO habilita silenciosamente `Balanced:Mob` | Contrato | Que errores de configuración no se esconden detrás de un default seguro |
| 4C-A5 | El rol `Raid` puede componerse en tests pero NO ejecuta a través del Gateway para actores `RaidBoss` | Integración | Que la frontera de eligibilidad no se expande accidentalmente |
| 4C-A6 | `NpcBrainEligibility` rechaza todo actor que no sea exacto `Monster` + exacto `AttackableAI`, independientemente del rol configurado | Integración | Que un Guard con rol Commander sigue en Legacy |
| 4C-A7 | R1-R5 con Strategy Enabled y roles configurados mantiene zero drops, zero overflow, max concurrent 1 | Rendimiento | Que la composición no degrada el scheduler |
| 4C-A8 | Release build sin errores nuevos | Arquitectura | Que no se introdujeron dependencias prohibidas |
| 4C-A9 | Registry es inmutable al startup; NO existe hot override de deltas de rol | Contrato | Que la reproducibilidad no se compromete por live tuning prematuro |
| 4C-A12 | `effectivePosturePrior = Clamp(Style.Prior + Role.PriorDelta, 0, 1000)` — siempre en [0,1000] | Contrato | Que la composición preserva la invariante del weighted average |
| 4C-A13 | `effectiveSwitchMargin = Max(0, ...)` y `effectiveMinDuration = Max(0, ...)` — nunca negativos | Contrato | Que deltas negativos no producen valores inválidos |

### Tests concretos

**Contracts (`L2Dn.Npc.Contracts.Tests`):**

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `RoleDelta_Mob_IsIdentityZero` | Delta Mob no modifica ningún score | `RoleDelta.Mob` → todos los campos == 0 |
| `RoleDelta_AllRoles_AreImmutable` | Deltas no mutan post-construcción | Crear delta, intentar modificar → compilación falla (readonly struct) |
| `RoleEnum_HasExactlyFiveValues` | Cardinalidad acotada para telemetría | `Enum.GetValues<NpcStrategyRole>()` → exactamente {Mob, Elite, Minion, Commander, Raid} |
| `RoleComposition_PriorNeverBelowZero` | Invariante prior | ∀ style × role × posture: effectivePosturePrior ≥ 0 |
| `RoleComposition_PriorNeverAbove1000` | Invariante prior | ∀ style × role × posture: effectivePosturePrior ≤ 1000 |
| `RoleComposition_DurationAndMarginNeverNegative` | Invariante commitment | ∀ role: effectiveSwitchMargin ≥ 0 AND effectiveMinDuration ≥ 0 |
| `TacticalScore_AlwaysInRange_0_1000` | Invariante tactical | ∀ style × role × posture × action: effectiveTacticalScore ∈ [0, 1000] |
| `TacticalScore_NegativeBias_ClampsToZero` | Clamp inferior | Attack base=60 + directiveBias=-150 → effectiveScore = 0, no -90 |

**Brain (`L2Dn.Npc.Brain.Tests`):**

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `Compose_Balanced_Mob_EqualsPhase4Balanced` | Identity: rol Mob no altera baseline | Perception + Balanced + Mob → decisión idéntica a Phase 4 Balanced sin rol |
| `Compose_AggressivePressure_Elite_ProducesExpectedScores` | Suma aritmética correcta | AP base + Elite delta → scores esperados verificables |
| `Compose_Survival_Commander_FleeScoreNeverNegative` | Scores negativos no rompen Tactical | Survival(Flee=115) + Commander(Flee=−25) + edge → Flee score ≥ 0 |
| `Compose_AllStyles_AllRoles_Deterministic_1000x` | Determinismo | Cada combinación × 1000 evaluaciones idénticas → output bit-a-bit idéntico |
| `Compose_ExplicitLegacyProfile_DefaultsToBalancedMob` | No herencia implícita | Intelligence profile legacy sin registro de Strategy → usa Balanced scores |
| `Registry_MalformedEntry_WarnsAndRejects` | Config inválida no se esconde | `"20130:invalid:mob"` → warning, template NO en registro |
| `Registry_UnknownRole_WarnsAndRejects` | Rol desconocido no habilita Balanced | `"20130:aggressive_pressure:tank"` → warning, template NO aparece |
| `Registry_DuplicateTemplate_LastWinsWithWarning` | Duplicados explícitos | Dos entradas para template 20130 → último gana, warning emitido |
| `Registry_IsImmutableAfterStartup` | Sin hot override | Intentar modificar registry post-startup → excepción o no-op |

**GameServer.Model (`L2Dn.GameServer.Model.Tests`):**

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `Eligibility_Guard_WithCommanderRole_StaysLegacy` | Actor boundary no se expande | Guard + registro commander → `UsesIntentBrain == false` |
| `Eligibility_RaidBoss_WithRaidRole_StaysLegacy` | RaidBoss no entra en Intent | RaidBoss + rol Raid → `UsesIntentBrain == false` |
| `Eligibility_BaseMonster_WithEliteRole_UsesIntent` | Monster base sí puede tener rol | Monster + rol Elite → `UsesIntentBrain == true` |
| `RoleTelemetry_EmitsExactlyFiveCardinalityValues` | Cardinalidad acotada | Evaluaciones con todos los roles → tags solo contienen 5 valores |

---

## Fase 4D — Policy Foundation

### Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4D-A1 | `TacticalActionEvaluator` refactorizado: `BuildCandidates()` produce `NpcTacticalCandidateSet` y `SelectCandidate()` es separado | Arquitectura | Que existe una sola fuente de verdad para eligibilidad de acciones |
| 4D-A2 | `NpcTacticalCandidateSet` con candidatos Disabled produce misma decisión que Phase 4C directamente | Comportamiento | Que la refactorización no altera comportamiento |
| 4D-A3 | `PolicyArbitrator` con mode=Disabled ignora advice y selecciona candidato por score determinista | Comportamiento | Que Disabled = transparente |
| 4D-A4 | `NpcPolicyAdvice` rechazado si `NpcKey` no coincide (NpcKey incluye Generation, no se duplica) | Contrato | Que un NPC respawneado no ejecuta advice de su encarnación anterior |
| 4D-A5 | `NpcPolicyAdvice` rechazado si `BasedOnStateRevision != CurrentStateRevision` (exacto en V1) | Contrato | Que advice de estado obsoleto no se aplica. Regla estricta para V1 |
| 4D-A6 | `NpcPolicyObservationV1` NUNCA contiene ObjectId, PlayerId, ni ningún identificador técnico | Contrato | Que la red nunca puede aprender identidad técnica |
| 4D-A7 | `NpcPolicyObservationV1` incluye CurrentTarget state (HpRatio, Distance, IsCasting, etc.) sin identidad | Contrato | Que la red tiene suficiente información para decidir |
| 4D-A8 | V1 decide tipo de acción (BasicAttack, Approach, OffensiveSkill, Heal, Flee, Reposition), NO target ni skill concreta | Contrato | Que V1 tiene alcance acotado y crecimiento progresivo |
| 4D-A9 | Reflex Brain produce la misma decisión con policy Disabled, Enabled, o error de policy | Comportamiento | Que Reflex NUNCA depende de la policy |
| 4D-A10 | `L2Dn.Npc.Brain` sigue referenciando SOLO `L2Dn.Npc.Contracts` | Arquitectura | Que la frontera no se viola |
| 4D-A11 | Fallback determinista es invariante: no existe config que lo desactive | Arquitectura | ADR-014 rev 2 |
| 4D-A12 | PolicyArbitrator implementa Opción A: neural elige directamente entre candidatos elegibles | Arquitectura | ADR-017 |
| 4D-A13 | Brain NO invoca `INpcPolicy`; solo consume `NpcPolicyAdvice?` inyectado | Arquitectura | Elimina dependencia síncrona de ONNX |
| 4D-A14 | `NpcPolicyInferenceCoordinator` y `NpcPolicyAdviceStore` viven en `L2Dn.Npc.Policy.Runtime`, no en Brain | Arquitectura | Brain no conoce cómo se obtiene el advice |
| 4D-A15 | Reflex Intent != null → no se genera policy evaluation para ese Think | Comportamiento | Reduce inferencias innecesarias |

### Tests concretos

**Contracts:**

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `ObservationV1_NoObjectIds` | Sin identidad técnica | Reflection sobre campos → ningún ObjectId |
| `ObservationV1_IsImmutable` | Sin mutación | Modificar campo → fallo de compilación (readonly struct) |
| `ObservationV1_HasCurrentTargetState` | Target info suficiente | Campos: HasTarget, DistanceNormalized, IsCasting, IsMoving, IsDisabled, RelativeAngle, ThreatRatio, WithinPhysicalRange (sin HpRatio en V1) |
| `CandidateSet_ContainsEligibilityAndReason` | Información completa | Cada candidato tiene score, eligible, ineligibilityReason |
| `Advice_NpcKeyMismatch_Rejected` | Causalidad | Advice con NpcKey.ObjectId=100/Gen=5, NPC con NpcKey.ObjectId=100/Gen=6 → rechazado (NpcKey incluye Generation) |
| `Advice_StateRevisionMismatch_Rejected` | Causalidad V1 estricta | Advice con StateRevision=100, NPC en StateRevision=101 → rechazado (exacto, no delta) |
| `Advice_ExpiresAtPast_IsStale` | Expiración | ExpiresAt=100, tick=101 → `IsStale == true` |
| `PolicyVersion_ChecksumMismatch_Rejected` | Modelo corrupto | Checksum alterado → `Rejected(ChecksumMismatch)` |
| `AdviceV1_NoPreferredTarget` | V1 sin target selection | NpcPolicyAdvice V1 no contiene campo PreferredTarget/Slot |
| `AdviceV1_NoSeparateGeneration` | Sin duplicación | NpcPolicyAdvice no contiene campo Generation separado (ya está en NpcKey) |

**Brain:**

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `CandidateSet_Phase4Parity_AllScenarios` | Transparencia | S1-S9: BuildCandidates + SelectCandidate == TacticalActionEvaluator original |
| `PolicyArbitrator_Disabled_UsesDeterministic` | Disabled transparente | Mode=Disabled → PolicyArbitrator ignora advice → argmax(deterministic scores) |
| `PolicyArbitrator_Enabled_NeuralSelectsFromEligible` | ADR-017 Opción A | Enabled + neural logits → softmax → candidato elegible seleccionado |
| `PolicyArbitrator_NeuralLogit_IneligibleCandidate_Ignored` | Neural solo toca elegibles | Logit máximo para Heal, pero Heal ineligible → Heal nunca seleccionado |
| `StaleAdvice_FallsBackToDeterministic` | Fallback funcional | Advice con StateRevision != Current → PolicyArbitrator usa argmax(deterministic) |
| `PolicyError_FallsBackToDeterministic` | Tolerancia a fallos | Policy con excepción → fallback → decisión válida |
| `ReflexBrain_IgnoresPolicy_TargetDead` | Reflex independiente | Target muerto + advice "Attack" → Reflex emite ClearTarget, ignora advice |
| `ReflexBrain_IgnoresPolicy_OutsideLeash` | Reflex independiente | Fuera de leash + advice "Approach" → Reflex emite ReturnHome |
| `ReflexIntent_SkipsPolicyEvaluation` | Eficiencia | Reflex produce Intent → no se encola policy evaluation para este Think |
| `Brain_NeverInvokesINpcPolicy` | Frontera | L2Dn.Npc.Brain assembly → reflection: no referencia a INpcPolicy.Evaluate ni similar |
| `InferenceCoordinator_InPolicyRuntime` | Ubicación | NpcPolicyInferenceCoordinator vive en L2Dn.Npc.Policy.Runtime, no en Brain |
| `ObservationExtraction_NoHeapAllocation` | Performance | Extraer observation → cero allocations (benchmark) |

---

## Fase 4D.5 — Tactical Movement Primitives

### Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4D5-A1 | `FlankTarget(Left)` produce movimiento al lado izquierdo del target, validado por GeoEngine | Comportamiento | Que el intent semántico se materializa en movimiento real |
| 4D5-A2 | Si GeoEngine rechaza TODAS las posiciones, el NPC mantiene su posición sin congelarse | Integración | Que el fallback de movimiento funciona |
| 4D5-A3 | `MaintainRange(300)` con target a 100 → NPC retrocede; con target a 500 → NPC avanza | Comportamiento | Que MaintainRange funciona bidireccional |
| 4D5-A4 | `FormationSlot` con slot asignado por SquadDirective produce movimiento correcto | Integración | Que Squad + Movement se integran |
| 4D5-A5 | Brain NUNCA produce coordenadas absolutas | Arquitectura | Que la frontera se mantiene |

### Tests concretos

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `FlankLeft_MovesToLeftOfTarget` | Posicionamiento | FlankTarget(Left, 150) + target en (100,100) → NPC se mueve a posición izquierda validada |
| `FlankLeft_GeoBlocked_TriesAlternatives` | Resiliencia | Posición izquierda bloqueada → prueba ±30°, ±60° → alternativa |
| `FlankLeft_AllBlocked_MaintainsPosition` | Fallback | Todas bloqueadas → NPC no se mueve, no se congela |
| `MaintainRange_TooClose_Retreats` | Rango bidireccional | Range=300, target a 100 → NPC retrocede |
| `MaintainRange_TooFar_Approaches` | Rango bidireccional | Range=300, target a 500 → NPC avanza |
| `FormationSlot_CorrectPosition` | Integración Squad | SquadDirective con slots → NPC va al slot asignado |
| `Brain_NeverProducesAbsoluteCoords` | Frontera | Inspeccionar TacticalMovementIntent → no contiene coordenadas X,Y,Z absolutas |

---

## Fase 4E — Squad Intelligence Foundation

### Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4E-A1 | Escuadrón desde Master + MinionList contiene exactamente los miembros que `MinionList` reporta | Integración | Que SquadFactory no inventa ni pierde miembros |
| 4E-A2 | `SquadSnapshot` refleja correctamente: HP promedio, composición viva/muerta, estado del commander, centroide | Contrato | Que el snapshot de escuadrón es correcto |
| 4E-A3 | `SquadThinkCoordinator` (en GameServer.Model) garantiza `MaxConcurrent = 1` bajo carga concurrente | Comportamiento | Que single-flight aplica a escuadrones |
| 4E-A4 | Muerte del Commander produce cambio de directiva observable (Retreat o FreeAgent) | Comportamiento | Que el SquadBrain reacciona a eventos críticos |
| 4E-A5 | NPC sin SquadDirective se comporta exactamente como Phase 4C | Integración | Que ausencia de squad = fallback limpio |
| 4E-A6 | `NpcCombatAssignment` modifica scores de `PolicyArbitrator` de forma consistente | Comportamiento | Que Frontline/RangedPressure/ProtectSupport producen diferencias observables |
| 4E-A7 | SquadBrain no modifica directamente ningún estado del GameServer | Arquitectura | Que solo produce directivas |
| 4E-A8 | `NPC_SQUAD_MODE=Disabled` produce comportamiento Phase 4C exacto | Comportamiento | Que el rollout modal funciona |
| 4E-A9 | `SquadBrainState` es `internal` en Brain, no público en Contracts | Arquitectura | Que estado privado no se expone |
| 4E-A10 | SquadDirective contiene causal metadata (SquadKey, SquadGeneration, MembershipRevision) | Contrato | Que un minion respawneado no ejecuta directiva de su encarnación anterior |

### Tests concretos

**Contracts:**

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `SquadSnapshot_IsImmutable` | Sin mutación | Modificar SquadSnapshot → fallo |
| `SquadDirective_IsImmutable` | Sin mutación | Modificar SquadDirective → fallo |
| `CombatAssignment_HasExpectedValues` | Enum completo | Exactamente 9 valores esperados |
| `SquadSnapshot_DeadMember_ReflectedInComposition` | Snapshot correcto | 5 miembros, 2 muertos → Alive=3, Dead=2 |
| `SquadSnapshot_CentroidCalculation_Correct` | Geometría correcta | 3 posiciones conocidas → centroide = promedio |
| `SquadDirective_CausalMetadata_AllFieldsPresent` | Causalidad | SquadKey, SquadGeneration, MembershipRevision, DirectiveSequence presentes |
| `SquadDirective_GenerationMismatch_Rejected` | Causalidad | Directive con SquadGeneration=3, squad actual Generation=4 → rechazado |

**Brain:**

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `SquadBrain_CommanderDied_EmitsRetreatOrRegroup` | Reacción a pérdida | CommanderAlive=false → Objective ∈ {Retreat, Regroup}; NPCs individuales pueden recibir Assignment=FreeAgent si squad se disuelve |
| `SquadBrain_AllMembersHealthy_NoFormationChange` | Estabilidad | Contexto estable → directiva no cambia |
| `SquadBrain_SupportThreatened_EmitsProtectDirective` | Protección | Support bajo ataque → al menos un ProtectSupport |
| `SquadBrain_NumericalDisadvantage_EmitsRegroup` | Adaptación | 2/5 vivos vs 4 enemigos → Regroup o Retreat |
| `SquadThink_SingleFlight_UnderConcurrency` | Concurrencia | 1000 wakeups simultáneos → MaxConcurrent=1, zero drops |
| `Assignment_Frontline_IncreasesApproachScore` | Efecto observable | Con/sin Frontline → Approach score mayor con Frontline |
| `NoSquad_FallbackToIndividualBrain` | Degradación limpia | Sin SquadDirective → misma decisión que Phase 4C |
| `SquadMode_Disabled_Phase4CExact` | Rollout modal | NPC_SQUAD_MODE=Disabled → comportamiento Phase 4C exacto |
| `SquadBrainState_IsInternal` | Frontera | `SquadBrainState` no es accesible fuera de Brain assembly |

**GameServer.Model:**

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `SquadFactory_FromMinionList_CorrectMembers` | Mapeo fiel | Monster + 4 minions → Squad con 5 members |
| `SquadFactory_EmptyMinionList_NoSquadCreated` | Sin squad vacío | Sin minions → no se crea Squad |
| `SquadFactory_ClanHelp_NotCreated_InPhase4E` | Solo master/minion | Clan help cluster → no se crea Squad (clan/faction squads son fase futura) |
| `SquadManager_AllMembersDead_SquadDissolved` | Limpieza | Todos mueren → Squad removido |
| `SquadThinkCoordinator_InGameServerModel` | Ubicación | SquadThinkCoordinator vive en L2Dn.GameServer.Model, no en Brain |

---

## Fase 4F-A — Dataset + Behavior Cloning

### Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4FA-A1 | La captura de replay NO degrada los TPS del GameServer más de un 2% | Rendimiento | Que el sistema de captura es verdaderamente pasivo |
| 4FA-A2 | Replay guarda hechos crudos (HP delta, damage dealt/received, ally death, etc.), NO reward calculado | Contrato | Que se puede cambiar la función de recompensa sin re-capturar |
| 4FA-A3 | `FeatureExtractor` usa el MISMO código que produce `NpcPolicyObservationV1` en producción (no reimplementa) | Arquitectura | Que no hay Train/Serve Skew |
| 4FA-A4 | El pipeline `train_imitation.py` converge con dataset sintético | Comportamiento | Que el modelo puede aprender |
| 4FA-A5 | `export_onnx.py` produce `.onnx` cargable por ONNX Runtime C# | Integración | Que el puente Python↔C# funciona |
| 4FA-A6 | Evaluación reporta per-class precision, recall, F1 y confusion matrix (no solo accuracy global) | Contrato | Que no hay class imbalance oculto |
| 4FA-A7 | Dataset split por Episode completo (no steps aleatorios del mismo combate) | Contrato | Que no hay data leakage entre train/test |
| 4FA-A8 | Hold-out templates verifica generalización a mobs no vistos en training | Contrato | Que el modelo no solo memoriza |
| 4FA-A9 | `NpcTrainingReward` vive en `L2Dn.Npc.Training.Contracts`, NO en `L2Dn.Npc.Contracts` | Arquitectura | Que el GameServer no conoce cómo entrenamos |

### Tests concretos

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `FeatureVector_SameDimensionAsProduction` | Sin Train/Serve Skew | Mismo NpcPerception → mismo vector en producción y en export |
| `FeatureVector_NoNaN_NoInf` | Datos limpios | 10,000 percepciones → cero NaN, cero Inf |
| `Episode_RawFacts_NoRewardField` | Reward offline | Episodio grabado → no contiene campo Reward |
| `RewardFunction_CanBeChanged_WithoutReCapture` | Flexibilidad | Mismo episodio + RewardFunction v1 → reward X; RewardFunction v2 → reward Y |
| `Episode_ActionMask_ConsistentWithAction` | Consistencia | Para cada step: si candidato[Heal].eligible=false → action_chosen ≠ Heal |
| `Replay_Capture_DoesNotBlockThink` | No bloquea | Queue llena → Think continúa, step descartado con telemetría |
| `ONNX_Export_LoadableInCSharp` | Interop | model.onnx → `new InferenceSession(path)` → sin excepción, shapes correctas |
| `ImitationLearning_LossDecreases` | Aprendizaje | 1000 episodes, 100 epochs → loss final < loss inicial × 0.5 |
| `Evaluation_PerClass_Metrics` | Sin class imbalance oculto | Evaluación reporta precision/recall/F1 por acción, no solo accuracy global |
| `Dataset_SplitByEpisode_NoLeakage` | Sin data leakage | Ningún step de un episodio train aparece en test set |
| `HoldoutTemplates_Generalization` | Generalización | Templates no vistos en training → accuracy > threshold |

---

## Fase 4G — Neural Policy Shadow

### Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4G-A1 | ONNX Runtime en `L2Dn.Npc.Policy.Onnx`, NO en `L2Dn.Npc.Brain` | Arquitectura | Brain mantiene propiedad de solo depender de Contracts |
| 4G-A2 | Modelo cargado al startup, en memoria, CERO lecturas de disco durante Think | Rendimiento | Sin I/O en hot path |
| 4G-A3 | Inferencia neural NO aparece en Critical reaction path: Reflex responde ANTES de completar inferencia | Comportamiento | Neural Policy es advisory, no blocking |
| 4G-A4 | Shadow comparison usa PolicyEvaluationId pareado (no compara contra Think actual) | Comportamiento | Que la comparación es causal y correcta |
| 4G-A5 | Cero inferencias alteran gameplay: TODAS las decisiones ejecutadas son del pipeline determinista | Comportamiento | Shadow es realmente shadow |
| 4G-A6 | Si `OnnxNpcPolicy` falla, NPC usa determinista sin interrupción | Integración | Fallback funciona en condiciones reales |
| 4G-A7 | Memory stable después de 24h de inferencia continua | Rendimiento | Sin memory leak |

### Tests concretos

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `OnnxPolicy_InSeparateAssembly` | Frontera | L2Dn.Npc.Brain.csproj no referencia Microsoft.ML.OnnxRuntime |
| `OnnxPolicy_LoadsAtStartup_NoLaterDiskRead` | Sin I/O en hot path | Mock filesystem → cero lecturas durante 1000 inferencias |
| `OnnxPolicy_Exception_FallsBackCleanly` | Tolerancia | OrtException → fallback → decisión válida |
| `Shadow_NeverExecutesNeuralDecision` | Shadow puro | 1000 ciclos → TODAS intents del pipeline determinista |
| `Shadow_PairedEvaluationId_Comparison` | Causalidad | Solicitud con ID=123 + ExpertAction → respuesta con ID=123 → compara correctamente |
| `Shadow_ComparisonNeverUsesCurrentThink` | Causalidad | Shadow comparison usa expert action guardada, no decisión del Think actual |
| `Reflex_RespondsBeforeInference` | Independencia | Target muere + inferencia pendiente → ClearTarget sin esperar |
| `OnnxRuntime_NoManagedLeak_1000Inferences` | Sin memory leak | 1000 inferencias → GC.GetTotalMemory estable ±5% |

---

## Fase 4H — Stochastic Policy Enabled

### Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4H-A1 | Con seed determinista (Hash-based PRNG), misma secuencia de observaciones produce misma secuencia de decisiones | Comportamiento | Reproducibilidad para replay/debug |
| 4H-A2 | Con NpcDecisionSeed diferente, NPC exhibe variabilidad observable | Comportamiento | Que el sampling produce diversidad perceptible |
| 4H-A3 | Un candidato inelegible en CandidateSet NUNCA es seleccionado por el sampler, independientemente de temperature | Contrato | Que CandidateSet eligibility es barrera hard |
| 4H-A4 | Temperature 0.01 produce >98% decisiones = argmax | Comportamiento | Que temperature baja converge a determinismo |
| 4H-A5 | Temperature 1.0 produce distribución igual a softmax(logits original) | Comportamiento | Que T=1 conserva la distribución original (entropía depende de los logits, no siempre es máxima) |
| 4H-A6 | Gateway rejection ratio con Neural Enabled < 5% diferencia absoluta vs Deterministic | Integración | Que la policy neural no genera intent storms |
| 4H-A7 | `NpcAiDifficultyTier` (NO SkillLevel) solo controla: temperature, neural influence, advice refresh, action variability | Contrato | Que no se prometen capacidades que esta fase no implementa |

### Tests concretos

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `Sampler_DeterministicSeed_Reproducible` | Reproducibilidad | NpcDecisionSeed=Hash(NpcKey,Gen,Seq,Version) × 100 → misma secuencia las 100 veces |
| `Sampler_DifferentNpcKey_DifferentOutput` | Variabilidad | Seeds distintas (NpcKey diferente) × 100 → al menos 2 acciones distintas |
| `Sampler_IneligibleCandidate_NeverSelected` | CandidateSet barrera | Candidato Heal inelegible + logit máximo + 10,000 samples → cero Heal |
| `Sampler_Temperature001_AlmostDeterministic` | Control temp | T=0.01 × 1,000 → >98% argmax |
| `Sampler_Temperature1_PreservesOriginalDistribution` | Control temp correcto | T=1.0 → distribución == softmax(logits) (verificar KL-divergence < ε) |
| `DifficultyTier_Novice_HighTemperature` | Mapeo correcto | NpcAiDifficultyTier.Novice → temperature ≥ 0.7 |
| `DifficultyTier_Legendary_LowTemperature` | Mapeo correcto | NpcAiDifficultyTier.Legendary → temperature ≤ 0.1 |
| `DifficultyTier_DoesNotPromiseLookahead` | Sin promesas falsas | NpcAiDifficultyTier no tiene campo Lookahead ni Coordination |
| `GatewayRejections_NeuralVsDeterministic_Comparable` | Sin intent storms | 1000 ciclos cada → rejection delta < 5% |

---

## Fase 4F-B — Headless Combat Simulator

### Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4FB-A1 | El simulador implementa función de transición: `S(t) + A(t) → S(t+1)` | Comportamiento | Que es un environment real para RL, no solo replay |
| 4FB-A2 | El simulador reutiliza la mayor cantidad posible de lógica del GameServer real | Arquitectura | Que se minimiza sim-to-real gap |
| 4FB-A3 | Gymnasium/RLlib env wrapper funcional | Integración | Que se puede conectar con frameworks de RL estándar |
| 4FB-A4 | El simulador es determinista con seed fija | Comportamiento | Que los experimentos son reproducibles |

### Tests concretos

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `Simulator_TransitionFunction_Works` | RL viable | Estado + acción → nuevo estado diferente y plausible |
| `Simulator_DeterministicWithSeed` | Reproducibilidad | Seed=X + misma secuencia de acciones → misma trayectoria |
| `Simulator_GymnasiumEnv_ResetStep` | Compatibilidad | env.reset() → observation; env.step(action) → obs, reward, done, info |
| `Simulator_GameServerLogicReused` | Sin sim-to-real gap | Damage calculation usa misma fórmula que GameServer real |

---

## Fase 4I-A — MARL / CTDE Individual Policies

### Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4IA-A1 | Cada NPC recibe su percepción local + aliados cercanos observables + CombatAssignment, NO información privilegiada global | Arquitectura | CTDE: ejecución descentralizada |
| 4IA-A2 | NPC PUEDE percibir aliados cercanos (HP, distancia, estado) si están en su percepción local | Comportamiento | Que CTDE no impide percepción local de aliados |
| 4IA-A3 | Parameter sharing escala: 12 NPCs con misma policy + seeds diferentes → coordinación pero no uniformidad | Comportamiento | Que shared policies funcionan sin roboticismo |
| 4IA-A4 | Self-play usa opponent pool (no solo current vs current) | Contrato | Que el entrenamiento es robusto contra oscilación |
| 4IA-A5 | Shared vs separate policies es un experimento, no un contrato fijo | Contrato | Que la decisión se toma con datos, no a priori |

### Tests concretos

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `CTDE_AgentInput_HasLocalAllyInfo` | Percepción local | Input tensor contiene ally_distance, ally_hp para aliados cercanos |
| `CTDE_AgentInput_NoGlobalState` | Sin info privilegiada | Input no contiene posiciones/HP de agentes fuera de percepción |
| `SharedPolicy_SameInputDifferentSeed_DifferentOutput` | Diversidad | Misma policy + seeds distintas → acciones diferentes |
| `SharedPolicy_SameInputSameSeed_SameOutput` | Reproducibilidad | Misma policy + misma seed → misma acción |
| `SelfPlay_OpponentPool_NotJustCurrent` | Robustez | Training usa pool de ≥3 opponents (v1, v2, deterministic) |

---

## Fase 4I-B — Learned Squad Policy

### Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4IB-A1 | Neural SquadBrain produce directivas coherentes y no degenera en oscilación | Comportamiento | Que la policy de squad aprendida funciona |
| 4IB-A2 | Squad neural que pierde comunicación degrada a comportamiento individual Phase 4C | Integración | Fallback a nivel de squad |
| 4IB-A3 | Rewards no producen comportamiento degenerado | Comportamiento | Que rewards incentivan gameplay |

### Tests concretos

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `NeuralSquad_Directives_NotOscillating` | Coherencia | 100 ticks → directiva no cambia más de 5 veces |
| `NeuralSquad_Failure_FallbackToIndividual` | Degradación | Policy throws → NPCs individuales actúan |
| `FormationReward_DoesNotCauseInaction` | Reward sano | Reward de formación + enemigos → squad ATACA |

---

## Fases 5-7 (visión)

Las fases de visión no tienen tests detallados definidos todavía. Se definirán cuando las fases previas establezcan los patrones y las métricas baseline. Cada fase heredará los gates invariantes más:

| Gate invariante | Aplica a |
|---|---|
| Zero drops en el scheduler | Todas |
| Single-flight por NPC y por Squad | Todas |
| Reflex independiente de neural | Todas |
| Rollback con un flag | Todas |
| Release build sin errores nuevos | Todas |
| Brain solo referencia Contracts | Todas |
| Sin I/O en Think | Todas |
| Model version observable | 4G+ |
| CandidateSet como única fuente de eligibilidad | 4D+ |
| Causalidad en Advice y Directive | 4D+ |
| Remote devuelve Advice, nunca Intent | 5+ |
| Raid: sin hot-switch a Legacy mid-encounter | 6+ |
| WorldDirector: Policy Gate con límites | 7+ |

---

## Correcciones de tests de la revisión 1

| Test rev 1 (incorrecto) | Corrección rev 2 | Razón |
|---|---|---|
| `Sampler_Temperature10_HighEntropy` con T=1.0 → "entropía máxima" | `Sampler_Temperature1_PreservesOriginalDistribution` → softmax(logits) | T=1.0 conserva distribución original; entropía depende de logits |
| `EncounterBrain_Failure_FallbackToLegacy` | `EncounterBrain_Failure_FallbackToDeterministicPolicy` | Hot-switch a Legacy mid-encounter es peligroso |
| `CTDE_AgentInput_NoGlobalState` sin info de aliados | Permite percepción local de aliados cercanos | CTDE no impide ver aliados; impide info privilegiada global |

---

## Cómo evoluciona este documento

Este documento se actualiza ANTES de implementar cada fase. Los tests propuestos se convierten en tests reales durante la implementación. Los criterios marcados como "a calibrar" se calibran con datos reales y se fijan como hard gates una vez establecidos.

Si un test pasa sin probar la propiedad que dice probar, el test está mal escrito y debe corregirse. Un test verde que no demuestra nada es peor que no tener test: da falsa confianza.
