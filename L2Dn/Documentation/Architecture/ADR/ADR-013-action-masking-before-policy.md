# ADR-013: Action Mask — una sola fuente de verdad (TacticalActionEvaluator)

Estado: aceptada (revisión 2)

## Contexto

Cuando una Neural Policy produce preferencias sobre acciones, puede asignar probabilidad a acciones que son físicamente imposibles en ese instante (skill en cooldown, heal con HP lleno, flee deshabilitado).

**Revisión 2**: El código existente en `TacticalActionEvaluator` ya determina cuáles acciones son elegibles, cuáles están fuera de rango, qué skill está disponible, cuándo Heal aplica, y cuándo una acción está suprimida. Conserva incluso el motivo de inelegibilidad en `NpcStrategyCandidateEligibility`. Crear un segundo `NpcPolicyActionMask` duplicaría esta lógica y produciría dos fuentes de verdad.

## Decisión

**No crear un ActionMasker separado.** En su lugar, refactorizar `TacticalActionEvaluator` para separar dos responsabilidades:

```text
BuildCandidates() → NpcTacticalCandidateSet
SelectCandidate() → candidato ganador
```

El `NpcTacticalCandidateSet` contiene para cada candidato:
- Action type
- Deterministic score
- Eligible (bool)
- IneligibilityReason (si no elegible)

**Este CandidateSet ES el Action Mask.** La red neuronal solo puede evaluar candidatos marcados como `eligible = true`.

```text
Strategy × Role
    ↓
Reflex
    ↓ (si no Reflex Intent)
TacticalActionEvaluator.BuildCandidates()
    ↓
NpcTacticalCandidateSet     ← ÚNICA fuente de verdad
    ↓                          de qué acciones son posibles
PolicyArbitrator
    ↓
Deterministic / Neural / Squad → modifica scores de candidatos elegibles
    ↓
ActionSelection (argmax o sampling)
    ↓
TacticalIntentBuilder
    ↓
NpcIntent → Gateway → GameServer
```

Dos barreras independientes:
1. **CandidateSet** (pre-decisión, snapshot) — acciones imposibles eliminadas antes de policy
2. **IntentGateway** (post-decisión, live state) — valida contra estado actual del mundo

## Consecuencias

- Una sola implementación de eligibilidad de acciones (la existente en TacticalActionEvaluator).
- La red solo compite sobre acciones realmente posibles.
- Dos barreras independientes: CandidateSet (pre-decisión) + Gateway (post-decisión).
- El CandidateSet es determinista y reproducible desde la percepción.
- `NpcPolicyActionMask` como tipo separado en Contracts se elimina.
- La métrica `l2dn.npc.tactical.candidates.masked` cuantifica cuántas acciones se filtran.
