# ADR-014: Fallback determinista es INVARIANTE, no configurable

Estado: aceptada (revisión 2)

## Contexto

La introducción de redes neuronales en el pipeline de decisión crea nuevos modos de fallo: la inferencia puede tardar demasiado, el modelo puede no estar disponible, el advice puede haber expirado, o el modelo puede producir output con schema incompatible. El NPC no debe congelarse bajo ninguna circunstancia.

**Revisión 2**: La revisión 1 proponía `NPC_POLICY_FALLBACK_ON_ERROR=true/false`. No debería existir la opción de desactivar el fallback en producción. Si desactivamos el fallback y la inferencia falla, el NPC se congela. Eso es inaceptable.

## Decisión

Fallback a comportamiento determinista **no es configurable**. Es un invariante del sistema.

```text
Policy error / timeout / stale / unavailable
    ↓
SIEMPRE: StrategyBrain determinista
    ↓
NUNCA: NPC congelado
```

### Se elimina

- `NPC_POLICY_FALLBACK_ON_ERROR` — ya no existe. El fallback es incondicional.

### Se mantiene

- `NPC_POLICY_MODE` (Disabled / Shadow / Enabled) — controla si la policy se usa, no si el fallback existe.
- Logging y telemetría de fallback — siempre activos.

### El fallback se activa cuando

- No existe advice vigente (causal metadata no coincide o ExpiresAtWorldTick < tick actual)
- La inferencia no completó a tiempo
- El modelo no está cargado o falló al cargar
- El output del modelo tiene schema incompatible o checksum incorrecto
- Error de runtime en la inferencia
- NpcKey o Generation del advice no coinciden con el NPC actual
- BasedOnStateRevision del advice es anterior al estado actual

## Consecuencias

- El gameplay NUNCA depende obligatoriamente de la red neuronal.
- Un fallo de inferencia es operacionalmente equivalente a `Policy Disabled`: el NPC usa el comportamiento determinista certificado.
- El Reflex Brain responde inmediatamente a eventos críticos sin esperar inferencia.
- La métrica `l2dn.npc.policy.fallback.total` cuantifica la frecuencia de fallback.
- El rollback completo se logra con `NPC_POLICY_MODE=Disabled`.
- Extiende ADR-006 (fallback local del NPC Brain) al contexto de Neural Policy.
- Aplica también a Squad: si SquadBrain falla, cada NPC actúa individualmente (Phase 4C).
- Aplica también a Encounter: si EncounterBrain falla, `DeterministicEncounterPolicy` (del nuevo sistema), no hot-switch a Legacy mid-fight.
