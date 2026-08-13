# ADR-013: Action masking antes de la selección de policy

Estado: aceptada

## Contexto

Cuando una Neural Policy produce preferencias sobre acciones, puede asignar probabilidad a acciones que son físicamente imposibles en ese instante (skill en cooldown, heal con HP lleno, flee deshabilitado). Permitir que la red compita sobre acciones inválidas desperdicia capacidad del modelo y puede producir comportamientos confusos si el fallback no es correcto.

## Decisión

Antes de que la policy (neural o determinista) evalúe sus preferencias, se aplica un **action mask** que elimina del espacio de decisión las acciones actualmente inválidas. El mask se construye desde la percepción inmutable y refleja:

- Skills en cooldown
- Mana insuficiente
- HP alto (para heal)
- Flee no permitido por intelligence profile
- Actor en estado casting/stunned/disabled
- Target fuera de rango (para skills específicos)

```text
Policy
    ↓
Action Mask          ← primera barrera
    ↓
Tactical Brain       ← selección concreta
    ↓
NpcIntent
    ↓
IntentGateway        ← segunda barrera (estado vivo del mundo)
    ↓
GameServer
```

## Consecuencias

- La red solo compite sobre acciones realmente posibles.
- Dos barreras independientes: mask (pre-decisión, snapshot) + Gateway (post-decisión, live state).
- El mask no reemplaza al Gateway; ambos son necesarios porque el mundo puede cambiar entre la decisión y la ejecución.
- El mask es determinista y reproducible desde la percepción.
- La métrica `l2dn.npc.policy.invalid_action_masked` cuantifica cuántas acciones se filtran.
