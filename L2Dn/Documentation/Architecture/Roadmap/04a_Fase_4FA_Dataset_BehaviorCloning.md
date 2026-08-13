# Fase 4F-A: Dataset & Behavior Cloning

**Estado:** Diseño  
**Fecha:** 12/08/2026

## 1. Objetivo
Establecer un pipeline determinista y offline para la captura de replays del GameServer, y el procesamiento posterior de un dataset para Imitation Learning (Behavior Cloning). Aislar la lógica de recompensa (Reward) del runtime del juego.

## 2. Prerequisitos
- Fase 4E (Squad Intelligence base) completada o en su defecto 4D (Policy Foundation).
- Sistema de replay o log binario capaz de guardar eventos de alta frecuencia.

## 3. Diagrama de arquitectura

```mermaid
flowchart TD
    GS[GameServer] -->|Raw Events| Replay[Replay Capture]
    Replay -->|Replay files| Exporter[DatasetBuilder Offline]
    Exporter -->|Applies RewardFunction| Dataset[Training Dataset]
    
    subgraph Model Training (Python)
    Dataset --> PyTrain[Behavior Cloning]
    PyTrain --> Model[ONNX Model]
    end
    
    Model --> GS
    
    classDef offline fill:#ddd,stroke:#333,stroke-width:2px;
    class Exporter,Dataset,PyTrain offline;
```

## 4. Piezas a crear

| Nombre | Ensamblado | Archivo propuesto | Propósito |
|---|---|---|---|
| `ReplayCaptureService` | `L2Dn.Npc.Brain` | `Training/ReplayCaptureService.cs` | Captura hechos crudos (HP delta, outcomes) al disco. |
| `DatasetBuilder` | `L2Dn.Npc.Training.Export` | `DatasetBuilder.cs` | Herramienta offline. Lee replays y aplica `RewardFunction`. |
| `NpcTrainingReward` | `L2Dn.Npc.Training.Contracts` | `NpcTrainingReward.cs` | Calcula recompensa de manera offline, sin acoplar al GameServer. |
| `FeatureSchemaV1` | `L2Dn.Npc.Contracts` | `Models/FeatureSchemaV1.cs` | Define dinámicamente N dimensiones del input vector. |
| `PolicyObservationV1` | `L2Dn.Npc.Contracts` | `Models/PolicyObservationV1.cs` | Vector normalizado para enviar a Python. |

## 5. Especificación detallada

- **Replay Capture:** Guarda hechos y outcomes (HP delta, damage dealt, ally death, formation metrics). NO evalúa el reward en vivo.
- **DatasetBuilder Offline:** Lee los replays brutos y les aplica la función de recompensa. Permite iterar la función de recompensa sin tener que jugar miles de horas de nuevo.
- **Train/Serve Skew Prevention:** La misma rutina exacta que produce `PolicyObservationV1` en producción debe producirlo durante el export de datos. Python recibe el vector crudo, NO interpreta objetos de alto nivel.
- **Dimensionalidad Dinámica:** `FeatureSchemaV1.Dimension` determina N. No forzar un 128 fijo. Tampoco forzar arquitectura MLP 128->128->64.

## 6. Ownership

| Componente | Responsabilidad | Equipo / Rol |
|---|---|---|
| Replay Capture | Captura asíncrona de alto rendimiento | Core Server Dev |
| DatasetBuilder | Procesamiento offline, reward function | ML Engineer |
| Feature Normalization | Proveer inputs sin skew | Core Server Dev |

## 7. Lo que NO incluye
- Entrenamiento en vivo (Online RL).
- Un simulador headless (eso es la fase 4F-B).
- Inferencia en el servidor.

## 8. Criterios de aceptación (Gates de completación)

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4FA-1 | Replay capture usa <2% de CPU extra | Rendimiento | Sistema de log es eficiente |
| 4FA-2 | Mismo replay con distinta RewardFunction produce distinto dataset offline | Diseño | Desacople de recompensa |
| 4FA-3 | Dataset Protection: Validar F1, Precision, Recall por clase en test split | ML | El modelo generaliza |
| 4FA-4 | Train/Serve Skew nulo | Integración | Las dimensiones coinciden exactamente con producción |

## 9. Tests requeridos

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `Replay_RecordsRawFacts_NoReward` | Desacople | Loguear combate → archivo binario sin campo de recompensa |
| `DatasetBuilder_AppliesReward` | Procesamiento offline | Replay binario + FunctionX → Dataset con RewardX |
| `FeatureSchema_DynamicDimension` | Flexibilidad | Schema v1 (Dim=80) → array float[80] |

## 10. Telemetría
- `l2dn.training.replay.bytes_written` (Counter): Volumen de replay.
- `l2dn.training.dataset.generated_rows` (Counter): Filas de dataset offline.

## 11. Configuración
- `L2DN_NPC_REPLAY_CAPTURE_ENABLED` (bool)
- `L2DN_NPC_REPLAY_DIR` (string)

## 12. Rollback
- Desactivar `L2DN_NPC_REPLAY_CAPTURE_ENABLED`.

## 13. Relación con fases adyacentes
- **Consume de:** Las métricas y percepciones estabilizadas en Fase 4C/4E.
- **Provee a:** Fase 4G (donde se usará el ONNX exportado).

## 14. Experimentos / laboratorio
- Hold-out templates: Entrenar el Behavior Cloning excluyendo a una clase de monstruos (ej. Orcs) y probar si el modelo generaliza a ellos sin haberlos visto.
