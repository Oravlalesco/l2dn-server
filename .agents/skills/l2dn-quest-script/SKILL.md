---
name: l2dn-quest-script
description: >-
  Guía y patrón para crear o modificar quests en C# para L2Dn Server.
  Usar cuando el usuario quiera portar una quest de L2J (Java), crear una
  quest nueva, o entender cómo funciona el sistema de quests (eventos,
  estado, recompensas) en L2Dn.GameServer.Scripts.
---

# L2Dn — Quests en C#

Las quests viven en `L2Dn/L2Dn.GameServer.Scripts/Quests/`. Cada quest es una clase C# que hereda de `Quest`.

La quest de referencia (ya funcional) es [`Q00206Tutorial.cs`](../../../L2Dn.GameServer.Scripts/Quests/Q00206Tutorial.cs).

---

## Estructura de una quest

```csharp
using L2Dn.GameServer.Model.Quests;
// + otros usings necesarios

namespace L2Dn.GameServer.Scripts.Quests;

public sealed class Q00206Tutorial : Quest
{
    // Constantes: IDs de NPCs, ítems, etc.
    private const int MY_NPC = 30009;
    private const int MY_ITEM = 6353;

    // Constructor: registra el quest con su ID y nombre
    public Q00206Tutorial() : base(206)
    {
        // Registro de NPCs que participan
        AddStartNpc(MY_NPC);
        AddTalkId(MY_NPC);
        AddKillId(/* monster ids */);
    }

    // Suscripción a eventos con atributos
    [SubscribeEvent]
    private void OnPlayerLogin(OnPlayerLogin evt)
    {
        // lógica...
    }
}
```

---

## Convención de nombres

- Archivo y clase: `Q{id:05}{NombreDescriptivo}` → `Q00206Tutorial`
- Namespace: `L2Dn.GameServer.Scripts.Quests`
- ID de quest: número único que no colisione con quests existentes

---

## Sistema de eventos

Las quests reaccionan a eventos del servidor mediante el atributo `[SubscribeEvent]`:

```csharp
using L2Dn.GameServer.Model.Events.Annotations;
using L2Dn.GameServer.Model.Events.Impl.Players;
using L2Dn.GameServer.Model.Events.Impl.Npcs;

[SubscribeEvent]
private void OnPlayerLogin(OnPlayerLogin evt) { ... }

[SubscribeEvent]
private void OnNpcTalk(OnNpcFirstTalk evt) { ... }

[SubscribeEvent]
private void OnKill(OnAttackableKill evt) { ... }
```

Los eventos disponibles están en `L2Dn.GameServer.Model/Events/Impl/`.

---

## Estado de quest por jugador

El estado se guarda en la BD a través del sistema `QuestState`:

```csharp
QuestState qs = player.getQuestState(Name);
if (qs == null)
    qs = newQuestState(player);

// Variables arbitrarias
qs.set("my_variable", "value");
string val = qs.get("my_variable");

// Estado de la quest
qs.startQuest();
qs.exitQuest(false); // false = no repetible, true = repetible
```

---

## Recompensas

```csharp
// Ítems
giveItems(player, ITEM_ID, count);
takeItems(player, ITEM_ID, count);

// Adena
giveAdena(player, 75_000, true);  // true = mostrar mensaje

// Exp / SP
addExpAndSp(player, exp, sp);

// Con holder
giveItems(player, new ItemHolder(ITEM_ID, count));
```

---

## HTML de diálogos

Los diálogos HTML van en:
`L2Dn/L2Dn.GameServer/DataPack/html/default/{npcId}.htm`
o rutas específicas de quest.

Desde la quest:
```csharp
return buildHtmlFile(npc, player, "path/to/file.htm");
// o
return getHtml("tutorial_new_character001.html");
```

---

## Registro de la quest

Las quests se registran automáticamente en `Scripts.cs` a través del mecanismo de reflection de `ScriptManager`. Verificar que la clase esté en el namespace correcto y sea `public`.

Si la quest no aparece, revisar `Scripts.cs` para ver si hay registro manual requerido.

---

## Checklist para una quest nueva

1. [ ] Crear `Q{id}{Nombre}.cs` en `L2Dn.GameServer.Scripts/Quests/`
2. [ ] Heredar de `Quest`, constructor llama `base(id)`
3. [ ] Registrar NPCs/mobs con `AddStartNpc`, `AddTalkId`, `AddKillId`, etc.
4. [ ] Implementar handlers con `[SubscribeEvent]`
5. [ ] Crear HTMLs en DataPack si hay diálogos
6. [ ] Compilar y verificar con `dev-publish.ps1` + `dev-restart.ps1 -Code`
7. [ ] Probar con un personaje de la raza/clase objetivo

---

## Portar desde L2J (Java)

El código Java original usa `notifyEvent`, `onTalk`, `onKill` como métodos override. En L2Dn se reemplazan por suscriptores de evento con `[SubscribeEvent]`. La lógica de negocio (IDs, variables, condiciones) se traslada directamente. Los nombres de métodos helper son similares (`giveItems`, `addExpAndSp`, etc.).
