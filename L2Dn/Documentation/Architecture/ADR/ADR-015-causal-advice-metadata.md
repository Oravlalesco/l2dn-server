# ADR-015: Causalidad obligatoria en Advice y Directive

Estado: aceptada (revisión 2)

## Contexto

`NpcPolicyAdvice` y `SquadDirective` se generan de forma asíncrona. Entre el momento en que se solicita la evaluación y el momento en que el advice/directive llega, el estado del NPC/squad puede haber cambiado radicalmente.

**Revisión 2**: Corrige dos problemas de la revisión 1:
1. `NpcKey` ya contiene `Generation` (es un `record struct(ObjectId, Generation)`). Listar `Generation` por separado en el Advice duplica información y crea dos fuentes de verdad.
2. Para V1, `BasedOnStateRevision` debe ser exactamente igual a `CurrentStateRevision`, no "menor que un delta configurable". Un mismatch de revisión semántica significa que algo relevante cambió.

## Decisión

### NpcPolicyAdvice — campos causales obligatorios

```text
NpcKey                       ← identidad + generación (ya contiene Generation)
BasedOnStateRevision         ← revisión del estado del Brain al extraer la observación
PolicyEvaluationId           ← ID único para correlación con Shadow comparison
GeneratedAtWorldTick         ← tick del mundo cuando se generó
ExpiresAtWorldTick           ← tick máximo de validez
ModelVersion                 ← versión del modelo que produjo el advice
FeatureSchemaVersion         ← versión del schema de features
ActionSchemaVersion          ← versión del schema de acciones
```

### SquadDirective — campos causales obligatorios

```text
SquadKey                     ← identificador del escuadrón
SquadGeneration              ← generación del escuadrón (no del NPC)
MembershipRevision           ← cambia cuando entran/salen miembros
DirectiveSequence            ← número monótono creciente
BasedOnSquadStateRevision    ← revisión del estado del squad al calcular directiva
IssuedAtWorldTick
ExpiresAtWorldTick
```

### Regla de validación V1 (estricta)

El consumidor (PolicyArbitrator) **rechaza** un advice si **cualquiera** no se cumple:

```text
Advice.NpcKey == NPC actual
Advice.BasedOnStateRevision == CurrentStateRevision    ← EXACTO, no delta
Advice.ExpiresAtWorldTick >= WorldTick actual
```

Si se rechaza → fallback determinista. Sin excepción.

### Futuro: AdviceCompatibilityPolicy

Si la evidencia muestra que la revisión estricta descarta demasiados advices útiles, se puede introducir:

```text
AdviceCompatibilityPolicy { MaxRevisionDelta = 2 }
```

Pero eso se decide con datos, no a priori.

### Seed determinista

```text
NpcDecisionSeed = Hash(
    ServerRunSeed,          ← reproducibilidad entre ejecuciones
    NpcKey,                 ← ya contiene ObjectId + Generation
    DecisionSequence,       ← contador monótono por NPC
    PolicyVersion
)
```

`ServerRunSeed` se guarda en el replay para reproducción exacta.

## Consecuencias

- Un NPC respawneado nunca ejecuta advice de su encarnación anterior (NpcKey incluye Generation).
- Un advice sobre un estado obsoleto es descartado (revisión exacta).
- Shadow comparison correlaciona por PolicyEvaluationId.
- Sin campo `Generation` separado que duplique lo que NpcKey ya contiene.
- Overhead mínimo: son campos escalares en una estructura inmutable.
