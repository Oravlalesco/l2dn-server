# Criterios de Aceptación y Estrategia de Testing

Estado: diseño. Fecha: 2026-08-13.

Este documento define qué debe demostrarse en cada fase para considerarla completa, y qué tests lo demuestran. No lista "cosas que pasan"; lista **qué propiedades prueban y por qué cada test es necesario**.

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

### Tests concretos

**Contracts (`L2Dn.Npc.Contracts.Tests`):**

| Test | Propiedad | Input | Output esperado |
|---|---|---|---|
| `RoleDelta_Mob_IsIdentityZero` | El delta Mob no modifica ningún score | `RoleDelta.Mob` | Todos los campos == 0 |
| `RoleDelta_AllRoles_AreImmutable` | Los deltas no pueden mutar después de construcción | Crear delta, intentar modificar | Compilación falla o excepción |
| `RoleEnum_HasExactlyFiveValues` | Cardinalidad acotada para telemetría | `Enum.GetValues<NpcStrategyRole>()` | Exactamente {Mob, Elite, Minion, Commander, Raid} |

**Brain (`L2Dn.Npc.Brain.Tests`):**

| Test | Propiedad | Input | Output esperado |
|---|---|---|---|
| `Compose_Balanced_Mob_EqualsPhase4Balanced` | Identity: rol Mob no altera baseline | Perception + Balanced + Mob | Decisión idéntica a Phase 4 Balanced sin rol |
| `Compose_AggressivePressure_Elite_ProducesExpectedScores` | La suma aritmética es correcta | AP base + Elite delta | BasicAttack=85, Approach=95, OffensiveSkill=120, Heal=85, Flee=50 |
| `Compose_Survival_Commander_FleeScoreNeverNegative` | Scores negativos no rompen Tactical | Survival(Flee=115) + Commander(Flee=−25) + edge case | Flee score ≥ 0, decisión válida |
| `Compose_AllStyles_AllRoles_Deterministic_1000x` | Determinismo | Cada combinación × 1000 evaluaciones idénticas | Output bit-a-bit idéntico las 1000 veces |
| `Compose_ExplicitLegacyProfile_DefaultsToBalancedMob` | No herencia implícita | Intelligence profile legacy sin registro de Strategy | Usa Balanced scores, no hereda un rol |
| `Registry_MalformedEntry_WarnsAndRejects` | Configuración inválida no se esconde | `"20130:invalid:mob"` | Warning emitido, template NO aparece en registro |
| `Registry_UnknownRole_WarnsAndRejects` | Rol desconocido no habilita Balanced | `"20130:aggressive_pressure:tank"` | Warning emitido, template NO aparece |
| `Registry_DuplicateTemplate_LastWinsWithWarning` | Comportamiento explícito para duplicados | Dos entradas para template 20130 | Último gana, warning emitido |

**GameServer.Model (`L2Dn.GameServer.Model.Tests`):**

| Test | Propiedad | Input | Output esperado |
|---|---|---|---|
| `Eligibility_Guard_WithCommanderRole_StaysLegacy` | Actor boundary no se expande | Guard actor + registro `"guard_id:balanced:commander"` | `UsesIntentBrain == false` |
| `Eligibility_RaidBoss_WithRaidRole_StaysLegacy` | RaidBoss no entra en Intent | RaidBoss actor + registro con rol Raid | `UsesIntentBrain == false` |
| `Eligibility_BaseMonster_WithEliteRole_UsesIntent` | Monster base sí puede tener rol | Monster base + registro con rol Elite | `UsesIntentBrain == true` |
| `RoleTelemetry_EmitsExactlyFiveCardinalityValues` | Cardinalidad acotada | Evaluaciones con todos los roles | Tags OTLP contienen solo los 5 valores del enum |

---

## Fase 4D — Policy Foundation

### Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4D-A1 | `DeterministicNpcPolicy` produce un `NpcPolicyAdvice` cuyo efecto sobre `StrategyBrain` resulta en la misma decisión que Strategy sin policy, para TODOS los scenarios de Phase 4 | Comportamiento | Que la policy determinista es semánticamente transparente — no cambia nada |
| 4D-A2 | `NoOpNpcPolicy` produce un advice que el cache reconoce como vencido inmediatamente, causando fallback | Comportamiento | Que el mecanismo de fallback funciona sin una policy real |
| 4D-A3 | Un `NpcPolicyAdvice` con `ExpiresAt < tick_actual` es ignorado por `StrategyBrain` y se usa Strategy determinista | Comportamiento | Que advice vencido = fallback, no NPC congelado |
| 4D-A4 | `NpcPolicyObservationV1` NUNCA contiene ObjectId, PlayerId, ni ningún identificador técnico de entidad | Contrato | Que la red nunca puede aprender identidad técnica |
| 4D-A5 | `NpcPolicyObservationV1` es construible desde un `NpcPerceptionSnapshot` en tiempo O(n) donde n = entidades visibles, sin allocations en el heap más allá del struct resultado | Rendimiento | Que la extracción de features no degrada el hot path |
| 4D-A6 | `NpcPolicyActionMask` enmascara correctamente: skill en cooldown, heal con HP > umbral, flee deshabilitado, skill sin MP, actor en casting/stunned | Comportamiento | Que la primera barrera funciona — acciones imposibles se filtran antes de la policy |
| 4D-A7 | El cache de advice es bounded: no crece más allá de NPCs registrados, limpia entries de generaciones obsoletas | Contrato | Que no hay memory leak de advice |
| 4D-A8 | `NpcPolicyVersion` con schema incompatible o checksum incorrecto causa rechazo + fallback + telemetría | Integración | Que un modelo corrupto no puede activarse silenciosamente |
| 4D-A9 | El Reflex Brain produce la misma decisión con policy Disabled, Enabled, o error de policy | Comportamiento | Que Reflex NUNCA depende de la policy |
| 4D-A10 | `L2Dn.Npc.Brain` sigue referenciando SOLO `L2Dn.Npc.Contracts` entre ensamblados L2Dn | Arquitectura | Que la frontera de dependencia no se viola |

### Tests concretos

**Contracts:**

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `ObservationV1_NoObjectIds` | Sin identidad técnica | Inspeccionar todos los campos de `NpcPolicyObservationV1` via reflection → ningún campo de tipo `int` que represente un ObjectId |
| `ObservationV1_AllFieldsNormalized` | Inputs numéricos para la red | HP ratio ∈ [0,1], MP ratio ∈ [0,1], distancias ≥ 0, counts ≥ 0 |
| `ObservationV1_IsImmutable` | Sin mutación post-construcción | Intentar modificar cualquier campo → fallo de compilación (readonly struct) |
| `ActionMask_SkillOnCooldown_IsMasked` | Primera barrera funcional | Skill con cooldown > 0 → bit de skill = false en mask |
| `ActionMask_HealWithHighHP_IsMasked` | Heal innecesario filtrado | HP > heal threshold → bit de heal = false |
| `ActionMask_FleeDisabledByIntelligence_IsMasked` | Respeta intelligence profile | FleeAllowed = false → bit de flee = false |
| `ActionMask_AllActionsValid_NothingMasked` | Mask no filtra sin razón | Todas las precondiciones cumplidas → mask all-true |
| `PolicyAdvice_Expired_IsRecognizedAsStale` | Fallback correcto | Advice con ExpiresAt = 100, tick actual = 101 → `IsStale == true` |
| `PolicyVersion_ChecksumMismatch_RejectedWithReason` | Modelo corrupto rechazado | Version con checksum alterado → `Rejected(ChecksumMismatch)` |

**Brain:**

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `DeterministicPolicy_Phase4Parity_AllScenarios` | Transparencia semántica | Para CADA scenario S1-S9 de Phase 4: Policy Disabled vs DeterministicPolicy → misma decisión |
| `StaleAdvice_FallsBackToStrategy` | Fallback funcional | Advice con ExpiresAt pasado + tick actual → Strategy sin modificar scores → misma decisión que sin policy |
| `PolicyError_FallsBackToStrategy` | Tolerancia a fallos | Policy que lanza excepción → fallback → decisión válida sin interrupción |
| `ReflexBrain_IgnoresPolicy_TargetDead` | Reflex independiente | Target muerto + advice que dice "Attack" → Reflex emite ClearTarget, ignora advice |
| `ReflexBrain_IgnoresPolicy_OutsideLeash` | Reflex independiente | Fuera de leash + advice que dice "Approach" → Reflex emite ReturnHome |
| `ObservationExtraction_NoHeapAllocation` | Performance hot path | Extraer observation desde perception → cero allocations (verificar con benchmark) |
| `AdviceCache_BoundedSize_CleansStaleGenerations` | Sin memory leak | Registrar 1000 NPCs, avanzar generación de 500 → cache ≤ 500 entries activas |

---

## Fase 4E — Squad Intelligence Foundation

### Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4E-A1 | Un escuadrón creado desde Master + MinionList contiene exactamente los miembros que `MinionList` reporta, con roles y estilos correctos | Integración | Que SquadFactory no inventa ni pierde miembros |
| 4E-A2 | `SquadContext` refleja correctamente: HP promedio, composición viva/muerta, estado del commander, posición centroide | Contrato | Que el snapshot de escuadrón es correcto |
| 4E-A3 | `SquadThinkCoordinator` garantiza `MaximumConcurrentSquadThinkPerSquad = 1` bajo carga concurrente | Comportamiento | Que la invariante single-flight aplica a escuadrones |
| 4E-A4 | La muerte del Commander produce un cambio de directiva observable (ej: Retreat o FreeAgent) | Comportamiento | Que el SquadBrain reacciona a eventos críticos |
| 4E-A5 | Un NPC individual sin SquadDirective (porque su escuadrón no existe o se destruyó) se comporta exactamente como en Phase 4C | Integración | Que la ausencia de squad = fallback limpio, no error |
| 4E-A6 | `NpcCombatAssignment` modifica los scores del `StrategyBrain` individual de forma consistente y predecible | Comportamiento | Que Frontline, RangedPressure, ProtectSupport producen diferencias observables |
| 4E-A7 | El SquadBrain no modifica directamente ningún estado del GameServer (no mueve NPCs, no cambia targets, no inflige daño) | Arquitectura | Que el SquadBrain solo produce directivas, igual que el Brain individual solo produce intents |

### Tests concretos

**Contracts:**

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `SquadContext_IsImmutable` | Sin mutación | Intentar modificar SquadContext → fallo |
| `SquadDirective_IsImmutable` | Sin mutación | Intentar modificar SquadDirective → fallo |
| `CombatAssignment_HasExpectedValues` | Enum completo | Exactamente: Frontline, FlankLeft, FlankRight, RangedPressure, ProtectSupport, ProtectCommander, Regroup, Retreat, FreeAgent |
| `SquadContext_DeadMember_ReflectedInComposition` | Snapshot correcto | Squad con 5 miembros, 2 muertos → AliveMemberCount=3, DeadMemberCount=2 |
| `SquadContext_CentroidCalculation_Correct` | Geometría correcta | 3 miembros en posiciones conocidas → centroide = promedio de las 3 posiciones |

**Brain:**

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `SquadBrain_CommanderDied_EmitsRetreatOrFreeAgent` | Reacción a pérdida de líder | SquadContext con CommanderAlive=false → Directive.Objective ∈ {Retreat, FreeAgent} |
| `SquadBrain_AllMembersHealthy_NoFormationChange` | Estabilidad sin eventos | SquadContext estable → Directive no cambia innecesariamente |
| `SquadBrain_SupportThreatened_EmitsProtectDirective` | Protección del soporte | SquadContext con support bajo ataque → Al menos un miembro recibe ProtectSupport |
| `SquadBrain_NumericalDisadvantage_EmitsRegroup` | Adaptación táctica | Squad con 2/5 vivos vs 4 enemigos → Directive.Objective = Regroup o Retreat |
| `SquadThink_SingleFlight_UnderConcurrency` | Invariante de concurrencia | 1000 wakeups simultáneos para un squad → MaxConcurrent = 1, zero drops |
| `SquadThink_Coalescing_MultipleWakes` | Eficiencia | 10 MemberDied wakes en 50ms para mismo squad → se fusionan en 1 Think |
| `Assignment_Frontline_IncreasesApproachScore` | Efecto observable | Mismo NPC con/sin Assignment=Frontline → Approach score mayor con Frontline |
| `Assignment_ProtectSupport_DecreasesFleeSensitivity` | Efecto observable | Mismo NPC con/sin Assignment=ProtectSupport → Flee score menor |
| `NoSquad_FallbackToIndividualBrain` | Degradación limpia | NPC sin SquadDirective → produce misma decisión que Phase 4C |

**GameServer.Model:**

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `SquadFactory_FromMinionList_CorrectMembers` | Mapeo fiel | Monster con MinionList de 4 minions → Squad con 5 members (master + 4) |
| `SquadFactory_EmptyMinionList_NoSquadCreated` | Sin escuadrón vacío | Monster sin minions → No se crea Squad |
| `SquadManager_MemberDeath_UpdatesContext` | Ciclo de vida | Miembro muere → próximo SquadContext refleja DeadMemberCount incrementado |
| `SquadManager_AllMembersDead_SquadDissolved` | Limpieza | Todos los miembros mueren → Squad removido del manager |

---

## Fase 4F — Training Platform

### Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4F-A1 | La captura de replay NO degrada los TPS del GameServer más de un 2% | Rendimiento | Que el sistema de captura es verdaderamente pasivo |
| 4F-A2 | Un `NpcEpisode` exportado puede reconstruir la secuencia completa de decisiones del Brain para ese combate | Contrato | Que el dataset contiene información suficiente para entrenamiento |
| 4F-A3 | `FeatureExtractor` produce vectores de dimensión fija con valores numéricos normalizados, sin NaN ni Inf | Contrato | Que los datos son consumibles por PyTorch sin preproceso adicional |
| 4F-A4 | El pipeline `train_imitation.py` converge: la loss disminuye monótonamente en las primeras 100 epochs con un dataset sintético de 1,000 episodios | Comportamiento | Que el modelo puede aprender el patrón determinista |
| 4F-A5 | `export_onnx.py` produce un `.onnx` cargable por ONNX Runtime C# sin errores de shape o tipo | Integración | Que el puente Python↔C# funciona end-to-end |
| 4F-A6 | `evaluate.py` reporta acuerdo semántico con formato cuantificable (%, no "se ve bien") | Contrato | Que la evaluación es reproducible y automática |
| 4F-A7 | El action mask del episodio es consistente: si el mask dice que Heal está bloqueado, la acción elegida NUNCA es Heal | Contrato | Que el mask del dataset refleja la realidad del juego |

### Tests concretos

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `FeatureVector_FixedDimension` | Dimensión estable | Cualquier NpcPerception → vector de exactamente N floats (N definido por schema V1) |
| `FeatureVector_NoNaN_NoInf` | Datos limpios | 10,000 percepciones aleatorias → cero NaN, cero Inf en vectores |
| `FeatureVector_HPRatio_InZeroOne` | Normalización correcta | HP=500, MaxHP=1000 → hp_ratio=0.5 |
| `Episode_RoundTrip_Serialization` | Serializabilidad | Crear episodio → serializar JSON → deserializar → comparar → iguales |
| `Episode_ActionMask_ConsistentWithAction` | Consistencia interna | Para cada step: si action_mask[Heal]=false → action_chosen ≠ Heal |
| `Replay_Capture_DoesNotBlockThink` | No bloquea | Replay queue llena → Think continúa sin esperar, step descartado con telemetría |
| `ONNX_Export_LoadableInCSharp` | Interoperabilidad | model.onnx exportado → `new InferenceSession(path)` en C# → sin excepción, shapes correctas |
| `ImitationLearning_LossDecreases` | Capacidad de aprendizaje | Dataset sintético 1000 episodes, 100 epochs → loss[epoch 100] < loss[epoch 1] × 0.5 |

---

## Fase 4G — Neural Policy Shadow

### Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4G-A1 | ONNX Runtime cargado al startup, modelo en memoria, CERO lecturas de disco durante Think | Rendimiento | Que la inferencia no tiene I/O en hot path |
| 4G-A2 | La inferencia neural NO aparece en el Critical reaction path: un ataque recibe respuesta Reflex ANTES de que complete la inferencia neural | Comportamiento | Que la Neural Policy es advisory, no blocking |
| 4G-A3 | Acuerdo semántico (ExactMatch + SemanticMatch) ≥ 90% en servidor de prueba con modelo behavior-cloned contra 1,000 ciclos de combate | Integración | Que el modelo reproduce razonablemente el comportamiento certificado |
| 4G-A4 | P99 de inferencia ONNX < 5ms para MLP 128→128→128→64 en CPU | Rendimiento | Que la inferencia individual es rápida |
| 4G-A5 | Cero inferencias alteran el gameplay: TODAS las decisiones ejecutadas son del pipeline determinista | Comportamiento | Que Shadow es realmente shadow — no leak de decisiones neurales |
| 4G-A6 | Si `OnnxNpcPolicy` falla (excepción, timeout, modelo corrupto), el NPC usa Strategy determinista sin interrupción visible | Integración | Que el fallback funciona en condiciones reales |
| 4G-A7 | Memory stable después de 24h de inferencia continua: no hay growth de managed/unmanaged heap atribuible a ONNX | Rendimiento | Que no hay memory leak |

### Tests concretos

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `OnnxPolicy_LoadsAtStartup_NoLaterDiskRead` | Sin I/O en hot path | Cargar modelo → mock filesystem para detectar lecturas posteriores → cero lecturas durante 1000 inferencias |
| `OnnxPolicy_InferenceOutput_MatchesExpectedShape` | Shape correcta | Input tensor de 128 floats → output tensor de N action logits |
| `OnnxPolicy_Exception_FallsBackCleanly` | Tolerancia a fallos | Modelo que lanza OrtException → `INpcPolicy.Evaluate` retorna fallback result → StrategyBrain usa determinista |
| `OnnxPolicy_Timeout_FallsBackCleanly` | Tolerancia a latencia | Inferencia artificialmente lenta (sleep 100ms) → advice marcado como stale → fallback |
| `Shadow_NeverExecutesNeuralDecision` | Shadow puro | 1000 ciclos en Shadow → TODAS las intents ejecutadas provienen del pipeline determinista |
| `Shadow_Comparison_EmitsCorrectMetrics` | Telemetría funcional | Decisiones comparadas → counters ExactMatch/SemanticMatch/Different incrementados correctamente |
| `Reflex_RespondsBeforeInference` | Independencia del Reflex | Target muere + inferencia pendiente → Reflex emite ClearTarget sin esperar inferencia |
| `OnnxRuntime_NoManagedLeak_1000Inferences` | Sin memory leak | 1000 inferencias → GC.GetTotalMemory estable ±5% |

---

## Fase 4H — Stochastic Policy Enabled

### Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4H-A1 | Con seed fija, la misma secuencia de observaciones produce la misma secuencia de decisiones | Comportamiento | Reproducibilidad para tests y replay |
| 4H-A2 | Con seed dinámica, el NPC exhibe variabilidad observable: no repite exactamente la misma acción en situaciones idénticas repetidas | Comportamiento | Que el sampling funciona y produce diversidad |
| 4H-A3 | Una acción enmascarada por ActionMask NUNCA es seleccionada por el sampler, independientemente de la temperature | Contrato | Que el mask es una barrera hard, no sugerencia |
| 4H-A4 | Temperature 0.01 produce comportamiento casi determinista (>98% de decisiones = argmax) | Comportamiento | Que temperature baja converge a determinismo |
| 4H-A5 | Temperature 1.0 produce distribución cercana a la original de la red | Comportamiento | Que temperature alta no distorsiona irracionalmente |
| 4H-A6 | Gateway rejection ratio con Neural Enabled NO es significativamente mayor que con Deterministic (< 5% de diferencia absoluta) | Integración | Que la policy neural no genera un volumen anormal de intents inválidos |
| 4H-A7 | A/B testing: grupo control y grupo experimental producen métricas de gameplay comparables (TTK, survival ratio) salvo variabilidad estadística esperada | Gameplay | Que la policy neural no rompe el balance |

### Tests concretos

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `Sampler_FixedSeed_Deterministic` | Reproducibilidad | Seed=12345 + misma distribución × 100 → misma secuencia de acciones las 100 veces |
| `Sampler_DynamicSeed_VariableOutput` | Variabilidad | Seed diferente + misma distribución × 100 → al menos 2 acciones distintas en 100 muestras |
| `Sampler_MaskedAction_NeverSelected` | Mask inviolable | Mask con Heal=false + distribución donde Heal tiene logit máximo + 10,000 samples → cero Heal seleccionadas |
| `Sampler_Temperature001_AlmostDeterministic` | Control de temperatura | T=0.01 + distribución clara × 1,000 → >98% selecciona argmax |
| `Sampler_Temperature10_HighEntropy` | Control de temperatura | T=1.0 + distribución uniforme-ish × 1,000 → entropía cercana a máxima |
| `SkillLevel_Novice_HighTemperature` | Mapeo correcto | SkillLevel.Novice → temperature ≥ 0.7 |
| `SkillLevel_Legendary_LowTemperature` | Mapeo correcto | SkillLevel.Legendary → temperature ≤ 0.1 |
| `GatewayRejections_NeuralVsDeterministic_Comparable` | Sin intent storms | 1000 ciclos Neural Enabled vs 1000 ciclos Deterministic → rejection rate delta < 5% |

---

## Fase 4I — Multi-Agent Intelligence

### Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4I-A1 | Un squad con Neural Squad Policy produce directivas que difieren de las deterministas de forma observable pero no degenera en oscilación o inacción | Comportamiento | Que la policy de squad aprendida produce comportamiento coherente |
| 4I-A2 | Parameter sharing: 12 Frontliners usando la misma `frontliner_policy_v4` se comportan de forma coordinada pero no idéntica (seeds diferentes) | Comportamiento | Que shared policies escalan sin uniformidad robótica |
| 4I-A3 | CTDE: cada NPC individual solo recibe su percepción + su CombatAssignment + SquadDirective, NUNCA el estado global del entrenador | Arquitectura | Que la ejecución es verdaderamente descentralizada |
| 4I-A4 | Un squad neural que pierde comunicación (SquadBrain falla) degrada limpiamente a comportamiento individual Phase 4C | Integración | Que el fallback funciona a nivel de squad |
| 4I-A5 | Los rewards no producen comportamiento degenerado: un squad con reward positivo por "formation cohesion" NO se queda parado en formación sin atacar | Comportamiento | Que los rewards incentivan gameplay, no gaming del reward |

### Tests concretos

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `SharedPolicy_SameInputDifferentSeed_DifferentOutput` | Diversidad con sharing | Misma policy + misma observation + seeds distintas → acciones diferentes |
| `SharedPolicy_SameInputSameSeed_SameOutput` | Reproducibilidad con sharing | Misma policy + misma observation + misma seed → misma acción |
| `CTDE_AgentInput_NoGlobalState` | Descentralización | Inspeccionar input tensor del agente → no contiene posiciones/HP de otros agentes |
| `SquadNeuralFailure_FallbackToIndividual` | Degradación limpia | Neural squad policy throws → cada NPC usa su Brain individual → decisiones válidas |
| `FormationReward_DoesNotCauseInaction` | Reward sano | Squad con reward de formación + enemigos atacando → squad ATACA, no solo mantiene formación |
| `TrainingConvergence_5v5_SquadScenario` | Capacidad de aprendizaje | 5v5 training → reward promedio aumenta en 1000 episodes |

---

## Fases 5-7 (visión a largo plazo)

Las fases de visión no tienen tests detallados definidos todavía. Se definirán cuando las fases previas establezcan los patrones y las métricas baseline. Sin embargo, cada fase heredará los gates invariantes:

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

---

## Cómo evoluciona este documento

Este documento se actualiza ANTES de implementar cada fase. Los tests propuestos se convierten en tests reales durante la implementación. Los criterios marcados como "a calibrar" (ej: >90% acuerdo semántico) se calibran con datos reales y se fijan como hard gates una vez establecidos.

Si un test pasa sin probar la propiedad que dice probar, el test está mal escrito y debe corregirse. Un test verde que no demuestra nada es peor que no tener test: da falsa confianza.
