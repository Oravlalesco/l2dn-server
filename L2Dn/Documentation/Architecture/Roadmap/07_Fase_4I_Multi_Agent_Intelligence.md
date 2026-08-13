# Fase 4I — Multi-Agent Intelligence

**Estado:** Diseño  
**Fecha:** 12/08/2026

## Objetivo
Introducir Multi-Agent Reinforcement Learning (MARL) para lograr que múltiples NPCs aprendan a cooperar dinámicamente. Utilizaremos la arquitectura CTDE (Centralized Training, Decentralized Execution) y Parameter Sharing para crear escuadrones tácticos, roles sinérgicos y comportamientos grupales orgánicos que reemplacen las lógicas hardcodeadas de escuadrón.

## Prerequisitos
- Fase 4H completada (Stochastic Policy en producción estable).
- Sistema de Squad/Commander base de Phase 4C operativo (Directivas).

## Diagrama de arquitectura

**Arquitectura CTDE (Centralized Training, Decentralized Execution):**

```mermaid
flowchart TD
    subgraph Entrenamiento (Offline / Environment)
    Global[Global State\nPosiciones, HP, Roles, Acciones] --> Trainer[RLlib Trainer]
    Trainer --> PolicyUpdate[Actualización de Política Central]
    end
    
    subgraph Producción (Decentralized Execution)
    PolicyUpdate -.-> |Exportación ONNX| NpcPolicy[Shared Neural Policy]
    
    Obs1[Percepción Local Mob 1\nRol: Frontliner] --> NpcPolicy
    NpcPolicy --> Action1[Acción Mob 1]
    
    Obs2[Percepción Local Mob 2\nRol: Archer] --> NpcPolicy
    NpcPolicy --> Action2[Acción Mob 2]
    
    Obs3[Percepción Local Mob 3\nRol: Support] --> NpcPolicy
    NpcPolicy --> Action3[Acción Mob 3]
    end
```

## Piezas a crear

| Nombre | Ensamblado | Archivo Propuesto | Propósito |
|---|---|---|---|
| `SquadDirective` | `L2Dn.Npc.Contracts` | `Directives/SquadDirective.cs` | Contrato de información de escuadrón que se pasa como input adicional a la red. |
| `SharedPolicyLoader` | `L2Dn.Npc.Brain` | `Policies/SharedPolicyLoader.cs` | Carga políticas por arquetipo (Parameter Sharing) en lugar de instanciar 1 por NPC. |
| `CombatAssignment` | `L2Dn.Npc.Contracts` | `Models/CombatAssignment.cs` | Etiqueta de rol táctico dinámico (Frontliner, Flanker, Healer). |
| `AgentRewardTracker` | `L2Dn.Npc.Brain` | `Training/AgentRewardTracker.cs` | (Solo en lab/entrenamiento) Rastrea señales de recompensa. |
| `MultiAgentEnv` | (Standalone Python) | `rllib_env/l2_multi_agent.py` | Entorno externo de RLlib para self-play y simulación. |

## Especificación detallada

### 1. Parameter Sharing
En lugar de que cada NPC tenga su propia red (lo cual es inviable para memoria y entrenamiento), compartimos pesos según la clase táctica:
- `frontliner_policy_v4.onnx` → Usado por todos los orcos melee, caballeros, etc.
- `ranged_policy_v3.onnx` → Usado por arqueros y magos ofensivos.
- `support_policy_v2.onnx` → Usado por healers y buffers.
Esto escala excelentemente y permite que un orco melee aprenda de las experiencias de un esqueleto melee durante el entrenamiento.

### 2. Reward System Propuesto (Training)
Para que los agentes aprendan a cooperar, el entorno de entrenamiento usa recompensas calibradas:
| Señal | Reward | Notas |
|---|---:|---|
| Squad win | +100 | Recompensa global, promueve altruismo. |
| Enemy killed | +30 | |
| Support survived | +20 | Enseña a los frontliners a proteger al healer. |
| Successful interrupt | +10 | Casteo enemigo cancelado. |
| Maintain formation | +5 | Evita que se dispersen demasiado. |
| Member killed | -20 | Penalización por perder un aliado. |
| Invalid Intent | -10 | Intentar acción ilegal (ej. cast sin MP). |
| Leash violation | -20 | Salirse de la zona de persecución. |
| Target flapping | -5 | Cambiar de target erráticamente. |
| Oscillation | -5 | Moverse adelante y atrás sin sentido. |

### 3. Falibilidad Humana
**La IA debe poder perder**. No diseñamos un enjambre perfecto y robótico. El objetivo es que parezca orgánico. Si el *temperature* está configurado a nivel "Normal", el Healer a veces puede distraerse, o un orco puede huir prematuramente. Esto es deseado.

## Ownership

| Componente | Capa | Responsabilidad |
|---|---|---|
| `SharedPolicyLoader` | Brain | Cachear y despachar modelos ONNX según el rol del NPC, optimizando memoria RAM. |
| `SquadDirective` | Contracts | Proveer el contexto reducido del escuadrón a cada agente de forma descentralizada. |
| Entorno RLlib | Offline | Conducir las sesiones de Self-Play y calcular las recompensas globales. |

## Lo que NO incluye
- El servidor `GameServer` no entrena modelos en runtime. El servidor solo ejecuta políticas.
- La ejecución en red distribuida de escuadrones; cada NPC sigue siendo un actor local independiente, coordinado por la política.
- Comunicación P2P explícita entre NPCs. Se comunican implícitamente por lo que perciben.

## Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4I-A1 | Squad con Neural Squad Policy produce directivas que difieren de deterministas pero no degenera en oscilación | Comportamiento | Policy de squad aprendida produce comportamiento coherente |
| 4I-A2 | 12 Frontliners con misma `frontliner_policy_v4` se coordinan pero no son idénticos (seeds diferentes) | Comportamiento | Shared policies escalan sin uniformidad robótica |
| 4I-A3 | Cada NPC individual solo recibe su percepción + CombatAssignment + SquadDirective, NUNCA estado global | Arquitectura | Ejecución verdaderamente descentralizada |
| 4I-A4 | Squad neural que pierde comunicación degrada a comportamiento individual Phase 4C | Integración | Fallback funciona a nivel de squad |
| 4I-A5 | Rewards no producen comportamiento degenerado (ej: squad con reward de formación NO se queda parado sin atacar) | Comportamiento | Rewards incentivan gameplay, no gaming del reward |

> Especificación completa de tests: [`11_Criterios_Aceptacion_y_Testing.md`](11_Criterios_Aceptacion_y_Testing.md)

## Tests requeridos

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `SharedPolicy_SameInputDifferentSeed_DifferentOutput` | Diversidad | Misma policy + seeds distintas → acciones diferentes |
| `SharedPolicy_SameInputSameSeed_SameOutput` | Reproducibilidad | Misma policy + misma seed → misma acción |
| `CTDE_AgentInput_NoGlobalState` | Descentralización | Input tensor → no contiene posiciones/HP de otros agentes |
| `SquadNeuralFailure_FallbackToIndividual` | Degradación limpia | Policy throws → NPC usa Brain individual → decisiones válidas |
| `FormationReward_DoesNotCauseInaction` | Reward sano | Reward de formación + enemigos atacando → squad ATACA |
| `TrainingConvergence_5v5_SquadScenario` | Aprendizaje | 5v5 training 1000 episodes → reward promedio aumenta |

## Telemetría

- `l2dn.npc.policy.shared.cache_hits`: Accesos al caché de modelos compartidos.
- `l2dn.npc.squad.cohesion`: Métrica que calcula la dispersión espacial promedio de un escuadrón en combate.
- `l2dn.npc.squad.survival_rate`: Ratio de supervivencia del escuadrón entero (Gauge).
- `l2dn.npc.combat.invalid_intent_ratio`: Ratio de Intents rechazados por el Gateway (debe ser casi 0).

## Configuración

- `NPC_MARL_ENABLED`: Bool.
- `NPC_POLICY_FRONTLINE_PATH`: String (Ruta al modelo melee).
- `NPC_POLICY_RANGED_PATH`: String (Ruta al modelo ranged).
- `NPC_POLICY_SUPPORT_PATH`: String (Ruta al modelo support).

## Rollback
- Desactivar `NPC_MARL_ENABLED`. Los NPCs volverán a procesar el `SquadDirective` a través del `SquadBrain` táctico determinista diseñado en la Fase 4E.

## Relación con fases adyacentes
- **Consume de:** Fase 4H (Usa su pipeline estocástico y arquitectura ONNX) y Fase 4C (Conceptos de Roles de escuadrón).
- **Provee a:** Fase 5 (Boss/Raid Encounters), donde este modelo multi-agente se aplicará a dinámicas de Raid (Minions vs Raid Boss vs Jugadores).

## Experimentos / laboratorio

- **Experimento Lab Squad A:** Un escuadrón de 5 Orcos (Commander + 2 Frontliners + Archer + Shaman) ejecutando el SquadBrain determinista (4E).
- **Experimento Lab Squad B:** El mismo escuadrón ejecutando la Neural Squad Policy en Shadow Mode (evaluar si las recomendaciones difieren de las reglas duras).
- **Experimento Lab Squad C (MARL Real):** Sustituir completamente el SquadBrain por la política MARL. Medir en self-play contra el Squad A si logran un *win-rate* superior o comportamientos emergentes tácticos inesperados (ej: focus fire, kiting).
