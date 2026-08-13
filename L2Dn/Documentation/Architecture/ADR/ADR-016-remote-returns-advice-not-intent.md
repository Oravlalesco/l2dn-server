# ADR-016: Remote Brain devuelve Advice, nunca Intent

Estado: aceptada

## Contexto

Fase 5 introduce la posibilidad de ejecutar inferencia neural en workers remotos. Debe definirse qué tipo de dato viaja por la red como respuesta.

## Decisión

Un worker remoto devuelve exclusivamente:

```text
NpcPolicyAdvice
SquadDirective
EncounterAdvice
```

**Nunca** devuelve `NpcIntent`.

El Brain local sigue siendo el único productor de `NpcIntent`, y el `IntentGateway` sigue siendo la única autoridad de validación.

```text
Worker remoto
    ↓
NpcPolicyAdvice (por red)
    ↓
NpcPolicyAdviceStore (local)
    ↓
Brain local
    ↓
NpcIntent (local)
    ↓
IntentGateway (local)
    ↓
GameServer
```

## Consecuencias

- La red de comunicación nunca forma parte del execution boundary.
- Un fallo de red produce fallback a determinístico, no un intent inválido.
- No se transmite `NpcPerceptionSnapshot` (demasiado grande y acopla transporte a percepción). Se transmite `NpcPolicyObservationV1` ya vectorizada/normalizada.
- El worker no necesita conocer la semántica de intents ni paquetes de red del GameServer.
