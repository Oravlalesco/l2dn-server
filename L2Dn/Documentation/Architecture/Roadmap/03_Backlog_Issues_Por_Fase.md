# Backlog de Trabajo — Inteligencia de NPCs L2Dn (por fase)

Estado: **backlog de issues**. Fecha: 2026-08-18.
Fuentes: [`00_Roadmap_Maestro.md`](00_Roadmap_Maestro.md), [`01_Plan_Implementacion_NPC_AI.md`](01_Plan_Implementacion_NPC_AI.md), [`02_Estrategia_Validacion_Testing.md`](02_Estrategia_Validacion_Testing.md), [`04_Criterios_Aceptacion_y_Testing.md`](04_Criterios_Aceptacion_y_Testing.md) y documentos por fase.

## Cómo usar este backlog

- **ID de issue**: <code>NPC-&lt;fase&gt;-&lt;nn&gt;</code> (inteligencia) y <code>VAL-&lt;nn&gt;</code> (validación transversal). Las subfases de 4B.5 conservan su numeración <code>4B.5.x</code> entre paréntesis.
- **Checkbox**: <code>[ ]</code> pendiente · <code>[x]</code> completado. Cada issue se cierra cuando cumple su **criterio de aceptación**, no cuando "compila".
- **Dep (depende de)**: no empezar un issue hasta que sus dependencias estén cerradas.
- **Secuencia sugerida** al final (§ Secuencia de ejecución).

### Convenciones de estado (para seguimiento en tablero)

| Estado | Significado |
|---|---|
| Backlog | Aún no priorizado para la iteración actual |
| Ready | Especificado y desbloqueado (dependencias cerradas) |
| In Progress | En implementación |
| Review | Tests + shadow + revisión de criterios |
| Done | Criterio de aceptación verificado + sin regresiones |

---

## Validación transversal (arranca YA, acompaña todas las fases)

> Detalle en [`02_Estrategia_Validacion_Testing.md`](02_Estrategia_Validacion_Testing.md).

### Nivel 1 — Tests sintéticos

- [ ] **VAL-01** — Crear <code>NpcScenarioBuilder</code> en <code>L2Dn/Tests/L2Dn.Npc.Brain.Tests/</code>
  - Entregable: builder fluente para <code>NpcPerceptionSnapshot</code> (CreateArcher/CreateMelee/CreateHealer + WithX()).
  - Criterio: API ergonómica; sin I/O; snapshot inmutable.
  - Dep: —
- [ ] **VAL-02** — Tests de arquetipo Melee/Asalto (persecución, rango de parada, skills corto alcance, cambio por hate)
  - Dep: VAL-01
- [ ] **VAL-03** — Tests de arquetipo Ranged/Kiting (retroceder si objetivo < 200; distancia óptima 500–900)
  - Dep: VAL-01
- [ ] **VAL-04** — Tests de Hate multi-target (tanque 1000 hate vs mago 1200 hate a 800 u, sin oscilación)
  - Dep: VAL-01
- [ ] **VAL-05** — Tests de Leash/retorno (soft-leash → timeout → <code>ReturnHomeIntent</code>)
  - Dep: VAL-01
- [ ] **VAL-06** — Tests Healer/soporte (curar aliados) — **forward-looking, marcar Skip con razón "requiere 4E"**
  - Dep: NPC-4E-05
- [ ] **VAL-07** — Tests reacción a Root/Silence (no emitir movimiento inútil / no castear) — **verificar + ampliar el Brain**
  - Dep: verificar <code>MovementDisabled</code>/<code>AllSkillsDisabled</code> en Tactical
- [ ] **VAL-08** — Cerrar cobertura: ≥ 25 tests unitarios sobre los 6 arquetipos
  - Criterio: <code>L2Dn.Npc.Brain.Tests</code> + <code>L2Dn.Npc.Contracts.Tests</code> al 100%

### Nivel 2 — Laboratorios de spawn

- [ ] **VAL-09** — Crear <code>L2Dn/L2Dn.GameServer/DataPack/spawns/Labs/</code>
- [ ] **VAL-10** — <code>Labs/Lab_Ranged_Combat.xml</code> (arqueros/magos por carriles aislados)
- [ ] **VAL-11** — <code>Labs/Lab_Social_Faction.xml</code> (líder + minions + healer)
- [ ] **VAL-12** — <code>Labs/Lab_Geodata_Navigation.xml</code> (coordenadas reales Cruma/Catacumbas; sin bucles ante esquinas/puertas)
- [ ] **VAL-13** — <code>Labs/Lab_Crowd_Control.xml</code> (stun/root/poison/fear y recuperación)

### Nivel 4 — Herramientas GM

- [ ] **VAL-14** — <code>AdminNpcAi.cs</code> + registrar <code>admin_npc_ai_status</code> en <code>AdminCommands.xml</code>
  - Entregable: HTML/SysMessage con perfil de IA, hate table ordenada, último intent + duración µs.
  - Campo "postura" se añade en NPC-4B5-04.
  - Dep: —
- [ ] **VAL-15** — Registrar <code>admin_npc_ai_lab [nombre]</code> (teleport + refresh de spawns)
  - Dep: VAL-09

### Nivel 3 — Shadow + OTel

- [ ] **VAL-16** — Entorno Docker <code>NPC_STRATEGY_MODE=Shadow</code> (Fase 4) + dashboard de discrepancias
  - Dep: —
- [ ] **VAL-17** — Añadir tag <code>archetype</code>/<code>template_kind</code> a la telemetría de shadow (reutilizar <code>l2dn.npc.strategy.shadow.*</code>)
  - Dep: —
- [ ] **VAL-18** — Extender shadow a <code>NPC_STRATEGY_ADAPTIVE_MODE=Shadow</code> cuando exista 4B.5
  - Dep: NPC-4B5-12

---

## Fase 4B.5 — Stateful Strategic Utility

> Detalle: [`Fases/01_Fase_4B5_Stateful_Strategic_Utility.md`](Fases/01_Fase_4B5_Stateful_Strategic_Utility.md). Criterios 4B5-A1…A22.

- [ ] **NPC-4B5-01** (4B.5.0) — Congelar Static Strategy V1 como baseline
  - Criterio: Decision/Intent equivalence con Phase 4B (4B5-A1).
  - Dep: —
- [ ] **NPC-4B5-02** (4B.5.1) — <code>NpcReflexPolicy</code> (readonly record struct en Contracts) + resolver al spawn; ReflexBrain la consume
  - Criterio: AggressivePressure→5%, Survival→30% (4B5-A16, A16b).
  - Dep: NPC-4B5-01
- [ ] **NPC-4B5-03** (4B.5.2) — <code>MaintainRange</code> + <code>Retreat</code> como Tactical candidates
  - Criterio: MaintainRange bidireccional; Retreat fallback sin congelarse (4B5-A20, A21).
  - Dep: NPC-4B5-02
- [ ] **NPC-4B5-04** (4B.5.3) — <code>NpcStrategicPosture</code> + <code>NpcPostureTransitionReason</code> + <code>NpcStrategyDirective</code> (Contracts)
  - Criterio: enum de 5 posturas; transition reasons ≥ 12; directiva inmutable (4B5-A7, A17).
  - Dep: NPC-4B5-03
- [ ] **NPC-4B5-05** (4B.5.4) — <code>NpcStrategyEvaluationContext</code> + <code>StrategyContextBuilder</code> (internal Brain)
  - Criterio: contexto derivado de percepción, sin I/O, zero allocation.
  - Dep: NPC-4B5-04
- [ ] **NPC-4B5-06** (4B.5.5) — <code>NpcUtilityConsideration</code> + <code>NpcUtilityCurve</code> + <code>NpcPosturePriorTable</code> (internal Brain)
  - Criterio: curves validadas al startup (ordenadas, sin duplicados, [0,1000], ≥2 puntos) (4B5-A22).
  - Dep: NPC-4B5-05
- [ ] **NPC-4B5-07** (4B.5.6) — <code>StrategicUtilityEvaluator</code> (media ponderada normalizada, fixed-point long)
  - Criterio: 0–1000 sin saturación; denominador protegido (4B5-A14, A15).
  - Dep: NPC-4B5-06
- [ ] **NPC-4B5-08** (4B.5.7) — <code>PostureEligibilityEvaluator</code> + <code>NpcPostureCandidateSet</code>
  - Criterio: hard constraints (Disengage ineligible si FleeNotAllowed); ineligible nunca seleccionada (4B5-A18).
  - Dep: NPC-4B5-07
- [ ] **NPC-4B5-09** (4B.5.8) — <code>NpcStrategyState</code> + <code>PostureStabilityGate</code> (hysteresis + commitment)
  - Criterio: HP 34↔36 no oscila; MinDuration respetado salvo Reflex Emergency (4B5-A4, A5).
  - Dep: NPC-4B5-08
- [ ] **NPC-4B5-10** (4B.5.9) — <code>StrategicDirectiveBuilder</code> (postura → biases tácticos ±1000)
  - Criterio: Recover → HealBias>0, AttackBias<0; scores clamp [0,1000].
  - Dep: NPC-4B5-09
- [ ] **NPC-4B5-11** (4B.5.10) — <code>NpcTieBreakPolicy</code> en TacticalActionEvaluator
  - Criterio: empate se resuelve por política explícita, no por orden de código (4B5-A9).
  - Dep: NPC-4B5-10
- [ ] **NPC-4B5-12** (4B.5.11) — <code>NpcAdaptiveShadowState</code> (Shadow longitudinal keyed por NpcKey)
  - Criterio: estado persiste entre Thinks; Generation mismatch → fresh Neutral (4B5-A19).
  - Dep: NPC-4B5-11
- [ ] **NPC-4B5-13** (4B.5.12) — Replay stateful (postura + utilidades + transiciones + razones + directiva + intent)
  - Criterio: determinista (4B5-A8).
  - Dep: NPC-4B5-12
- [ ] **NPC-4B5-14** (4B.5.13) — Telemetría OTLP (postura, transiciones, hysteresis, márgenes, shadow)
  - Criterio: tags <code>style</code> + <code>posture</code>, sin ObjectId/TemplateId.
  - Dep: NPC-4B5-13
- [ ] **NPC-4B5-15** (4B.5.14) — R1–R5 con Adaptive Enabled
  - Criterio: P99 < 1 ms; zero drops; max concurrent = 1 (4B5-A10…A13).
  - Dep: NPC-4B5-14
- [ ] **NPC-4B5-16** (4B.5.15) — Laboratorio real (Talking Island, templates conocidos)
  - Dep: NPC-4B5-15
- [ ] **NPC-4B5-17** (4B.5.16) — Rollout controlado (waves 1–3)
  - Dep: NPC-4B5-16
- [ ] **NPC-4B5-18** (4B.5.17) — Checkpoint + rollback verificado (<code>NPC_STRATEGY_ADAPTIVE_MODE=Disabled</code>)
  - Criterio: rollback sin residuo a Phase 4B.
  - Dep: NPC-4B5-17

---

## Fase 4C — Style × Role

> Detalle: [`Fases/02_Fase_4C_Style_x_Role.md`](Fases/02_Fase_4C_Style_x_Role.md) + [`NpcStrategyPhase4C.md`](../NpcStrategyPhase4C.md).

- [ ] **NPC-4C-01** — <code>NpcStrategyRole</code> enum (Mob/Elite/Minion/Commander/Raid) + deltas inmutables
  - Criterio: Mob = delta cero; cardinalidad 5 (4C-A1, A9).
  - Dep: NPC-4B5-18
- [ ] **NPC-4C-02** — Composición <code>style + role</code> en <code>StrategyBrain</code> (incluye PosturePriors + SwitchMargin + MinDuration)
  - Criterio: <code>Balanced × Mob</code> idéntico a Phase 4; clamp [0,1000] (4C-A12, A13).
  - Dep: NPC-4C-01
- [ ] **NPC-4C-03** — Parseo de registro <code>id:style:role</code> en <code>NpcStrategyOptions</code>
  - Criterio: rol desconocido → warning + rechazo (no default silencioso); last-wins con warning (4C-A4).
  - Dep: NPC-4C-01
- [ ] **NPC-4C-04** — Tag OTLP <code>role</code> (cardinalidad 5, sin template ids)
  - Dep: NPC-4C-02
- [ ] **NPC-4C-05** — Tests Brain (matriz style×role; Raid compone sin ejecutar)
  - Criterio: determinismo ×1000; <code>NpcBrainEligibility</code> intacta (4C-A5, A6).
  - Dep: NPC-4C-02
- [ ] **NPC-4C-06** — Tests data/lab (<code>NpcDataLoadingTests</code>) — solo actores <code>Monster</code> + <code>AttackableAI</code>
  - Dep: NPC-4C-03
- [ ] **NPC-4C-07** — Lab spawns (carriles Elite / Minion / Raid-contract)
  - Dep: NPC-4C-03, VAL-09
- [ ] **NPC-4C-08** — Compose env + rollout shadow → 1–2 jugadores → area-wave
  - Criterio: R1–R5 sin drops/overflow (4C-A7).
  - Dep: NPC-4C-06, NPC-4C-07

---

## Fase 4D — Policy Foundation

> Detalle: [`Fases/03_Fase_4D_Policy_Foundation.md`](Fases/03_Fase_4D_Policy_Foundation.md). [`ADR-012`](../ADR/ADR-012-neural-policy-advisory-only.md) / [`ADR-013`](../ADR/ADR-013-action-masking-before-policy.md) / [`ADR-014`](../ADR/ADR-014-mandatory-deterministic-fallback.md) / [`ADR-015`](../ADR/ADR-015-causal-advice-metadata.md) / [`ADR-017`](../ADR/ADR-017-policy-arbitration-semantics.md).

- [ ] **NPC-4D-01** — Refactorizar <code>TacticalActionEvaluator</code> → <code>BuildCandidates()</code> (<code>NpcTacticalCandidateSet</code>) + <code>SelectCandidate()</code>
  - Criterio: CandidateSet = única fuente de eligibilidad; paridad Phase 4C (4D-A1, A2).
  - Dep: NPC-4C-08
- [ ] **NPC-4D-02** — <code>NpcPolicyObservationV1</code> (sin ObjectId/PlayerId; incluye CurrentTarget sin identidad)
  - Criterio: reflection sin identificadores técnicos (4D-A6, A7).
  - Dep: NPC-4D-01
- [ ] **NPC-4D-03** — <code>NpcPolicyAdvice</code> (causal metadata: NpcKey + BasedOnStateRevision + PolicyEvaluationId + TTL + versiones)
  - Criterio: mismatch NpcKey/StateRevision → rechazado (4D-A4, A5).
  - Dep: NPC-4D-02
- [ ] **NPC-4D-04** — <code>PolicyArbitrator</code> (Opción A; Disabled/Shadow/Enabled)
  - Criterio: Disabled transparente; neural solo toca candidatos elegibles; fallback invariante (4D-A3, A11, A12).
  - Dep: NPC-4D-03
- [ ] **NPC-4D-05** — <code>L2Dn.Npc.Policy.Runtime</code> (<code>INpcPolicyInferenceEngine</code> en Contracts; <code>NpcPolicyInferenceCoordinator</code> + <code>NpcPolicyAdviceStore</code>)
  - Criterio: Runtime no depende de Onnx; Brain no invoca <code>INpcPolicy</code> (4D-A13, A14).
  - Dep: NPC-4D-04
- [ ] **NPC-4D-06** — <code>NoOpInferenceEngine</code> + registro DI en GameServer Host
  - Dep: NPC-4D-05
- [ ] **NPC-4D-07** — Decisión <code>TargetHpRatio</code>: Perception Schema V2 vs V1 sin salud de target
  - Dep: NPC-4D-02 (decidir ANTES de cerrar 4C — ver riesgo R1)
- [ ] **NPC-4D-08** — Tests (contracts, brain, frontera de ensamblado) + Reflex ignora policy (4D-A9, A15)
  - Dep: NPC-4D-06

---

## Fase 4D.5 — Tactical Movement Primitives

> Detalle: [`Fases/04_Fase_4D5_Tactical_Movement.md`](Fases/04_Fase_4D5_Tactical_Movement.md).

- [ ] **NPC-4D5-01** — <code>FlankTarget(side, distance)</code> (validado por GeoEngine)
  - Dep: NPC-4D-08
- [ ] **NPC-4D5-02** — <code>FormationSlot(slotId, referenceFrame)</code>
  - Dep: NPC-4D5-01
- [ ] **NPC-4D5-03** — <code>CircleTarget</code> + <code>Scatter</code>
  - Dep: NPC-4D5-02
- [ ] **NPC-4D5-04** — Fallback de geodata (todas bloqueadas → mantener posición) + tests (Brain nunca produce coordenadas absolutas)
  - Dep: NPC-4D5-03

---

## Fase 4E — Squad Intelligence Foundation

> Detalle: [`Fases/05_Fase_4E_Squad_Intelligence.md`](Fases/05_Fase_4E_Squad_Intelligence.md).

- [ ] **NPC-4E-01** — <code>SquadSnapshot</code> + <code>SquadDirective</code> (Contracts, causal metadata: SquadKey/Generation/MembershipRevision)
  - Dep: NPC-4D5-04
- [ ] **NPC-4E-02** — <code>SquadFactory</code> desde <code>MinionList</code> (solo master+minion; sin clan-help)
  - Dep: NPC-4E-01
- [ ] **NPC-4E-03** — <code>SquadBrain</code> + <code>SquadBrainState</code> (internal Brain)
  - Criterio: muerte de commander → Retreat/Regroup; estabilidad sin cambios innecesarios.
  - Dep: NPC-4E-02
- [ ] **NPC-4E-04** — <code>SquadThinkCoordinator</code> en GameServer.Model (single-flight MaxConcurrent=1)
  - Dep: NPC-4E-03
- [ ] **NPC-4E-05** — <code>NpcCombatAssignment</code> modifica <code>StrategyEvaluationContext</code> (no scores directos)
  - Dep: NPC-4E-04
- [ ] **NPC-4E-06** — <code>NPC_SQUAD_MODE</code> (Disabled/Shadow/Enabled) + rollout
  - Criterio: Disabled = Phase 4C exacta.
  - Dep: NPC-4E-05
- [ ] **NPC-4E-07** — Tests (snapshot, factory, squad brain, assignments) + lab squad (líder + privates)
  - Dep: NPC-4E-06

---

## Fase 4F-A — Dataset + Behavior Cloning

> Detalle: [`Fases/06a_Fase_4FA_Dataset_BehaviorCloning.md`](Fases/06a_Fase_4FA_Dataset_BehaviorCloning.md).

- [ ] **NPC-4FA-01** — Captura pasiva de replay (hechos crudos, sin reward; queue llena → drop con telemetría)
  - Criterio: impacto TPS < 2%.
  - Dep: NPC-4E-07
- [ ] **NPC-4FA-02** — <code>L2Dn.Npc.Training.Contracts</code> (<code>Episode</code>, <code>EpisodeStep</code>, <code>NpcTrainingReward</code>)
  - Criterio: sin campo Reward en el replay bruto.
  - Dep: NPC-4FA-01
- [ ] **NPC-4FA-03** — <code>FeatureExtractor</code> que reutiliza el código de producción de <code>NpcPolicyObservationV1</code> (sin train/serve skew)
  - Dep: NPC-4FA-02
- [ ] **NPC-4FA-04** — <code>L2Dn.Npc.Training.Export</code> (<code>DatasetBuilder</code> + split por episodio)
  - Criterio: sin leakage; hold-out templates.
  - Dep: NPC-4FA-03
- [ ] **NPC-4FA-05** — <code>Training/Python/train_imitation.py</code> + <code>evaluate.py</code> (métricas por clase)
  - Dep: NPC-4FA-04
- [ ] **NPC-4FA-06** — <code>Training/Python/export_onnx.py</code> + test de carga en C# (ONNX Runtime)
  - Dep: NPC-4FA-05

---

## Fase 4G — Neural Policy Shadow (ONNX)

> Detalle: [`Fases/07_Fase_4G_Neural_Policy_Shadow.md`](Fases/07_Fase_4G_Neural_Policy_Shadow.md).

- [ ] **NPC-4G-01** — <code>L2Dn.Npc.Policy.Onnx</code> (carga de modelo al startup)
  - Criterio: Brain no referencia ONNX (frontera).
  - Dep: NPC-4FA-06
- [ ] **NPC-4G-02** — <code>OnnxInferenceEngine</code> implementando <code>INpcPolicyInferenceEngine</code>
  - Dep: NPC-4G-01
- [ ] **NPC-4G-03** — Bounded Inference Coordinator + AdviceStore (consumir engine via DI)
  - Criterio: cero I/O en Think; single-flight por NPC.
  - Dep: NPC-4G-02
- [ ] **NPC-4G-04** — Shadow con <code>PolicyEvaluationId</code> pareado + comparación causal
  - Criterio: cero inferencias alteran gameplay (4G-A5); comparación contra expert action guardada.
  - Dep: NPC-4G-03
- [ ] **NPC-4G-05** — Tests (frontera, fallback ante OrtException, sin leak 1000 inferencias)
  - Dep: NPC-4G-04

---

## Fase 4H — Stochastic Individual Policy (Enabled)

> Detalle: [`Fases/08_Fase_4H_Stochastic_Policy.md`](Fases/08_Fase_4H_Stochastic_Policy.md).

- [ ] **NPC-4H-01** — PRNG Hash-based (<code>NpcDecisionSeed</code>) determinista
  - Dep: NPC-4G-05
- [ ] **NPC-4H-02** — Sampler (temperature) sobre <code>CandidateSet</code>
  - Criterio: inelegible nunca muestreado; T=0.01 ≈ argmax; T=1.0 = softmax.
  - Dep: NPC-4H-01
- [ ] **NPC-4H-03** — <code>NpcAiDifficultyTier</code> (temperature, neural influence, advice refresh, variabilidad)
  - Dep: NPC-4H-02
- [ ] **NPC-4H-04** — Rollout Enabled + verificación Gateway rejection ratio (< 5% vs determinista)
  - Dep: NPC-4H-03

---

## Fase 4F-B — Headless Combat Simulator

> Detalle: [`Fases/06b_Fase_4FB_Headless_Simulator.md`](Fases/06b_Fase_4FB_Headless_Simulator.md). Puede arrancar en paralelo con 4F-A/4G/4H.

- [ ] **NPC-4FB-01** — Simulador con función de transición <code>S(t)+A(t)→S(t+1)</code>
  - Dep: NPC-4H-04
- [ ] **NPC-4FB-02** — Reutilizar lógica real del GameServer (damage formula, etc.)
  - Dep: NPC-4FB-01
- [ ] **NPC-4FB-03** — Wrapper Gymnasium/RLlib (reset/step)
  - Dep: NPC-4FB-02
- [ ] **NPC-4FB-04** — Determinismo con seed fija + tests
  - Dep: NPC-4FB-03

---

## Fase 4I-A — MARL / CTDE Individual

> Detalle: [`Fases/09a_Fase_4IA_MARL_CTDE.md`](Fases/09a_Fase_4IA_MARL_CTDE.md).

- [ ] **NPC-4IA-01** — Input CTDE: percepción local + aliados cercanos + <code>CombatAssignment</code> (sin estado global)
  - Dep: NPC-4FB-04
- [ ] **NPC-4IA-02** — Parameter sharing (12 NPCs, misma policy, seeds distintas)
  - Dep: NPC-4IA-01
- [ ] **NPC-4IA-03** — Self-play con opponent pool (≥3)
  - Dep: NPC-4IA-02
- [ ] **NPC-4IA-04** — <code>Training/Python/train_rl.py</code>
  - Dep: NPC-4IA-03

---

## Fase 4I-B — Learned Squad Policy

> Detalle: [`Fases/09b_Fase_4IB_Learned_Squad_Policy.md`](Fases/09b_Fase_4IB_Learned_Squad_Policy.md).

- [ ] **NPC-4IB-01** — <code>Neural SquadBrain</code> (directivas coherentes, sin oscilación)
  - Dep: NPC-4IA-04
- [ ] **NPC-4IB-02** — Fallback a comportamiento individual Phase 4C si la policy falla
  - Dep: NPC-4IB-01
- [ ] **NPC-4IB-03** — Reward shaping sin comportamiento degenerado + tests
  - Dep: NPC-4IB-02

---

## Fase 5 — Distributed Policy Runtime

> Detalle: [`Fases/10_Fase_5_Distributed_Brain.md`](Fases/10_Fase_5_Distributed_Brain.md).

- [ ] **NPC-5-01** — <code>L2Dn.Npc.Transport.Grpc</code> (client/server)
  - Dep: NPC-4IB-03
- [ ] **NPC-5-02** — <code>GrpcInferenceEngine</code> implementando <code>INpcPolicyInferenceEngine</code>
  - Dep: NPC-5-01
- [ ] **NPC-5-03** — Circuit breaker + workers devuelven Advice (nunca Intent)
  - Dep: NPC-5-02
- [ ] **NPC-5-04** — Servicio worker remoto + telemetría de latencia/fallos
  - Dep: NPC-5-03

---

## Fase 6 — Encounter / Raid

> Detalle: [`Fases/11_Fase_6_Raid_Commander.md`](Fases/11_Fase_6_Raid_Commander.md).

- [ ] **NPC-6-01** — Política determinista de encounter (fallback, no hot-switch a Legacy)
  - Dep: NPC-5-04
- [ ] **NPC-6-02** — Separar mechanics de intelligence + tests
  - Dep: NPC-6-01

---

## Fase 7+ — Cognitive / World

> Detalle: [`Fases/12_Fase_7_Cognitive_World.md`](Fases/12_Fase_7_Cognitive_World.md).

- [ ] **NPC-7-01** — <code>L2Dn.AI.Directors</code> (<code>WorldDirector</code>, <code>RegionDirector</code>) operando por directivas/políticas
  - Dep: NPC-6-02
- [ ] **NPC-7-02** — Policy Gate con límites + LLM fuera del loop de combate (ADR-007)
  - Dep: NPC-7-01

---

## Secuencia de ejecución sugerida

### Iteración 0 (ahora — sin dependencias)
1. VAL-01 → VAL-02 … VAL-05 (congelar Fase 4 contra regresiones)
2. VAL-14, VAL-15 (herramientas GM — visibilidad inmediata)
3. VAL-16, VAL-17 (shadow + telemetría de la Fase 4 actual)
4. VAL-09 → VAL-10 … VAL-13 (labs; el de social/facción queda "observación Legacy" hasta 4E)

### Iteración 1 — Fase 4B.5
5. NPC-4B5-01 … NPC-4B5-18 (en orden; VAL-08 y VAL-18 al cerrar)

### Iteración 2 — Fase 4C
6. NPC-4C-01 … NPC-4C-08

### Iteración 3 — Fundación de política
7. NPC-4D-01 … NPC-4D-08 (decidir NPC-4D-07 TargetHpRatio ANTES)
8. NPC-4D5-01 … NPC-4D5-04

### Iteración 4 — Squad
9. NPC-4E-01 … NPC-4E-07 (desbloquea VAL-06)

### Iteración 5+ — Aprendizaje
10. NPC-4FA-* → NPC-4G-* → NPC-4H-* (vía imitación); NPC-4FB-* en paralelo (vía RL)
11. NPC-4IA-* → NPC-4IB-* → NPC-5-* → NPC-6-* → NPC-7-*

### Resumen de dependencias clave
- VAL-06 (healer aliado) espera NPC-4E-05.
- VAL-07 (CC) espera verificación/ampliación de <code>MovementDisabled</code>/<code>AllSkillsDisabled</code> en Tactical.
- VAL-18 (shadow adaptativo) espera NPC-4B5-12.
- NPC-4D-07 (TargetHpRatio) debe decidirse antes de cerrar 4C.
