# Fase 4D — Policy Foundation

**Estado:** Diseño.
**Fecha:** Agosto 2026
**Revisión:** 2 (2026-08-13)

## 1. Objetivo
Introducir la **capa de política intercambiable** que permitirá más adelante conectar redes neuronales (LLM / ML) SIN modificar el pipeline existente. Esta capa se ubica después de la construcción de candidatos tácticos y antes de la selección final, proporcionando consejos probabilísticos que el cerebro táctico evalúa de manera segura.

## 2. Prerequisitos
- Fase 4C (Style × Role) completada y estabilizada.
- Entorno de Telemetría OTLP configurado.

## 3. Diagrama de Arquitectura

```mermaid
graph TD
    A[NpcPerception] -->|Feature Extraction| B[NpcPolicyObservation]
    
    subgraph Brain Pipeline
    A --> S[StrategyBrain]
    S --> R[ReflexBrain]
    R -->|Si no hay Reflex Intent| T[TacticalCandidateBuilder]
    T -->|BuildCandidates| CS[NpcTacticalCandidateSet]
    
    CS --> PA[PolicyArbitrator]
    B --> PolicyLayer{INpcPolicy}
    PolicyLayer -->|NpcPolicyAdvice| PA
    PA -->|Selection| I[NpcIntent]
    end
    
    subgraph Policy Implementations
    PolicyLayer -.-> D[DeterministicNpcPolicy]
    PolicyLayer -.-> E[NoOpNpcPolicy]
    PolicyLayer -.-> F[OnnxNpcPolicy - Future]
    end
    
    I --> IG[Intent Gateway]
    IG --> L[GameServer]
```

## 4. Piezas a crear

| Nombre | Ensamblado/Proyecto | Archivo propuesto | Propósito |
|---|---|---|---|
| `INpcPolicy` | `L2Dn.Npc.Contracts` | `INpcPolicy.cs` | Interfaz de política intercambiable |
| `NpcPolicyObservation` | `L2Dn.Npc.Contracts` | `NpcPolicyObservation.cs` | Representación explícita y versionada del estado |
| `NpcPolicyObservationV1` | `L2Dn.Npc.Contracts` | `NpcPolicyObservationV1.cs` | Primera versión de inputs estructurados |
| `NpcTacticalCandidate` | `L2Dn.Npc.Contracts` | `NpcTacticalCandidate.cs` | Representa un candidato (action, score determinista, eligible, ineligibilityReason) |
| `NpcTacticalCandidateSet` | `L2Dn.Npc.Contracts` | `NpcTacticalCandidateSet.cs` | Conjunto inmutable de candidatos (actúa como Action Masking implícito) |
| `NpcPolicyAdvice` | `L2Dn.Npc.Contracts` | `NpcPolicyAdvice.cs` | Recomendación con causalidad, biases y scores probabilísticos |
| `NpcPolicyAction` | `L2Dn.Npc.Contracts` | `NpcPolicyAction.cs` | Enumeración de acciones abstractas posibles |
| `NpcPolicyVersion` | `L2Dn.Npc.Contracts` | `NpcPolicyVersion.cs` | Versionado con Checksum |
| `NpcPolicyResult` | `L2Dn.Npc.Contracts` | `NpcPolicyResult.cs` | Resultado de la evaluación |
| `PolicyArbitrator` | `L2Dn.Npc.Brain` | `PolicyArbitrator.cs` | Combina scores deterministas con neural advice para seleccionar la acción final |
| `DeterministicNpcPolicy` | `L2Dn.Npc.Brain` | `DeterministicNpcPolicy.cs` | Implementación baseline (Legacy) |
| `NoOpNpcPolicy` | `L2Dn.Npc.Brain` | `NoOpNpcPolicy.cs` | Implementación nula para testing |
| `NpcPolicyEvaluator` | `L2Dn.Npc.Brain` | `NpcPolicyEvaluator.cs` | Coordinador de políticas |

## 5. Especificación Detallada

### NpcPolicyObservationV1
Contiene la abstracción del entorno estructurada. Su tamaño se define por `FeatureSchemaV1.Dimension` y sus campos escalares.
**Regla de oro: NUNCA poner ObjectId/PlayerId como input**.
- **Self:** HP ratio, MP ratio, casting, moving, stunned, distance from home, combat duration, recent damage.
- **Environment:** nearby enemies count, nearest enemy distance, average enemy distance.
- **Intelligence:** Style, Role, FleeAllowed, PreferredRange.

### NpcPolicyAdvice y Causalidad
El `NpcPolicyAdvice` incluye campos estrictos de causalidad para garantizar que un consejo estocástico no se aplique a un estado inválido:
- **Causalidad:** `NpcKey`, `Generation`, `BasedOnStateRevision`, `PolicyEvaluationId`.
- **Temporalidad:** `GeneratedAtWorldTick`, `ExpiresAtWorldTick`.
- **Metadata:** `ModelVersion`, `ConfidenceScore`.
- **Action Biases:** Scores o logits por `NpcPolicyAction`.

*(Nota: La versión 1 de la política NO incluye target selection neural, la selección de target sigue siendo estrictamente determinista del Brain. Para V2 se propondrán Target Candidate Slots).*

### Action Masking mediante NpcTacticalCandidateSet
No existe un "Action Masker" aislado. `TacticalActionEvaluator` evalúa elegibilidad determinista (cooldowns, MP, rangos, disables) y expone `BuildCandidates()`, lo cual produce un `NpcTacticalCandidateSet` inmutable con `score`, `eligible` (bool) y `reason`.
- La política solo procesará o será validada contra el subset de candidatos que sean `eligible == true`. El CandidateSet *ES* la única fuente de la verdad para el Action Masking.

### Arbitraje e Independencia del Reflex
1. **Reflex Brain:** Siempre evalúa primero (ej. targets muertos, out of leash). NUNCA consulta políticas neuronales.
2. Si Reflex no produce Intent, `TacticalCandidateBuilder` genera los candidatos.
3. El `PolicyArbitrator` toma el `NpcTacticalCandidateSet` determinista y el `NpcPolicyAdvice` y computa el ganador final.
4. **Fallback:** Es invariante. Si la política falla, expira, o emite un advice inválido, el Arbitrator automáticamente recae sobre el `score` determinista mayor. No hay flag configurable para desactivar el fallback.

## 6. Ownership

| Componente | Equipo/Rol Responsable |
|---|---|
| `L2Dn.Npc.Contracts` (Policy) | Core Architecture Team |
| `L2Dn.Npc.Brain` (Arbitrator, Evaluator) | AI Engine Team |
| `TacticalCandidateBuilder` | Game Logic Team |

## 7. Lo que NO incluye
- Modificaciones al `ReflexBrain`.
- Implementación real de inferencia con ONNX (eso es Fase 4G).
- Target selection neural.
- `NpcPolicyContext` o estado mutable en `Contracts`. El runtime state (generaciones, cache local) es privado de `Brain`.

## 8. Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4D-A1 | `DeterministicNpcPolicy` genera un advice que, al pasar por el `PolicyArbitrator`, produce idéntico resultado al sistema Phase 4C puro | Comportamiento | Transparencia semántica del pipeline |
| 4D-A2 | `NoOpNpcPolicy` produce advice obsoleto/nulo y fuerza el fallback determinista inmediato sin detener el combate | Comportamiento | Fallback invariante de latencia/fallo |
| 4D-A3 | Un advice con `BasedOnStateRevision` desactualizado es ignorado por el Arbitrator | Comportamiento | Protección de causalidad |
| 4D-A4 | La abstracción de features produce tensores de longitud exactamente `FeatureSchemaV1.Dimension` | Contrato | Formato rígido sin dimensiones hardcodeadas fijas y mágicas |
| 4D-A5 | Acciones con `eligible == false` en `NpcTacticalCandidateSet` NUNCA son seleccionadas sin importar el score neural | Integración | Hard constraints (cooldowns, disables) siempre ganan |

## 8b. Tests requeridos

### Contracts

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `CandidateSet_IsImmutable` | Inmutabilidad | Instancia creada → campos de solo lectura, sin setters |
| `Advice_CausalityFieldsRequired` | Contrato estricto | Creación sin Generation o Revision → fallo |
| `ObservationV1_NoObjectIds` | Aislamiento | Reflection sobre campos → ningún PlayerId/ObjectId |

### Brain

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `Arbitrator_AppliesAdviceOnlyToEligibleCandidates` | Masking estricto | Advice maximiza Flee, pero Flee eligible=false → gana el siguiente mejor elegible |
| `Arbitrator_StaleRevision_TriggersFallback` | Causalidad temporal | Advice con Revision antigua → utiliza scores deterministas puros |
| `TacticalCandidateBuilder_BuildsCorrectMasks` | Validaciones deterministas | Muteado → Skills tienen eligible=false con ineligibilityReason=Silenced |

## 9. Telemetría
- `npc.brain.policy.evaluation_ms`: Tiempo gastado en el Evaluator/Arbitrator.
- `npc.brain.policy.fallback_rate`: Porcentaje de ciclos que cayeron en fallback por latencia o error.
- `npc.brain.policy.stale_advices`: Consejos ignorados por `BasedOnStateRevision` antiguo.

## 10. Configuración
- `NPC_POLICY_MODE`: Valores posibles `Disabled`, `Shadow`, `Enabled`.
- `NPC_POLICY_ADVICE_TTL_TICKS`: Tiempo de vida en ticks antes de considerarse obsoleto.
- `NPC_POLICY_OBSERVATION_VERSION`: Versión del schema a emplear (ej. `V1`).

## 11. Rollback
- Ajustar `NPC_POLICY_MODE` a `Disabled`. El `PolicyArbitrator` simplemente devolverá el candidato determinista de mayor score sin procesar ningún advice.

## 12. Relación con fases adyacentes
- **Consume de:** Fase 4C (Roles y Estilos determinan parte del observation state).
- **Provee a:** Fase 4E (Escuadrones) y 4G (Shadow ONNX). El policy allocator será el punto de entrada para recomendaciones externas.

## 13. Experimentos / laboratorio
- Inyectar una política mock que siempre retorne máxima confianza para `Flee`, y verificar que un NPC en un `TacticalCandidateSet` donde `Flee` es elegible siempre huye, y donde `Flee` no es elegible, continúa atacando con normalidad.
