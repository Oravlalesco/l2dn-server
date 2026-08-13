# ADR-017: Policy Arbitration Semantics — Opción A (Neural elige directamente)

Estado: aceptada

## Contexto

`PolicyArbitrator` combina scores deterministas con neural advice para seleccionar un candidato táctico. La revisión 2 del roadmap lo introdujo pero no definió cómo se realiza la combinación. Esto es crítico porque los scores deterministas y los logits neuronales están en escalas incomparables.

## Decisión

Para V1 se adopta **Opción A — Neural elige directamente entre candidatos válidos**.

```text
NpcTacticalCandidateSet
    ↓
eligible candidates only
    ↓
    ├─ Disabled → argmax(deterministic scores)
    ├─ Shadow   → argmax(deterministic scores) ejecuta
    │              argmax/sample(neural logits) compara
    └─ Enabled  → neural logits
                    ↓
                   mask ineligible (logit = -∞)
                    ↓
                   softmax
                    ↓
                   argmax o sampling (según NpcAiDifficultyTier)
                    ↓
                   SelectedCandidate
```

### Lo que esto significa

- **Disabled**: los scores deterministas determinan el ganador (argmax). Equivalente a Phase 4C.
- **Shadow**: el ganador determinista se ejecuta. Además, se calcula el ganador neural. Se comparan para telemetría (ExactMatch/SemanticMatch/Different). El gameplay no cambia.
- **Enabled**: la red neuronal produce logits por cada candidato elegible. Se aplica softmax sobre logits enmascarados. Se selecciona por argmax (determinista tier) o sampling (estocástico tier).
- **Neural unavailable/stale/error**: fallback a argmax(deterministic scores). Invariante. No configurable.

### Los scores deterministas sirven para

1. Fallback cuando neural no está disponible.
2. Expert label para Behavior Cloning (el candidato ganador determinista es el label de entrenamiento).
3. Comparación en Shadow mode.

### Los scores deterministas NO sirven para

1. Sumarse con logits neuronales (escalas incomparables).
2. Modificarse por "neural deltas" (complejidad innecesaria en V1).

### Opción B (reservada para futuro)

```text
EffectiveScore = DeterministicScore + clamp(NeuralDelta, -K, +K)
```

Si la evidencia de Shadow muestra que la red hace buenas correcciones marginales pero no buenas decisiones absolutas, se puede migrar a Opción B. Eso requiere entrenar específicamente para producir deltas, no logits absolutos.

## Consecuencias

- PolicyArbitrator es extremadamente fácil de entender, testear y reproducir.
- Un solo camino de decisión para Disabled/Shadow/Enabled/Stochastic.
- La red aprende directamente qué acción elegir entre candidatos, no cuánto ajustar un score.
- Behavior Cloning es limpio: `Observation + CandidateEligibility → ExpertAction`.
- No existe confusión sobre qué escala usan los números.
