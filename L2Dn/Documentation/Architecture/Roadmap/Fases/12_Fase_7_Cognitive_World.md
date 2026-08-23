# Fase 7+: Cognitive / World Intelligence

**Estado:** Visión especulativa a largo plazo
**Fecha:** 12 de Agosto de 2026
**Revisión:** 2 (2026-08-13)

## 1. Objetivo

Representar la frontera final en la inteligencia del servidor de Lineage 2. Integrar entidades a escala macro (regiones, mundo), memoria a largo plazo para NPCs, y uso de grandes modelos de lenguaje (LLMs) fuera del ciclo de combate (ADR-007) para generación narrativa, personalidades y ecología del mundo.

## 2. Prerequisitos

- Todas las fases anteriores (1 a 6) estabilizadas en producción.
- Infraestructura de datos para memoria a largo plazo (almacenamiento asíncrono, bases de datos vectoriales si aplica).
- ADR-007 verificado (LLMs deben estar fuera de loops hot-path).

## 3. Diagrama de arquitectura

```mermaid
flowchart TD
    WorldDir[World Director]
    
    subgraph Faction & Region
        RegionDir[Region Director - Giran]
        FactionDir[Faction Director - Orcs]
        RegionDir --> FactionDir
    end
    
    subgraph Encounters & Squads
        EncounterDir[Encounter]
        SquadDir[Squad]
    end
    
    subgraph Cognitive NPC
        LongTermMem[(Long-Term Memory)]
        Personality[Personality Traits]
        LLM[LLM Service - Dialogue/Quests]
        Brain[Local Brain - Combat]
    end
    
    WorldDir --> RegionDir
    FactionDir --> EncounterDir
    EncounterDir --> SquadDir
    SquadDir --> Brain
    
    Brain <..> Personality
    LLM <--> LongTermMem
    LLM --> Personality
```

## 4. Piezas a crear

| Nombre | Ensamblado | Archivo propuesto | Propósito |
|---|---|---|---|
| `WorldDirector` | `L2Dn.AI.Directors` | `World/WorldDirector.cs` | Orquesta eventos globales y patrones climáticos/económicos. |
| `RegionDirector` | `L2Dn.AI.Directors` | `Region/RegionDirector.cs` | Controla migraciones y niveles de amenaza por territorio. |
| `WorldDirective` | `L2Dn.AI.Directors` | `Contracts/WorldDirective.cs` | Directiva inmutable del director con límites presupuestarios. |
| `DirectorPolicyGate` | `L2Dn.GameServer.Model` | `AI/Directors/DirectorPolicyGate.cs` | Valida directivas del director contra límites de spawn/economy/reward. |
| `CognitiveMemory` | `L2Dn.AI.Cognitive` | `Memory/CognitiveMemory.cs` | Subsistema de memoria persistente para NPCs clave. |
| `LLMDialogueGateway` | `L2Dn.AI.Cognitive` | `LLM/LLMDialogueGateway.cs` | Interfaz con API externas (OpenAI/Anthropic) para rol no combate. |

## 5. Especificación detallada

- **World & Region Directors:** Complementan (no reemplazan) la generación estática de spawns. Si una zona de granja es muy atacada, el `RegionDirector` podría pedir refuerzos de una región aledaña, migrando mobs dinámicamente. Toda directiva pasa por `DirectorPolicyGate` que impone límites:
  - Max spawn delta por región y por tick
  - Max event frequency
  - Cooldown por región
  - Economy budgets
  - Reward budgets
  - Allowlisted actions
  Misma filosofía que IntentGateway, pero a escala mundial.
- **Cognitive Memory:** NPCs guardas o comerciantes recuerdan si un jugador completó un objetivo o los atacó semanas atrás.
- **Dynamic Quests:** Basado en el estado del mundo (ej. "Los orcos tomaron la granja"), un LLM genera textos de quest plausibles y el `WorldDirector` los valida y los integra al modelo de recompensas existente.

## 6. Ownership

| Componente | Responsabilidad | Equipo / Rol |
|---|---|---|
| Director System (World/Region) | Ecología y balance global del mundo | Lead Game Designer |
| LLM & Memory Integration | Manejo de APIs, prompts y bases vectoriales | AI Engineer |
| Fallback Content | Fallbacks para cuando el LLM falle o esté offline | Content Scripter |

## 7. Lo que NO incluye

- **LLM en Pathfinding o Combate:** Bajo ninguna circunstancia un LLM decidirá a quién atacar en el próximo frame. Es lento e impredecible.
- **Narrativa sin moderar:** Se implementarán filtros y validaciones lógicas para evitar alucinaciones perjudiciales.

## 8. Criterios de aceptación

| ID | Criterio | Tipo | Qué demuestra |
|---|---|---|---|
| 7-A1 | `RegionDirector` modifica densidad de spawns basándose en métricas observables (población de jugadores, hora del servidor) sin intervención manual | Comportamiento | Que el director reacciona a datos reales del mundo |
| 7-A2 | Las llamadas a LLM NUNCA bloquean el Think loop de ningún NPC: son asíncronas con timeout y fallback a diálogo estático | Arquitectura | Que ADR-007 se cumple rigurosamente |
| 7-A3 | Latencia P99 de diálogo con LLM < 3 segundos desde la perspectiva del jugador | Rendimiento | Que la experiencia de diálogo es aceptable |
| 7-A4 | Si el servicio LLM no está disponible, el NPC usa HTML estático sin error visible | Integración | Que la ausencia de LLM es transparente al jugador |
| 7-A5 | El combate no muestra degradación de TPS ni latencia cuando las APIs cognitivas están activas | Rendimiento | Que la capa cognitiva es ortogonal al combat loop |

> Especificación completa de tests: [`04_Criterios_Aceptacion_y_Testing.md`](../04_Criterios_Aceptacion_y_Testing.md)

## 8b. Tests requeridos

| Test | Propiedad | Input → Output esperado |
|---|---|---|
| `LLM_Call_NeverBlocksThink` | Sin bloqueo | Llamada LLM pendiente → Think de NPC cercano continúa sin esperar |
| `LLM_Unavailable_FallbackToStaticHTML` | Fallback graceful | Servicio LLM offline → NPC muestra diálogo HTML estático → sin error |
| `LLM_Timeout_ReturnsDefault` | Timeout controlado | LLM responde en > 5s → timeout → respuesta default |
| `RegionDirector_AdjustsSpawnDensity` | Reactividad | 50 jugadores en zona → densidad aumenta; 0 jugadores → densidad disminuye |
| `CognitiveAPIs_NoTPSDegradation` | Ortogonalidad | APIs activas vs inactivas → TPS delta < 1% |

## 9. Telemetría

- `l2dn.cognitive.llm_calls` (Counter): Llamadas a servicios de lenguaje.
- `l2dn.cognitive.llm_latency_ms` (Histogram): Latencia de la inferencia narrativa.
- `l2dn.macro.region_events` (Counter): Eventos regionales disparados por el director.

## 10. Configuración

- `L2DN_MACRO_AI_ENABLED` (bool)
- `L2DN_COGNITIVE_LLM_API_KEY` (string)
- `L2DN_COGNITIVE_LLM_MODEL` (string)

## 11. Rollback

1. Deshabilitar `L2DN_MACRO_AI_ENABLED` detiene la generación dinámica de eventos y restaura el estado determinista clásico del servidor.
2. Deshabilitar los LLMs forzará a los NPCs a usar el archivo `HTML` tradicional de diálogos.

## 12. Relación con fases adyacentes

- **Consume:** Todo el ecosistema distribuido (Fase 5) para manejar la carga asíncrona de los directores.
- **Provee a:** La culminación del objetivo del servidor de ser una experiencia inmersiva y reactiva.

## 13. Experimentos / laboratorio

- Integrar un LLM simple a un Guard de Talking Island para responder preguntas sobre la ubicación de tiendas (traduciendo coordenadas del geodata a lenguaje natural).
- Simular un asalto de Mobs a un castillo donde el `FactionDirector` envía oleadas generadas proceduralmente en vez de hardcodeadas.
