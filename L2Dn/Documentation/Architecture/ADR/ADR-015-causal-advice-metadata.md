# ADR-015: Causalidad obligatoria en Advice y Directive

Estado: aceptada

## Contexto

`NpcPolicyAdvice` y `SquadDirective` se generan de forma asíncrona. Entre el momento en que se solicita la evaluación y el momento en que el advice/directive llega, el estado del NPC/squad puede haber cambiado: el NPC puede haber muerto y respawneado (nueva generación), cambiado de target, perdido HP significativo, o la composición del squad puede haber cambiado.

Un TTL por sí solo no es suficiente: un advice puede estar dentro de su TTL y aun así referirse a un estado obsoleto.

## Decisión

Todo `NpcPolicyAdvice` debe contener metadata causal que permita verificar que fue calculado para la encarnación y estado correctos:

```text
NpcKey                   ← identidad del NPC
Generation               ← encarnación (evita usar advice de un NPC que murió y respawneó)
BasedOnStateRevision     ← revisión del estado del Brain cuando se extrajo la observación
PolicyEvaluationId       ← ID único para correlación con Shadow comparison
GeneratedAtWorldTick     ← tick del mundo cuando se generó
ExpiresAtWorldTick       ← tick máximo de validez
```

Todo `SquadDirective` debe contener:

```text
SquadKey
SquadGeneration
MembershipRevision       ← evita ejecutar directiva de composición anterior
DirectiveSequence        ← ordenamiento monótono
BasedOnSquadRevision
IssuedAtWorldTick
ExpiresAtWorldTick
```

El consumidor (Brain/StrategyBrain) **rechaza** un advice/directive si:
- `NpcKey` o `Generation` no coinciden con el NPC actual
- `BasedOnStateRevision` es anterior a la revisión actual menos un delta configurable
- `ExpiresAtWorldTick < worldTick actual`

## Consecuencias

- Un NPC que murió y respawneó nunca ejecuta advice de su encarnación anterior.
- Un advice calculado sobre un estado muy diferente al actual es descartado.
- Shadow comparison puede correlacionar evaluaciones por `PolicyEvaluationId` en vez de comparar contra el Think actual.
- Overhead mínimo: son campos escalares en una estructura inmutable.
