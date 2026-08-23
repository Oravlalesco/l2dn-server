# Plan de Implementación — Fase 4B.5 (Stateful Strategic Utility)

Estado: **plan de implementación**. Fecha: 2026-08-23.
Fuente de diseño: <code>Fases/01_Fase_4B5_Stateful_Strategic_Utility.md</code>. Criterios de aceptación: <code>04_Criterios_Aceptacion_y_Testing.md</code> §4B.5 (4B5-A1…A22).

Este documento NO repite el diseño; lo convierte en una **secuencia de ejecución** con archivos, tipos, tests y verificación por subfase. Cada subfase corresponde a un issue del backlog <code>03_Backlog_Issues_Por_Fase.md</code> (NPC-4B5-01…18).

---

## 1. Objetivo y alcance

Que un NPC individual cambie racionalmente de **postura** durante el combate (Pressure / ControlRange / Recover / Disengage), con utility normalizada (0–1000), hysteresis, commitment y movimientos mínimos (MaintainRange / Retreat).

- **No incluye**: Role (4C), Squad (4E), Neural/ONNX/RL (4G+), LLM, GOAP/HTN, Flank/Formation (4D.5).
- **Modo de rollout**: <code>NPC_STRATEGY_ADAPTIVE_MODE = Disabled | Shadow | Enabled</code>.
- **Rollback**: <code>Disabled</code> → comportamiento idéntico a Phase 4B (sin residuo).

## 2. Prerequisitos

- Phase 4B certificada (tag <code>npc-brain-phase4-complete</code>), ya en <code>develop</code>.
- Iteración 0 completa: <code>NpcScenarioBuilder</code> + <code>ScenarioContext</code> + tests de arquetipos (verde en <code>develop</code>).

## 3. Principios de ejecución (se aplican a todas las subfases)

1. **Disabled es sagrado**: ninguna subfase puede alterar el resultado observable del pipeline cuando <code>NPC_STRATEGY_ADAPTIVE_MODE=Disabled</code>. El guard es el test baseline de 4B.5.0 (4B5-A1).
2. **Cada subfase compila y tiene tests verdes** antes de avanzar a la siguiente.
3. **Contratos vs Brain** (frontera): solo <code>NpcStrategicPosture</code>, <code>NpcStrategyDirective</code>, <code>NpcPostureTransitionReason</code>, <code>NpcPostureUtilities</code>, <code>NpcReflexPolicy</code> cruzan a <code>L2Dn.Npc.Contracts</code>. Utility internals (Considerations, Curves, PriorTable, StabilityGate) son **internal** en <code>L2Dn.Npc.Brain</code>.
4. **Sin I/O en Think**, determinista, zero-alloc en el hot path.
5. **Reflex nunca depende de Strategy adaptativa**: <code>NpcReflexPolicy</code> inmutable al spawn (se introduce en 4B.5.1).
6. **Eligibility ≠ Utility**: elegibilidad es hard constraint; utility es preferencia soft. Un candidato ineligible nunca gana.

---

## 4. Subfases (resumen con dependencias)

| Issue | Subfase | Entregable clave | Criterio | Dep |
|---|---|---|---|---|
| **NPC-4B5-01** | 4B.5.0 | Congelar Static V1: <code>NpcAdaptiveStrategyMode</code> + test baseline | 4B5-A1, A16b | — |
| **NPC-4B5-02** | 4B.5.1 | <code>NpcReflexPolicy</code> (Contracts) + resolver al spawn; ReflexBrain la consume | 4B5-A16 | 01 |
| **NPC-4B5-03** | 4B.5.2 | <code>MaintainRange</code> + <code>Retreat</code> como tactical candidates | 4B5-A20, A21 | 02 |
| **NPC-4B5-04** | 4B.5.3 | <code>NpcStrategicPosture</code> + <code>NpcPostureTransitionReason</code> + <code>NpcStrategyDirective</code> (Contracts) | 4B5-A7, A17 | 03 |
| **NPC-4B5-05** | 4B.5.4 | <code>NpcStrategyEvaluationContext</code> + <code>StrategyContextBuilder</code> (internal) | zero-alloc | 04 |
| **NPC-4B5-06** | 4B.5.5 | <code>NpcUtilityConsideration</code> + <code>NpcUtilityCurve</code> + <code>NpcPosturePriorTable</code> (internal) | 4B5-A22 | 05 |
| **NPC-4B5-07** | 4B.5.6 | <code>StrategicUtilityEvaluator</code> (media ponderada fixed-point) | 4B5-A14, A15 | 06 |
| **NPC-4B5-08** | 4B.5.7 | <code>PostureEligibilityEvaluator</code> + <code>NpcPostureCandidateSet</code> | 4B5-A18 | 07 |
| **NPC-4B5-09** | 4B.5.8 | <code>NpcStrategyState</code> + <code>PostureStabilityGate</code> (hysteresis + commitment) | 4B5-A4, A5 | 08 |
| **NPC-4B5-10** | 4B.5.9 | <code>StrategicDirectiveBuilder</code> (postura → biases ±1000) | — | 09 |
| **NPC-4B5-11** | 4B.5.10 | <code>NpcTieBreakPolicy</code> en TacticalActionEvaluator | 4B5-A9 | 10 |
| **NPC-4B5-12** | 4B.5.11 | <code>NpcAdaptiveShadowState</code> (longitudinal, keyed por NpcKey) | 4B5-A19 | 11 |
| **NPC-4B5-13** | 4B.5.12 | Replay stateful (postura + utilidades + transiciones + directiva + intent) | 4B5-A8 | 12 |
| **NPC-4B5-14** | 4B.5.13 | Telemetría OTLP (postura, transiciones, hysteresis, shadow) | tags style+posture | 13 |
| **NPC-4B5-15** | 4B.5.14 | R1–R5 con Adaptive Enabled | 4B5-A10…A13 | 14 |
| **NPC-4B5-16** | 4B.5.15 | Laboratorio real (Talking Island) | — | 15 |
| **NPC-4B5-17** | 4B.5.16 | Rollout waves 1–3 | — | 16 |
| **NPC-4B5-18** | 4B.5.17 | Checkpoint + rollback verificado | 4B5-A1 | 17 |

---

## 5. Detalle por subfase

### Hito A — Congelar baseline (NPC-4B5-01)

Ver §7 (arranque, detalle completo).

### Hito B — Reflex inmutable + movimiento mínimo (NPC-4B5-02…03)

**NPC-4B5-02 — <code>NpcReflexPolicy</code>**
- Objetivo: separar el Emergency Flee de la Strategy adaptativa. Reflex debe consumir una política **resuelta una sola vez al spawn**, no el <code>strategy.EffectiveFleeHpPercent</code> dinámico.
- Contracts: <code>readonly record struct NpcReflexPolicy(int EmergencyFleeHpPercent, bool FleeAllowed, int LeashRange)</code> en <code>NpcPerceptionPrimitives.cs</code> o archivo hermano.
- Resolver: <code>NpcIntelligenceProfile</code> + Static Strategy V1 (override) → <code>NpcReflexPolicy</code> al configurar el NPC. <code>EmergencyFleeHpPercent = strategy.FleeHpPercentOverride ?? intelligence.FleeHpPercent</code>.
- Brain: <code>ReflexBrain.Decide</code> pasa a recibir <code>NpcReflexPolicy</code> en lugar del <code>fleeHpPercent</code> derivado de strategy.
- Tests: <code>ReflexPolicy_AggressivePressure_FleeFivePercent</code> (5%), <code>ReflexPolicy_Survival_FleeThirtyPercent</code> (30%), <code>ReflexFlee_UsesOnlyNpcReflexPolicy</code>.
- Criterio: 4B5-A16, A16b (backward compat exacta).

**NPC-4B5-03 — <code>MaintainRange</code> + <code>Retreat</code>**
- Objetivo: añadir dos movimientos como **tactical candidates**, no como intents directos de Strategy.
- <code>NpcTacticalAction</code>: añadir <code>MaintainRange</code> y <code>Retreat</code> (junto a <code>BasicAttack/Approach/Flee/CastSkill</code>).
- Semántica: MaintainRange retrocede/acerca según <code>distance vs preferred ± tolerance</code>; Retreat se aleja del target, con fallback de dirección si geodata bloquea (mantener posición, no congelarse).
- Tests: <code>MaintainRange_TooClose_Retreats</code>, <code>MaintainRange_TooFar_Approaches</code>, <code>MaintainRange_InBand_NoMovement</code>, <code>Retreat_AllBlocked_MaintainsPosition</code>.
- Criterio: 4B5-A20, A21.

### Hito C — Contratos de postura (NPC-4B5-04)

- Contracts: <code>enum NpcStrategicPosture { Neutral, Pressure, ControlRange, Recover, Disengage }</code>; <code>enum NpcPostureTransitionReason</code> (≥12 valores); <code>readonly record struct NpcStrategyDirective(Posture, AttackBias, ApproachBias, OffensiveSkillBias, HealBias, FleeBias, RetreatBias, PreferredRange, Reason)</code>; <code>readonly record struct NpcPostureUtilities(...)</code>.
- Criterio: 4B5-A7 (transición con razón), A17 (Neutral no compite).

### Hito D — Evaluación de utility (NPC-4B5-05…08)

- **05** <code>NpcStrategyEvaluationContext</code> (SelfHpRatio, InCombat, TargetDistance, SkillReady, HealReady, CanFlee, VisibleHostileCount, CurrentPosture, TimeInPostureTicks, …) + <code>StrategyContextBuilder</code> (deriva de percepción, zero-alloc).
- **06** <code>NpcUtilityConsideration</code> + <code>NpcUtilityCurve</code> (piecewise-linear, validada al startup) + <code>NpcPosturePriorTable</code> (priors por Style).
- **07** <code>StrategicUtilityEvaluator</code>: media ponderada normalizada con <code>long</code> accumulator; denominador protegido (<code>if denominator <= 0 return prior</code>); <code>PriorWeight ∈ [1,1000]</code>.
- **08** <code>PostureEligibilityEvaluator</code> (hard constraints: Disengage→CanFlee, ControlRange→HasTarget, Recover→HealReady||CanFlee) + <code>NpcPostureCandidateSet</code>.
- Tests (05–08): <code>Utility_WeightedAverage_CorrectResult</code>, <code>Utility_NeverSaturates</code>, <code>Utility_DenominatorNeverZero</code>, <code>Weights_AreIntegers</code>, <code>Curve_*_RejectedAtStartup</code> (×4), <code>Ineligible_NeverSelected</code>, <code>Neutral_NeverComputedAsUtility</code>.
- Criterio: 4B5-A14, A15, A18, A22, A6.

### Hito E — Estabilidad + directiva (NPC-4B5-09…10)

- **09** <code>NpcStrategyState</code> (CurrentPosture, PostureEnteredWorldTick, PreviousPosture, LastTransitionReason, DecisionSequence) + <code>PostureStabilityGate</code> (SwitchMargin=100, MinDuration, RangeTolerance=60). Reflex Emergency siempre tiene autoridad superior.
- **10** <code>StrategicDirectiveBuilder</code>: postura ganadora → biases tácticos (±1000) + PreferredRange + Reason. Tactical score = <code>Clamp(base + roleDelta + directiveBias, 0, 1000)</code> (roleDelta es 4C, aquí = 0).
- Tests: <code>Hysteresis_OscillatingHp_StablePosture</code>, <code>Commitment_MinDuration_Respected</code>, <code>Commitment_ReflexEmergencyOverrides</code>, <code>Directive_Recover_IncreasesHealBias</code>, <code>AggressivePressure_LowHp_HealReady_Recover</code>, <code>Sequence_HpRecovery_ReEngagement</code>.
- Criterio: 4B5-A2, A3, A4, A5.

### Hito F — Tie-break (NPC-4B5-11)

- <code>NpcTieBreakPolicy</code> (internal): <code>PreferOffensive / PreferDefensive / PreferMobility</code>, configurable por Style, explícito y testeable.
- Tests: <code>TieBreak_ExplicitPolicy_NotCodeOrder</code>, <code>TieBreak_OrderIndependent</code>.
- Criterio: 4B5-A9.

### Hito G — Shadow + replay + telemetría (NPC-4B5-12…14)

- **12** <code>NpcAdaptiveShadowState</code>: keyed por <code>NpcKey</code>; Generation mismatch → descartar y fresh Neutral. Shadow mantiene estado V2 longitudinal sin alterar gameplay.
- **13** Replay stateful: postura + utilidades + transiciones + razones + directiva + intent.
- **14** Telemetría OTLP: <code>l2dn.npc.strategy.posture.selected</code>, <code>.transition</code>, <code>.dwell_ticks</code>, <code>.transition.suppressed</code>, <code>.utility.margin</code>, <code>.posture.ineligible</code>, <code>.evaluate_us</code>, <code>.shadow.agreement</code>, <code>.shadow.divergence</code>. Tags <code>style</code> + <code>posture</code> (sin ObjectId/TemplateId).
- Criterio: 4B5-A19, A8.

### Hito H — Rendimiento + rollout + checkpoint (NPC-4B5-15…18)

- **15** R1–R5 con Adaptive Enabled: P99 < 1 ms, zero drops, max concurrent = 1 (4B5-A10…A13).
- **16** Laboratorio real Talking Island (templates conocidos).
- **17** Rollout waves 1–3.
- **18** Checkpoint + rollback <code>Disabled</code> verificado (4B5-A1 sin residuo).

---

## 6. Mapa de criterios de aceptación → subfase

| Criterio | Subfase | Criterio | Subfase |
|---|---|---|---|
| A1 (Disabled==4B) | 4B5.0, 4B5.17 | A12 (alloc ≈ V1) | 4B5.14 |
| A2 (AP HP<25%→Recover) | 4B5.9 | A13 (P95 ≤ +25%) | 4B5.14 |
| A3 (Recover→reengage) | 4B5.9 | A14 (utility 0-1000) | 4B5.6 |
| A4 (hysteresis) | 4B5.8 | A15 (weights int, long) | 4B5.6 |
| A5 (commitment) | 4B5.8 | A16/A16b (Reflex) | 4B5.1 |
| A6 (Survival≥AP) | 4B5.5–4B5.6 | A17 (Neutral) | 4B5.7 |
| A7 (transition reason) | 4B5.3, 4B5.8 | A18 (ineligible) | 4B5.7 |
| A8 (replay determinista) | 4B5.12 | A19 (shadow longitudinal) | 4B5.11 |
| A9 (tie-break) | 4B5.10 | A20 (MaintainRange) | 4B5.2 |
| A10 (P99<1ms) | 4B5.14 | A21 (Retreat fallback) | 4B5.2 |
| A11 (zero I/O) | 4B5.5 | A22 (utility internals) | 4B5.5 |

---

## 7. NPC-4B5-01 — Congelar Static V1 como baseline (arranque)

### Objetivo

Establecer el **baseline inmutable** que el rollback <code>Disabled</code> debe reproducir exactamente. No se toca ningún comportamiento; solo se crea el flag de modo y el guard de regresión.

### Entregables

**1. <code>NpcAdaptiveStrategyMode</code> (nuevo)**
- Archivo: <code>L2Dn/L2Dn.GameServer.Model/AI/Npc/Brain/NpcAdaptiveStrategyMode.cs</code>, namespace <code>L2Dn.GameServer.AI.Runtime</code>.
- <code>public enum NpcAdaptiveStrategyMode { Disabled = 0, Shadow = 1, Enabled = 2 }</code> (clon del patrón <code>NpcStrategyMode</code>).

**2. Parseo de <code>NPC_STRATEGY_ADAPTIVE_MODE</code>**
- En <code>NpcStrategyOptions.FromEnvironment</code> (o un <code>NpcAdaptiveStrategyOptions</code> paralelo): leer <code>NPC_STRATEGY_ADAPTIVE_MODE</code> con el mismo <code>ParseMode</code> normalizado.
- **En 4B.5.0 solo <code>Disabled</code> es soportado**: <code>Shadow</code>/<code>Enabled</code> → warning + fallback a <code>Disabled</code> (igual que el pattern actual de <code>supported</code>).
- Añadir <code>AdaptiveMode</code> al record <code>NpcStrategyOptions</code>.

**3. Test baseline <code>AdaptiveBaselineTests.cs</code> (nuevo)**
- Archivo: <code>L2Dn/Tests/L2Dn.Npc.Brain.Tests/AdaptiveBaselineTests.cs</code>.
- Matriz representativa con <code>NpcScenarioBuilder</code>: estilos {Balanced, AggressivePressure, RangedControl, Survival} × situaciones {idle, target-en-rango, target-fuera-rango, HP-bajo, target-muerto, leash}. ~20–30 escenarios.
- Asertar la decisión esperada **exacta** (golden). Ejecutar hoy para capturar los valores correctos y fijarlos en el test.
- Este test es el guard que demuestra 4B5-A1 en toda la fase: cualquier subfase posterior que altere el resultado de <code>Disabled</code> lo rompe.
- Opcional: añadir un test que corra <code>NpcStrategyDecisionComparer.Compare(phase4bDecision, disabledDecision)</code> y exija <code>ExactMatch</code>.

### Criterio de salida

- <code>NPC_STRATEGY_ADAPTIVE_MODE</code> parsea con <code>Disabled</code> default; <code>Shadow/Enabled</code> rechazados con warning.
- El test baseline compila y pasa contra el código actual (Phase 4B) sin modificar ningún test existente.
- <code>dotnet test</code> (Brain + Contracts) verde.

### Verificación

<code>dotnet test L2Dn/Tests/L2Dn.Npc.Brain.Tests/</code> — baseline verde; 0 regresiones.

---

## 8. Riesgos y decisiones abiertas

| # | Riesgo / decisión | Resolución |
|---|---|---|
| 1 | <code>TargetHpRatio</code> no está en el snapshot → 4B.5 no lo necesita; se resuelve antes de 4D | No bloquea 4B.5 |
| 2 | Calibrar <code>MinimumPostureDurationTicks</code> y priors de postura | Con datos reales en 4B.5.15–16 |
| 3 | <code>NpcUtilityCurve</code> mal configurada rompe determinismo | Validación al startup (4B5.6) |
| 4 | Shadow longitudinal contamina el estado real | <code>NpcAdaptiveShadowState</code> separado, keyed por NpcKey |
| 5 | <code>MaintainRange/Retreat</code> sin geodata real | Fallback mantiene posición (no congelarse) |

---

## 9. Orden de verificación por subfase

Cada subfase cierra con: (1) compila, (2) tests de la subfase verdes, (3) baseline de 4B.5.0 sigue verde, (4) <code>dotnet test</code> completo sin regresiones. El checkpoint final (4B.5.17) exige la Definition of Done completa del diseño (§28 de <code>Fases/01_Fase_4B5_Stateful_Strategic_Utility.md</code>): arquitectura, compatibilidad, seguridad, rendimiento y estabilidad.
