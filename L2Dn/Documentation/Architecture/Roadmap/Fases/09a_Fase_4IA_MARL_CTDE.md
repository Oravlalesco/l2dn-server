# Fase 4I-A: MARL / CTDE Individual Policies

**Estado:** Diseño  
**Fecha:** 12/08/2026

## 1. Objetivo
Introducir un esquema Multi-Agent Reinforcement Learning (MARL) usando Centralized Training Decentralized Execution (CTDE) para políticas individuales compartidas.

## 2. Prerequisitos
- Fase 4F-B (Simulador Headless).

## 3. Diagrama de arquitectura

```mermaid
flowchart TD
    subgraph Centralized Training (Offline)
    Critic[Global Critic]
    Actor[Shared Actor Policy]
    Env[Headless Simulator]
    Env --> Critic
    Critic --> Actor
    end
    
    subgraph Decentralized Execution (Production)
    Obs1[Local Obs NPC 1] --> ActorPolicy1[ONNX Policy]
    Obs2[Local Obs NPC 2] --> ActorPolicy2[ONNX Policy]
    ActorPolicy1 --> Action1
    ActorPolicy2 --> Action2
    end
```

## 4. Piezas a crear

| Nombre | Ensamblado | Archivo propuesto | Propósito |
|---|---|---|---|
| `AllyObservation` | `L2Dn.Npc.Contracts` | `Models/AllyObservation.cs` | Vector que describe a los aliados cercanos. |
| `NpcPolicyObservationV2` | `L2Dn.Npc.Contracts` | `Models/NpcPolicyObservationV2.cs` | Nueva versión de la obs que incluye arrays de aliados locales. |

## 5. Especificación detallada

- **CTDE (Centralized Training with Decentralized Execution):**
  - **Producción:** El agente (Actor) solo usa percepción local (`NpcPolicyObservationV2`), pero AHORA puede percibir a aliados cercanos (HP, distancia, estado) si están en su radio visual/aggro. No tiene información global privilegiada.
  - **Entrenamiento:** El Crítico (Critic) sí ve todo el mapa para calcular mejores recompensas y ayudar al entrenamiento del Actor.
- **Parameter Sharing:** Múltiples agentes del mismo tipo (ej. Arqueros) comparten los mismos pesos (la misma red neuronal). El experimento decidirá si usar políticas separadas o una sola con *role embeddings*.
- **Self-Play:** Entrenar contra un opponent pool diverso (v1, v2, scripts legacy, deterministas) para asegurar robustez.

## 6. Ownership

| Componente | Responsabilidad | Equipo / Rol |
|---|---|---|
| MARL Architecture | Implementar MAPPO/QMIX en Python | ML Engineer |
| Percepción Local | Añadir info de aliados a la observación C# | Core Server Dev |

## 7. Lo que NO incluye
- Un controlador central que le dé órdenes a los NPCs. Ellos toman decisiones independientes (decentralized).

## 8. Criterios de aceptación (Gates de completación)

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4IA-1 | `NpcPolicyObservationV2` incluye a lo sumo N aliados cercanos, manteniendo el tamaño del tensor fijo | Diseño | El input de la red es compatible con batching |
| 4IA-2 | El self-play converge contra el baseline determinista | Entrenamiento | MARL es superior a scripts en coordinación implícita |
| 4IA-3 | Decentralized Execution no bloquea threads entre NPCs | Rendimiento | Las inferencias siguen siendo aisladas |

## 9. Tests requeridos

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `Observation_IncludesAllies_SortedByDistance` | Correctitud | 5 aliados cerca → array normalizado con 3 más cercanos |

## 10. Telemetría
- `l2dn.marl.allies_perceived` (Histogram)

## 11. Configuración
- `L2DN_MARL_CTDE_ENABLED`

## 12. Rollback
- Volver a `NpcPolicyObservationV1` y desactivar las políticas colaborativas.

## 13. Relación con fases adyacentes
- **Consume:** El simulador headless (4F-B).
- **Provee a:** Fase 4I-B (Learned Squad), donde añadiremos un director jerárquico.

## 14. Experimentos / laboratorio
- **Role Embedding vs Separate Policies:** Probar si una red que recibe `[IsArcher, IsHealer, IsMelee]` como parte de la observación entrena más rápido que 3 redes separadas.
