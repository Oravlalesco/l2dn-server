# ADR-014: Fallback obligatorio a comportamiento determinista

Estado: aceptada

## Contexto

La introducción de redes neuronales en el pipeline de decisión crea un nuevo modo de fallo: la inferencia puede tardar demasiado, el modelo puede no estar disponible, el advice puede haber expirado, o el modelo puede producir output con schema incompatible. El NPC no debe congelarse bajo ninguna circunstancia.

## Decisión

Toda ejecución de Neural Policy tiene un **fallback obligatorio** a comportamiento determinista. La jerarquía de decisión es:

```text
1. Reflex / Safety         ← obligatorio, inmediato, sin dependencia neural
2. Reglas obligatorias
3. Neural Advice válido    ← si existe y no expiró
4. Strategy determinista   ← fallback automático
5. Tactical
6. Intent
7. Gateway
```

El fallback se activa cuando:

- No existe advice vigente (`GeneratedAt` + TTL < tick actual)
- La inferencia no completó a tiempo
- El modelo no está cargado o falló al cargar
- El output del modelo tiene schema incompatible
- El checksum del modelo no coincide
- `NPC_POLICY_MODE=Disabled`
- Error de runtime en la inferencia

## Consecuencias

- El gameplay NUNCA depende obligatoriamente de la red neuronal.
- Un fallo de inferencia es operacionalmente equivalente a `Strategy Disabled`: el NPC usa el comportamiento determinista certificado.
- El Reflex Brain responde inmediatamente a eventos críticos sin esperar inferencia.
- La métrica `l2dn.npc.policy.fallback.total` cuantifica la frecuencia de fallback.
- El rollback completo se logra con `NPC_POLICY_MODE=Disabled`.
- Extiende ADR-006 (fallback local del NPC Brain) al contexto de Neural Policy.
