---
name: l2dn-npc-brain
description: >-
  Guía de la arquitectura del sistema NPC Brain de L2Dn (IA de NPCs).
  Usar cuando el usuario quiera entender o modificar el comportamiento de IA
  de los NPCs, el sistema de decisión Strategy/Reflex/Tactical, el contrato
  NpcIntent/NpcIntentGateway, los modos de despliegue (Legacy/Shadow/Intent),
  o las clases de L2Dn.Npc.Brain y L2Dn.Npc.Contracts.
---

# L2Dn — Sistema NPC Brain

El Brain de NPCs es una arquitectura en capas **determinista-primero** con separación estricta entre decidir y ejecutar. Código en `L2Dn.Npc.Brain/` y `L2Dn.Npc.Contracts/`.

> Documentación de referencia: `L2Dn/Documentation/Architecture/` (ADRs 001–017, NpcBrainPhase3.md, NpcStrategyPhase4.md, etc.)

---

## La regla de oro (ADR-002)

```
El Brain solo PROPONE. GameServer valida y EJECUTA.
```

- `NpcBrainCoordinator` produce un `NpcIntent` inmutable
- Solo `NpcIntentGateway` (en GameServer) valida elegibilidad y ejecuta
- **Nunca** llamar directamente al ejecutor desde el Brain

---

## Capas de decisión — Orden real

Verificado en `NpcBrainCoordinator.DecideWithState`:

```
StrategyBrain
    ↓  (fija pesos y umbrales de estilo de combate)
ReflexBrain
    ↓  (urgencias: si hay reflex → devuelve intent inmediatamente)
TacticalBrain
    ↓  (si no hubo reflex → acción concreta)
NpcIntent  →  NpcIntentGateway  →  GameServer ejecuta
```

### `StrategyBrain`
- Define **pesos y umbrales deterministas** del estilo de combate
- Estilos: `Balanced`, `AggressivePressure`, `RangedControl`, `Survival`
- **No** hace decisiones a largo plazo; lo cognitivo/generativo/memoria está explícitamente fuera de alcance (ADR-007, ADR-010, Phase 4 "Out of scope")
- Configura parámetros que `TacticalBrain` usa para puntuar acciones

### `ReflexBrain`
- Maneja urgencias de alta prioridad: target inválido, leash, retorno a posición, huida
- Si produce un `NpcIntent` → el pipeline **termina aquí** (no llega a Tactical)
- Sin side-effects externos; solo inspecciona estado y emite intent

### `TacticalBrain`
- Genera la acción concreta de combate
- Delega en `TacticalActionEvaluator`: calcula scores de elegibilidad para cada acción candidata
- Solo elige entre acciones `eligible` → el neural/remoto **no puede elegir acciones no elegibles** (action masking, ADR-013)

---

## Entidades clave

### `NpcIntent` y `NpcIntentGateway`
```
NpcIntent       — propuesta de acción inmutable
NpcIntentEnvelope — lleva: NpcKey (con Generation), BasedOnStateRevision,
                    DecisionSequence (trazabilidad causal, ADR-015)
NpcIntentGateway — valida y ejecuta en GameServer (no en Brain)
NpcIntentRejectionReason — 21 razones tipadas (dead_actor, out_of_range,
                            cooldown, ...) — rechazos explícitos, no excepciones
```

### `NpcBrainCoordinator` (en `L2Dn.Npc.Brain`)
- Orquesta el pipeline: Strategy → Reflex → Tactical
- Emite `NpcIntent`; el fallback determinista es invariante — **nunca** devuelve "NPC congelado"

### `NpcThinkCoordinator` (en `L2Dn.GameServer.Model/AI/Npc/Scheduling/`)
- **Entidad distinta** del `NpcBrainCoordinator`
- Decide **cuándo** piensa un NPC (scheduler single-flight, cola acotada)
- Usa lock por NPC para serializar pensamientos concurrentes

### `NpcBrainStateStore`
- Estado mutable por NPC, con lock y generación (`NpcKey` con `Generation`)
- Garantiza que `NpcIntent` obsoleto (de generación anterior) sea rechazado

### `NpcPerceptionSnapshot` (en `L2Dn.Npc.Contracts`)
- Modelo **inmutable** de hechos del mundo visible para el NPC
- Lectura pura; se pasa al Brain para que tome decisiones sin acceder al GameServer

### `NpcPerceptionFacts`
- Clase **estática de helpers puros**: `IsActorOperational`, `SelectTarget`, cálculos de distancia
- **No** es el modelo de percepción; es utilidades sin estado

### `TacticalActionEvaluator`
- Calcula scores y elegibilidad de acciones concretas (devuelve `NpcTacticalScore`)
- El candidato elegible es el "action mask" implícito (ADR-013); la red neural solo elige entre elegibles
- **Nota**: `NpcTacticalCandidateSet` y `BuildCandidates()` son artefactos **planificados** (Fase 4D, en ADRs/roadmap); aún no están implementados en el código

### `NpcStrategyDecisionComparer`
- Compara **decisión baseline vs decisión strategy** para el modo Shadow
- Resultados: `ExactMatch` / `SemanticMatch` / `DifferentAction` / `DifferentTarget` / `DifferentSkill` / `DifferentMovement` / `NotComparable`
- **No** evalúa estrategias de combate; es una herramienta de comparación/trazabilidad

### `NpcStrategyProfileResolver`
- Resuelve el perfil de estrategia para un NPC dado (qué estilo de combate usar)

---

## Modos de despliegue

Rollback sin rebuild, controlado por flags de configuración:

| Modo | Flag `NPC_BRAIN_MODE` | Comportamiento |
|------|----------------------|----------------|
| `Legacy` | `Legacy` | IA clásica de L2J, sin Brain |
| `Shadow` | `Shadow` | Ejecuta Legacy + Brain en paralelo; compara con `NpcStrategyDecisionComparer`; no actúa con Brain |
| `Intent` | `Intent` | Brain controla; Legacy como fallback |

`NPC_STRATEGY_MODE` activa/desactiva la capa `StrategyBrain` independientemente.

---

## Contratos independientes (ADR-008)

`L2Dn.Npc.Brain.csproj` referencia **solo** `L2Dn.Npc.Contracts`, nunca `L2Dn.GameServer.Model`. Esto permite:
- Testear el Brain sin levantar el GameServer
- Desplegar el Brain como proceso remoto (ADR-016)
- Evitar acoplamiento circular

---

## Reproducibilidad y Shadow mode (ADR-011/012)

```csharp
// NpcBrainReplayRunner: reproduce una secuencia de percepciones y compara resultados
// NpcStrategyDecisionComparer: valida que la nueva lógica es semánticamente equivalente
// RNG inyectable: seed fija en tests para determinismo
```

---

## Elegibilidad estricta (ADR-013)

```csharp
// NpcBrainEligibility exige tipo exacto (no subclases):
actor.GetType() == typeof(Monster)
&& actor.getAI() is AttackableAI ai && ai.GetType() == typeof(AttackableAI)
```

Los NPCs que no cumplan estos criterios exactos no pueden usar el Brain.

---

## LLM / Neural: fuera del loop de combate

- LLM está planificado para Fase 7+, solo como **advisory cognitivo** (recomendación), nunca síncrono en el tick de combate
- La red neural (si existe) solo elige entre candidatos `eligible` del `TacticalActionEvaluator`
- El Brain **siempre produce un Intent local**; lo remoto devuelve `NpcPolicyAdvice` / `SquadDirective` / `EncounterAdvice`, no un `NpcIntent`

---

## Flujo completo de un tick de decisión

```
NpcThinkCoordinator (scheduler) → decide que es momento de pensar
  ↓
NpcBrainStateStore.GetState(npcKey) → obtiene NpcBrainState
  ↓
NpcPerceptionSnapshot (hechos del mundo, inmutable)
  ↓
NpcBrainCoordinator.DecideWithState(state, perception)
  ├── StrategyBrain.Decide() → NpcStrategyDecision (pesos/umbrales de estilo)
  ├── ReflexBrain.Decide() → NpcIntent? (si urgencia → fin)
  └── TacticalBrain.Decide(strategy) → NpcIntent (vía TacticalActionEvaluator)
  ↓
NpcIntentEnvelope (Intent + NpcKey.Generation + BasedOnStateRevision + DecisionSequence)
  ↓
NpcIntentGateway.Validate() → rechaza si obsoleto/inelegible (NpcIntentRejectionReason)
  ↓
GameServer ejecuta la acción
```
