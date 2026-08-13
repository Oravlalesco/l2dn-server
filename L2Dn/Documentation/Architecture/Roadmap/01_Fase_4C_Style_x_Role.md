# Fase 4C — Style × Role

**Estado:** Diseño aceptado, código pendiente de implementación.
**Fecha:** Agosto 2026

## 1. Objetivo
Implementar el eje de **rol** compuesto con el eje de **estilo** ya certificado en la Fase 4A/4B. Esta fase añade una dimensión estructural a los NPCs (qué son en el mundo: Mob, Elite, Minion, Commander, Raid) que, combinada con su estilo de combate, permite una diferenciación más rica del comportamiento táctico.

## 2. Prerequisitos
- Fase 4A completada y certificada.
- Fase 4B completada y certificada (Estilos de Estrategia: Balanced, AggressivePressure, RangedControl, Survival).
- Fase 3 completada (Pipeline Perception → ReflexBrain → TacticalBrain → NpcIntent → IntentGateway → GameServer).

## 3. Diagrama de Arquitectura

```mermaid
graph TD
    A[NpcStrategyOptions] -->|Parsing templateId:style:role| B(NpcStrategyProfileResolver)
    B -->|Base Profile| C[Style Profile]
    B -->|Role Delta| D[Role Delta Profile]
    C --> E{StrategyBrain Composition}
    D --> E
    E -->|effectiveProfile = styleProfile + roleDelta| F[TacticalBrain]
    F -->|Intent| G[Intent Gateway]
    G --> H[GameServer]
    
    subgraph Conceptual Separation
    I[NpcStrategyRole] -.->|Identidad Estática| J[Qué soy estructuralmente]
    K[NpcCombatAssignment Phase 4E] -.->|Misión Dinámica| L[Qué trabajo hago AHORA]
    end
```

## 4. Piezas a crear

| Nombre | Ensamblado/Proyecto | Archivo propuesto | Propósito |
|---|---|---|---|
| `NpcStrategyRole` | `L2Dn.Npc.Contracts` | `NpcStrategyRole.cs` | Enum para los roles: `Mob`, `Elite`, `Minion`, `Commander`, `Raid` |
| `NpcStrategyProfileResolver` | `L2Dn.Npc.Brain` | `NpcStrategyProfileResolver.cs` | Contiene los deltas inmutables por rol y perfil base por estilo |
| `NpcStrategyOptions` | `L2Dn.Npc.Brain` | `NpcStrategyOptions.cs` | Lógica de parsing para el registro `templateId:style:role` |
| `StrategyBrain` | `L2Dn.Npc.Brain` | `StrategyBrain.cs` | Composición en tiempo de ejecución: `effectiveProfile = styleProfile + roleDelta` |
| `StrategyValidationLab` | `L2Dn.Npc.Brain.Tests` | `StrategyValidationLab.xml` | Laboratorio con lanes de pruebas |

## 5. Especificación Detallada

### Aclaración Conceptual Importante
- `NpcStrategyRole` representa **qué soy estructuralmente** (identidad estática).
- `NpcCombatAssignment` (futura fase 4E) representa **qué trabajo estoy realizando AHORA** (misión dinámica).
No deben mezclarse. Por ejemplo, un Minion con estilo `AggressivePressure` puede tener un CombatAssignment de `ProtectSupport`.

### Deltas de Rol Propuestos
| Rol | BasicAttack | Approach | OffensiveSkill | Heal | Flee | Heal HP% |
|---|---:|---:|---:|---:|---:|---:|
| Mob | 0 | 0 | 0 | 0 | 0 | 0 |
| Elite | +10 | +5 | +15 | 0 | -20 | -10 pp |
| Minion | 0 | +15 | -5 | 0 | -30 | 0 |
| Commander | 0 | +10 | +10 | 0 | -25 | 0 |
| Raid | 0 | +5 | +20 | +10 | -40 | 0 |

### Tabla de Composición de Ejemplo
*Ejemplo para Elite + AggressivePressure:*
- **AggressivePressure base:** BasicAttack: 75, Approach: 90, OffensiveSkill: 105, Heal: 85, Flee: 70, Heal HP%: 25
- **Elite delta:** BasicAttack: +10, Approach: +5, OffensiveSkill: +15, Heal: 0, Flee: -20, Heal HP%: -10
- **Effective Profile:** BasicAttack: 85, Approach: 95, OffensiveSkill: 120, Heal: 85, Flee: 50, Heal HP%: 15

## 6. Ownership

| Componente | Equipo/Rol Responsable |
|---|---|
| `NpcStrategyRole` | Core Architecture Team |
| `NpcStrategyProfileResolver` | AI Engine Team |
| Parsing en `NpcStrategyOptions` | AI Engine Team |
| Testing (Validation Lab) | QA / AI Engine Team |

## 7. Lo que NO incluye
- Modificaciones a `NpcBrainEligibility` (permanece inalterado).
- No expande el Intent de `RaidBoss` (sigue manejándose vía Legacy para raid bosses completos que no sean Monster exacto).
- Implementación de `NpcCombatAssignment`.

## 8. Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 4C-A1 | `Balanced × Mob` produce exactamente los mismos scores que Phase 4 `Balanced` sin rol | Comportamiento | Que el rol `Mob` es la identidad (delta cero) y no altera el baseline certificado |
| 4C-A2 | Cada combinación `style × role` produce scores deterministas e iguales en 1,000 evaluaciones idénticas | Comportamiento | Que la composición es pura y no introduce estado ni aleatoriedad |
| 4C-A3 | Un perfil con intelligence profile legacy explícito NO hereda un rol no-Balanced a menos que el registro lo declare | Comportamiento | Que scripts y tests existentes no cambian de comportamiento implícitamente |
| 4C-A4 | Una entrada de registro malformada o con rol desconocido emite warning y NO habilita silenciosamente `Balanced:Mob` | Contrato | Que errores de configuración no se esconden detrás de un default seguro |
| 4C-A5 | El rol `Raid` puede componerse en tests pero NO ejecuta a través del Gateway para actores `RaidBoss` | Integración | Que la frontera de eligibilidad no se expande accidentalmente |
| 4C-A6 | `NpcBrainEligibility` rechaza todo actor que no sea exacto `Monster` + exacto `AttackableAI`, independientemente del rol configurado | Integración | Que un Guard con rol Commander sigue en Legacy |
| 4C-A7 | R1-R5 con Strategy Enabled y roles configurados mantiene zero drops, zero overflow, max concurrent 1 | Rendimiento | Que la composición no degrada el scheduler |
| 4C-A8 | Release build sin errores nuevos | Arquitectura | Que no se introdujeron dependencias prohibidas |

> Especificación completa de tests: [`11_Criterios_Aceptacion_y_Testing.md`](11_Criterios_Aceptacion_y_Testing.md)

## 8b. Tests requeridos

### Contracts (`L2Dn.Npc.Contracts.Tests`)

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `RoleDelta_Mob_IsIdentityZero` | El delta Mob no modifica ningún score | `RoleDelta.Mob` → todos los campos == 0 |
| `RoleDelta_AllRoles_AreImmutable` | Los deltas no pueden mutar después de construcción | Crear delta, intentar modificar → compilación falla o excepción |
| `RoleEnum_HasExactlyFiveValues` | Cardinalidad acotada para telemetría | `Enum.GetValues<NpcStrategyRole>()` → exactamente {Mob, Elite, Minion, Commander, Raid} |

### Brain (`L2Dn.Npc.Brain.Tests`)

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `Compose_Balanced_Mob_EqualsPhase4Balanced` | Identity: rol Mob no altera baseline | Perception + Balanced + Mob → decisión idéntica a Phase 4 Balanced sin rol |
| `Compose_AggressivePressure_Elite_ProducesExpectedScores` | Suma aritmética correcta | AP base + Elite delta → BasicAttack=85, Approach=95, OffensiveSkill=120 |
| `Compose_Survival_Commander_FleeScoreNeverNegative` | Scores negativos no rompen Tactical | Survival(Flee=115) + Commander(Flee=−25) + edge → Flee score ≥ 0 |
| `Compose_AllStyles_AllRoles_Deterministic_1000x` | Determinismo | Cada combinación × 1000 → output bit-a-bit idéntico |
| `Compose_ExplicitLegacyProfile_DefaultsToBalancedMob` | No herencia implícita | Legacy profile sin registro → Balanced scores |
| `Registry_MalformedEntry_WarnsAndRejects` | Config inválida no se esconde | `"20130:invalid:mob"` → warning, template NO en registro |
| `Registry_UnknownRole_WarnsAndRejects` | Rol desconocido no habilita Balanced | `"20130:aggressive_pressure:tank"` → warning, template NO aparece |
| `Registry_DuplicateTemplate_LastWinsWithWarning` | Duplicados explícitos | Dos entradas para 20130 → último gana, warning emitido |

### GameServer.Model (`L2Dn.GameServer.Model.Tests`)

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `Eligibility_Guard_WithCommanderRole_StaysLegacy` | Actor boundary no se expande | Guard + registro commander → `UsesIntentBrain == false` |
| `Eligibility_RaidBoss_WithRaidRole_StaysLegacy` | RaidBoss no entra en Intent | RaidBoss + rol Raid → `UsesIntentBrain == false` |
| `Eligibility_BaseMonster_WithEliteRole_UsesIntent` | Monster base sí puede tener rol | Monster + rol Elite → `UsesIntentBrain == true` |
| `RoleTelemetry_EmitsExactlyFiveCardinalityValues` | Cardinalidad acotada | Evaluaciones con todos los roles → tags solo contienen 5 valores |

## 9. Telemetría
- **Métrica:** `npc.brain.strategy.evaluation_time` (ya existente, verificar que el impacto sea mínimo)
- **Atributo/Tag propuesto:** `role` (con cardinalidad acotada a 5: `Mob`, `Elite`, `Minion`, `Commander`, `Raid`) y `style`.

## 10. Configuración
- `NPC_STRATEGY_ROLE_ENABLED` (booleano, default: `true` en el futuro)
- `NPC_STRATEGY_ROLE_OVERRIDES` (diccionario JSON opcional para sobreescribir deltas en caliente para experimentación).

## 11. Rollback
- Si se detecta inestabilidad, revertir el registro `templateId:style:role` a `templateId:style` en la configuración (la opción por defecto asume `Mob` con delta 0).
- Deshabilitar variable de entorno si se implementó un switch global.

## 12. Relación con fases adyacentes
- **Consume de:** Fase 4A/4B (Perfiles base inmutables de estilo).
- **Provee a:** Fase 4E (La identidad estática servirá de input, junto a otras cosas, para que el Combat Assignment decida las misiones dinámicas).

## 13. Experimentos / laboratorio
- Configurar en `StrategyValidationLab.xml` un "lane" de combate con:
  - Un Elite AggressivePressure vs Player.
  - Un Minion + Commander Survival vs Player.
  - Un Raid (solo validación de contrato) Balanced vs Player.
- Medir la divergencia de comportamiento esperada usando mocks del Intent Gateway.
