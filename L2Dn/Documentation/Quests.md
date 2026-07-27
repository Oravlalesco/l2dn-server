# Quests modernas (NewQuest) — guía técnica experimental

Cómo funcionan las misiones modernas importadas desde ClassicAden/Essence en L2Dn,
añadir una quest nueva y cómo sobreescribir el comportamiento para casos especiales
(cinemáticas, instancias, etc.).

> **Importante:** la rama canónica de este proyecto es
> **Classic 447/Shinemaker**. Las 288 entradas de `NewQuestData.xml` y la cadena
> `10071`–`10079` proceden de ClassicAden; la cadena enana es un piloto técnico
> ejecutado sobre Classic, no contenido Classic nativo. No debe copiarse a nuevas
> cadenas sin auditar primero todas sus dependencias.
>
> Diagnóstico, evidencia y reglas de compatibilidad:
> [Classic447Compatibility.md](Classic447Compatibility.md).

## Estado actual

| Pieza | Estado |
|-------|--------|
| Motor (`Quest`, `QuestState`, `QuestManager`, paquetes `ExQuest*`) | Portado |
| Datos `DataPack/NewQuestData.xml` (288 entradas ClassicAden) | Cargados; no implican scripts ni compatibilidad Classic |
| Tutorial enano `Q00206Tutorial` (ID 206) | Operativo |
| Cadena de novato enano `10071`–`10079` | Operativa en servidor; piloto ClassicAden sobre cliente Classic |
| Otras cadenas raciales / Deton / cambio de clase | Pendiente |

El cambio de clase **no** va por quest: lo gestiona `PlayerClassChange`
(`ExClassChangeSetAlarmPacket` al nivel 20/40/76). Por eso la cadena piloto
termina en `10079` y **no** registra `10080+`.

## Arquitectura

```
NewQuestData.xml  →  NewQuestData loader  →  metadata en memoria
                                              ↓
Scripts.RegisterQuests()  →  StoryQuest / subclases  →  QuestManager
                                              ↓
Cliente (UI)  ←→  RequestExQuestAccept / Teleport / Complete
```

- El **XML** define NPCs, condiciones, goals y recompensas.
- El **script C#** aporta la lógica (y puede sobreescribir cualquier paso).
- Sin entrada en `QuestManager`, el cliente puede *mostrar* la quest pero
  `RequestExQuestAccept` la descarta en silencio.

## Flujo de paquetes

1. **Login** (`EnterWorldPacket`): recorre `NewQuestData`; si
   `QuestManager.getQuest(id)` existe, `canStartQuest` y no es
   `specificStart`, envía `ExQuestDialogPacket(ACCEPT)` de la primera.
   **`specificStart` no es una condición de `canStartQuest`**: solo evita el auto-offer
   en login. Si una quest lo usa (p. ej. 10071), el script debe añadir `addCondStart`
   (tutorial memo ≥ 5); si no, un cliente modificado puede enviar ACCEPT directo.
2. **Aceptar** → `RequestExQuestAcceptPacket` → `quest.notifyEvent("ACCEPT")` →
   `StoryQuest.OnAccept` → `canStartQuest` → hint de objetivo (`ExShowScreenMessage`).
3. **Teleport UI** → `RequestExQuestTeleportPacket` → `"TELEPORT"`.
4. **Goal cumplido** → cond `DONE` + diálogo END + hint (sin auto-completar).
5. **Completar** → `RequestExQuestCompletePacket` (o recovery Laferon) → `"COMPLETE"` →
   `rewardPlayer` + diálogo ACCEPT de la siguiente (`NextQuestId`) + hint.

## Cliente Classic frente a metadata ClassicAden

Los paquetes `ExQuest*` **no envían texto**. Title, Description e imágenes del journal
salen del System del cliente (`QuestName*.dat` / `NewQuestData*.dat`). Las entradas
de `10071`–`10079` existen en `NewQuestData_ClassicAden-eu.dat`, pero no en la tabla
Classic activa. Por eso la pestaña Quest se ve vacía aunque el estado sea correcto.

**Mitigación L2Dn (servidor):**

| Canal | Uso |
|-------|-----|
| `ExShowScreenMessage` | Objetivos, progreso `n/N`, “completa la quest”, siguiente Accept |
| `ExQuestDialog` TELEPORT | Intactos; **no** abrir HTML en el mismo tick |
| Ítems 98xxx | Remap a IDs clásicos si el System no los conoce |

Ciclo endurecido: Accept → cazar → **DONE** → Complete (UI o Laferon) → Accept siguiente.

### Ítems exclusivos de ClassicAden usados por la cadena enana

| Evitar | Sustituto / nota |
|--------|------------------|
| `98464` Herb Roots | 10074 usa kill-count (sin ítem) |
| `98474`–`98478` joyas Sealed | `114`, `115`, `877`, `878`, `909` (recompensas 10076) |

Cleanup: `Ep30DwarvenItemCleanup` al login/talk Laferon (memo ≥ 5) y al completar 10076.

## Piezas clave (rutas)

| Qué | Archivo |
|-----|---------|
| Clase base de scripts | `L2Dn.GameServer.Scripts/Quests/StoryQuest.cs` |
| Cadena piloto enano | `L2Dn.GameServer.Scripts/Quests/DwarvenVillage/Q10071…Q10079*.cs` |
| Registro | `L2Dn.GameServer.Scripts/Scripts.cs` → `RegisterQuests()` |
| Datos XML | `L2Dn.GameServer/DataPack/NewQuestData.xml` |
| Teleports de quest | `L2Dn.GameServer/DataPack/TeleportListData.xml` |
| Modelo / helpers | `L2Dn.GameServer.Model/Model/Quests/Quest.cs` |
| Condiciones / goals | `…/NewQuestData/NewQuest*.cs` |

Helpers ya en `Quest`: `canStartQuest`, `getQuestData`, `rewardPlayer`,
`teleportToQuestLocation`, `giveStoryBuffReward`, `sendAcceptDialog`,
`sendEndDialog`.

## Cómo añadir una quest simple

1. Entrada en `NewQuestData.xml` (id, type, NPCs, conditions, goals, rewards).
2. Constantes nombradas para IDs (no literales sueltos). En la cadena enana viven en
   `Quests/DwarvenVillage/DwarvenNewbieIds.cs`.
3. Clase delgada que herede de `StoryQuest`:

```csharp
namespace L2Dn.GameServer.Scripts.Quests.DwarvenVillage;

public sealed class Q10075_BePrepared: StoryQuest
{
    public Q10075_BePrepared()
        : base(DwarvenNewbieIds.QuestBePrepared, DwarvenNewbieIds.FrozenValleyMonsters)
    {
    }

    protected override int NextQuestId => DwarvenNewbieIds.QuestUsefulPreparations;
}
```

4. Registrar en `Scripts.RegisterQuests()` **después** de sus pre-quests
   (el constructor lee `preQuestId` desde `QuestManager`).
5. Si usa `startLocationId` / `endLocationId`, esos IDs deben existir en
   `TeleportListData.xml` (comentar el origen NewQuestData en el XML).
6. Compilar C#: `cd Docker; .\dev-publish.ps1; .\dev-restart.ps1 -Code`.
   Solo XML/teleports: `.\dev-restart.ps1`.

## Goals soportados por `StoryQuest`

| Tipo | Cómo se detecta | Progreso |
|------|-----------------|----------|
| Hablar con NPC | Sin `addKillId` y sin `goalItemId` | `onFirstTalk` del start/end NPC → DONE |
| Matar N mobs | `addKillId` + `goalCount` | `onKill` incrementa count |
| Recoger ítem | `goalItemId` + `goalCount` | `onKill` da el ítem y cuenta |

## Quests especiales (cinemáticas, Antharas, instancias)

No hace falta otra arquitectura. Hereda de `StoryQuest` (o de `Quest`) y
sobreescribe solo lo necesario:

```csharp
public override string? onAdvEvent(string @event, Npc? npc, Player? player)
{
    if (@event == "ACCEPT" && player != null)
    {
        // lógica custom, cinemática, etc.
        // playMovie / spawn / instance…
    }
    return base.onAdvEvent(@event, npc, player);
}
```

Todo es `virtual`: `OnAccept`, `OnTeleport`, `OnComplete`, `onFirstTalk`,
`onKill`, `OfferNextQuest`, `CompletesOnTalk`, `NextQuestId`.

## Piloto ClassicAden enano sobre Classic (strip mine → Frozen Valley)

Orden alineado con el catálogo ClassicAden / `NewQuestData`. Este flujo documenta
el piloto existente; no constituye la base canónica para quests Classic nuevas:

| Fase | Quests | Dónde |
|------|--------|-------|
| Tutorial 206 | Helper → gema → Laferon `reward_2` | Strip mine (**sin** teleport a aldea) |
| Story strip mine | **10071** → **10072** → **10073** (nivel 5) | Strip mine, gremlins junto a Laferon |
| Salida | **10074** TELEPORT (`startLocationId` 144) → matar 3 Longtail Keltir | Frozen Valley (sin ítem 98464) |
| Más tarde | 10075–10079 | Frozen Valley / Western Mining; 10076 da joyas clásicas |

`Q00206Tutorial` es dueño del first-talk de Laferon (`30528`) hasta `reward_2` (memoState 5).

| Paso tutorial | Comportamiento |
|---------------|----------------|
| memoState 4 | HTML `30528-2.html` → `reward_2` (soulshots + ACCEPT **10071** en el mine) |
| memoState 5/6 | HTML `30528-4.html` (seguir por UI NewQuest en el mine) |

Salida del mine = TELEPORT de **10074**, no el tutorial. Newbie Guide `30601` **fuera de alcance**.

Configuración de **10071**:

1. `specificStart=true` → `EnterWorld` no ofrece ACCEPT al login.
2. `AutoRegisterNpcFirstTalk => false`; talk-goal se completa en `OnAccept` y se hace
   `OnComplete` al instante (sin diálogo END, evita carrera HTML/ExQuest).
3. Recuperación: con tutorial memoState ≥ 5, al hablar con Laferon o al login se llama
   `OfferPendingDwarvenStoryDialog` (ACCEPT pendiente, o COMPLETE de estados DONE atascados).
4. `StoryQuest.onFirstTalk` no llama `showChatWindow` si la quest no actúa.

Hook: `protected virtual bool AutoRegisterNpcFirstTalk => true` en `Quest`.

## Errores frecuentes

- Quest visible pero al aceptar no pasa nada → falta `questManager.addQuest(...)`.
- Teleport de UI no mueve → ID ausente en `TeleportListData.xml` (revisar log
  `missing TeleportListData id`).
- `preQuestId` ignorado → la pre-quest se registró **después** que la actual.
- Cond `DONE` no persiste → `setCond(QuestCondType)` debe guardar el valor
  numérico del enum (ya corregido en `QuestState`).
- Teleport a aldea antes de gremlins → el tutorial no debe teletransportar en `reward_2`;
  10071–10073 se hacen en el strip mine; 10074 saca al jugador.
