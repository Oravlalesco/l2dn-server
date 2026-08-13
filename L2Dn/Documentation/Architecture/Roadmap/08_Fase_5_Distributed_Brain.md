# Fase 5: Distributed / Accelerated Brain

**Estado:** Visión a largo plazo
**Fecha:** 12 de Agosto de 2026

## 1. Objetivo

Distribuir las partes más pesadas del razonamiento (como inferencias de redes neuronales y políticas de escuadrón) a *workers* externos, aprovechando aceleración por hardware (GPU) o escalado horizontal, mientras se mantiene la latencia crítica y la autoridad dentro del GameServer.

## 2. Prerequisitos

- Fase 4I-B (Learned Squad Policy) completada.
- Recolección de datos sobre número de NPCs activos, inferencias por segundo y latencias del pipeline local.

## 3. Diagrama de arquitectura

```mermaid
flowchart TD
    GS[GameServer - Authoritative World]
    
    subgraph Local Node
        Gateway[Intent Gateway]
        AdviceStore[NpcPolicyAdviceStore]
        GrpcClient[GrpcBrainClient\n(L2Dn.Npc.Transport.Grpc)]
        LocalBrain[Local Brain\n(L2Dn.Npc.Brain)]
        Reflex[ReflexBrain]
        Tactical[TacticalBrain]
        
        LocalBrain --> Reflex
        LocalBrain --> Tactical
        AdviceStore -.->|Inyecta NpcPolicyAdvice?| LocalBrain
        Gateway <--> LocalBrain
    end
    
    subgraph Remote Node GPU / Batch
        RemoteBrain[Remote Brain Worker]
        Squad[Squad Policy]
        Neural[Neural Policy]
        Encounter[Encounter Intelligence]
        RemoteBrain --> Squad
        RemoteBrain --> Neural
        RemoteBrain --> Encounter
    end
    
    GS <--> Gateway
    GrpcClient <-->|gRPC / IPC - Fallback local| RemoteBrain
    GrpcClient --> AdviceStore
```

## 4. Piezas a crear

| Nombre | Ensamblado | Archivo propuesto | Propósito |
|---|---|---|---|
| `IRemoteBrainWorker` | `L2Dn.Npc.Contracts` | `IRemoteBrainWorker.cs` | Contrato para comunicación con el worker remoto. |
| `GrpcBrainClient` | `L2Dn.Npc.Transport.Grpc` | `GrpcBrainClient.cs` | Cliente gRPC para solicitar decisiones al backend remoto. |
| `BrainBatchDispatcher` | `L2Dn.Npc.Transport.Grpc` | `BrainBatchDispatcher.cs` | Agrupa solicitudes de estado para batch inference (ONNX/GPU). |
| `RemotePolicyConfig` | `L2Dn.Npc.Transport.Grpc` | `RemotePolicyConfig.cs` | Configuración de endpoints, latencias máximas y batch sizes. |

## 5. Especificación detallada

- **Distribución Selectiva:** Solamente el razonamiento de alto nivel (Estrategia, Squad, Neural) se envía por la red.
- **Vectorización:** NO transmitir `NpcPerceptionSnapshot` crudo por red. El GameServer serializa y transmite `NpcPolicyObservationV1` (vectorizado/normalizado) directo al backend.
- **Worker Devuelve Advice (ADR-016):** Los workers externos calculan la política pero devuelven un `NpcPolicyAdvice` (o `SquadDirective`), NUNCA un `NpcIntent`. El Intent solo puede ser emitido localmente para ser validado por el GameServer.
- **Batch Inference & Coalescing:** `BrainBatchDispatcher` acumula estados en una ventana muy corta de tiempo y los envía para predicción masiva. Se aplica coalescing (agrupamiento por modelo).
- **Resiliencia & Control de Flujo:**
  - **Backpressure & Bounded Inference Queue:** Límite estricto de solicitudes en vuelo.
  - **Drop Stale Observations:** Descartar observaciones antiguas si la red se congestiona.
  - **Circuit Breaker & Bulkhead Isolation:** Proteger al GameServer aislando fallos del backend remoto.
- **Tolerancia a fallos:** El cliente `GrpcBrainClient` debe usar el fallback local definido en el ADR-006 si el worker remoto no responde.

## 6. Ownership

| Componente | Responsabilidad | Equipo / Rol |
|---|---|---|
| Backend Remoto (Workers) | Alojamiento de modelos ONNX, inferencia GPU | IA Engineer |
| Cliente gRPC & Dispatcher | Batching, serialización, manejo de red | Core Server Dev |
| Fallback System | Garantizar que el NPC no se quede inactivo si la red falla | Core Server Dev |

## 7. Lo que NO incluye

- **Reflex remoto:** Los reflejos (ej. *attack range*, *basic attack*) NUNCA cruzan la red.
- **Autoridad:** El worker no puede aplicar daño ni mover personajes. Solo devuelve directivas o advice (NpcPolicyAdvice). NUNCA devuelve un Intent (ADR-016).
- **Critical path dependency:** El loop del GameServer nunca se bloquea esperando a la red.

## Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 5-A1 | Reflex Brain NUNCA depende de comunicación remota | Arquitectura | Critical path es local |
| 5-A2 | Corte de red de 2s activa fallback local sin NPC congelado | Integración | ADR-006 funciona bajo partición |
| 5-A3 | Batch inference de 256 NPCs en GPU completa en < 10ms | Rendimiento | Justifica la distribución |
| 5-A4 | Latencia P99 de gRPC roundtrip < 20ms en misma máquina | Rendimiento | Overhead de red aceptable |

> Especificación completa de tests: [`11_Criterios_Aceptacion_y_Testing.md`](11_Criterios_Aceptacion_y_Testing.md)

## Tests requeridos

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `Reflex_NeverCallsRemote` | Localidad | Reflex Brain → cero llamadas gRPC |
| `NetworkPartition_FallbackActivates` | Tolerancia | Simular corte 2s → fallback local → NPCs actúan |
| `BatchInference_256NPCs_Under10ms` | Rendimiento | 256 tensors → inferencia GPU → < 10ms |
| `gRPC_Roundtrip_P99_Under20ms` | Latencia | 1000 roundtrips → P99 < 20ms |

## 9. Telemetría

- `l2dn.brain.remote.batch_size` (Histogram): Tamaño de los batches enviados a inferencia.
- `l2dn.brain.remote.latency_ms` (Histogram): Latencia round-trip de inferencia externa.
- `l2dn.brain.remote.fallback_count` (Counter): Cantidad de veces que se aplicó fallback por latencia.

## 10. Configuración

- `L2DN_BRAIN_REMOTE_ENABLED` (bool)
- `L2DN_BRAIN_REMOTE_ENDPOINT` (string)
- `L2DN_BRAIN_REMOTE_TIMEOUT_MS` (int) - Default: 50
- `L2DN_BRAIN_BATCH_WINDOW_MS` (int) - Default: 15

## 11. Rollback

1. Cambiar `L2DN_BRAIN_REMOTE_ENABLED=false` desactivará de inmediato los clientes remotos, forzando a los Brains a correr en los módulos locales (Tactical/Strategy predeterminados).

## 12. Relación con fases adyacentes

- **Consume:** Toda la carga procesal acumulada hasta la Fase 4I-B (incluyendo MARL y Learned Squad).
- **Provee a:** Fase 6 y Fase 7, donde el coste computacional será masivo y el worker distribuido será el único medio viable.

## 13. Experimentos / laboratorio

- Levantar instancias de Triton Inference Server (ONNX) y medir overhead de gRPC contra invocaciones locales C#.
- Probar el impacto en CPU y memoria al manejar 10,000 NPCs remotos en lugar de locales.
