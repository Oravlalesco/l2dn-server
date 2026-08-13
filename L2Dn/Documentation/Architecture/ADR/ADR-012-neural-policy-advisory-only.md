# ADR-012: Neural Policy produce preferencias sobre acciones, no intents directos

Estado: aceptada (revisión 2)

## Contexto

El programa de modernización introduce redes neuronales como capa de decisión para NPCs. Debe definirse el límite de autoridad entre la red neuronal y el pipeline existente de intents.

**Revisión 2**: Alinea este ADR con ADR-013 (CandidateSet como Action Mask) y ADR-017 (PolicyArbitrator Semantics).

## Decisión

La Neural Policy produce **action preferences** (logits por candidato elegible), nunca `NpcIntent` directos. El `PolicyArbitrator` selecciona el candidato ganador. El `TacticalIntentBuilder` construye el intent concreto. El `IntentGateway` valida el mundo real.

```text
Strategy × Role
    ↓
Reflex Brain (safety / leash)
    ↓ si no Reflex Intent
TacticalActionEvaluator.BuildCandidates()
    ↓
NpcTacticalCandidateSet              ← candidatos con eligibility
    ↓
PolicyArbitrator                     ← combina deterministic + neural
    ├─ Disabled → argmax(deterministic scores)
    ├─ Shadow   → deterministic ejecuta / neural compara
    └─ Enabled  → neural logits → softmax → selection
    ↓
SelectedCandidate                    ← hasta aquí llega la red
    ↓
TacticalIntentBuilder                ← construye intent concreto
    ↓
NpcIntent
    ↓
IntentGateway                        ← validación autoritativa
    ↓
GameServer                           ← ejecución
```

## Consecuencias

- La red no puede bypasear cooldowns, mana, rango, geodata ni lifecycle.
- Un modelo defectuoso selecciona un candidato subóptimo entre los elegibles, no una acción ilegal.
- El fallback a determinístico solo requiere que PolicyArbitrator use argmax(deterministic scores).
- Shadow comparison compara selección neural vs selección determinista sin riesgo de ejecución.
- El modelo no necesita conocer la semántica de intents ni paquetes de red.
- Reflex NUNCA depende de la policy (ADR-006 extendido).
- La red recibe como input los candidatos elegibles del CandidateSet (ADR-013), no un action mask separado.
