# ADR-012: Neural Policy produce recomendaciones, no intents directos

Estado: aceptada

## Contexto

El programa de modernización introduce redes neuronales como capa de decisión para NPCs. Debe definirse el límite de autoridad entre la red neuronal y el pipeline existente de intents.

## Decisión

La Neural Policy produce **preferencias sobre acciones** (`NpcPolicyAdvice`), nunca `NpcIntent` directos. Las preferencias son biases numéricos que modifican los scores del `StrategyBrain` / `TacticalBrain`. El `TacticalBrain` sigue seleccionando la acción concreta. El `IntentGateway` sigue validando el mundo real.

```text
NpcPerception
    ↓
Neural Policy
    ↓
Action Preferences (biases)    ← hasta aquí llega la red
    ↓
Strategy / Tactical Brain       ← selección concreta
    ↓
NpcIntent
    ↓
IntentGateway                   ← validación autoritativa
    ↓
GameServer                      ← ejecución
```

## Consecuencias

- La red no puede bypasear cooldowns, mana, rango, geodata ni lifecycle.
- Un modelo defectuoso produce biases subóptimos, no acciones ilegales.
- El fallback a Strategy determinista solo requiere ignorar el advice.
- Shadow comparison puede comparar biases sin riesgo de ejecución.
- El modelo no necesita conocer la semántica completa de intents ni paquetes de red.
