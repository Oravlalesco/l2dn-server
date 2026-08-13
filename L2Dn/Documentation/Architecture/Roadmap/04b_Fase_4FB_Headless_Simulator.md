# Fase 4F-B: Headless Simulator

**Estado:** Diseño  
**Fecha:** 12/08/2026

## 1. Objetivo
Construir un simulador de combate headless (sin red, sin DB principal) reutilizando la mayor cantidad posible de lógica real del GameServer, para permitir Reinforcement Learning real (no solo Imitation Learning).

## 2. Prerequisitos
- Fase 4F-A, Fase 4G, y Fase 4H completadas.

## 3. Diagrama de arquitectura

```mermaid
flowchart TD
    subgraph Headless L2Dn Server
    Env[Simulated Env Context]
    GS[GameServer Logic]
    Agents[Synthetic Player Agents]
    end
    
    subgraph RL Framework (Python)
    RLlib[RLlib / Gymnasium]
    Agent[RL Agent]
    end
    
    RLlib <-->|Actions / Observations (gRPC)| Env
```

## 4. Piezas a crear

| Nombre | Ensamblado | Archivo propuesto | Propósito |
|---|---|---|---|
| `HeadlessSimContext` | `L2Dn.Npc.Training.Sim` | `HeadlessSimContext.cs` | Contenedor ligero del mundo. |
| `SyntheticPlayerAgent` | `L2Dn.Npc.Training.Sim` | `SyntheticPlayerAgent.cs` | Bot programado que simula un jugador atacando. |
| `GymnasiumWrapper` | `L2Dn.Npc.Training.Sim` | `Network/GymnasiumWrapper.cs` | Expone un entorno compatible con RL (State, Action, Reward, NextState). |

## 5. Especificación detallada

- Un replay solo sabe qué sucedió (Behavior Cloning). El RL necesita `S(t) + A(t) → S(t+1)`, o sea, simular qué pasa si toma otra decisión.
- Para evitar el *sim-to-real gap*, no escribiremos un emulador simplificado de L2 en Python. L2Dn en C# debe poder correr en un modo "Lab" de alta velocidad, generando miles de combates por segundo y alimentando a un agente de RL.

## 6. Ownership

| Componente | Responsabilidad | Equipo / Rol |
|---|---|---|
| Headless Engine | Inicialización parcial del servidor | Core Server Dev |
| Env Wrapper | Exponer la API tipo Gymnasium | ML Engineer |

## 7. Lo que NO incluye
- Gráficos o cliente.
- DB de persistencia.

## 8. Criterios de aceptación (Gates de completación)

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4FB-1 | Arranque del modo Headless en < 2 segundos | Rendimiento | Inicialización ágil para training |
| 4FB-2 | Procesamiento de 10,000 steps de combate / segundo | Rendimiento | Factibilidad para RL masivo |
| 4FB-3 | Sim-to-Real nulo (el cálculo de daño es el mismo código de producción) | Arquitectura | Código compartido de verdad |

## 9. Tests requeridos

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `Headless_CombatStep_MatchesRealWorld` | Correctitud | Combate en lab == Mismo daño y fórmulas que en GameServer |

## 10. Telemetría
- `l2dn.sim.steps_per_second`
- `l2dn.sim.episode_length`

## 11. Configuración
- Modo de booteo de servidor: `-mode=HeadlessRL`

## 12. Rollback
- Al ser una herramienta paralela/offline, no afecta al server de producción.

## 13. Relación con fases adyacentes
- **Provee a:** Fases 4I-A y 4I-B (que necesitan este simulador para entrenar multi-agente).

## 14. Experimentos / laboratorio
- Medir la velocidad de simulación pura en un solo núcleo.
