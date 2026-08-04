# Misiones públicas (Daily Missions / One Day Reward)

## Qué sistema es

Las **Misiones** del cliente entregan objetos al alcanzar un objetivo público. No son las *Quests* narrativas: no se aceptan ni se entregan hablando con un NPC. El catálogo es común para todos los personajes y el progreso se guarda por personaje.

| Sistema | Fuente | Inicio | Persistencia |
|---|---|---|---|
| Misiones / One Day Reward | `DataPack/DailyMission.xml` | Eventos globales | `CharacterDailyRewards` |
| Quests | scripts `Quest` y `NewQuestData.xml` | NPC, diálogo o script | estados de quest |
| Mission Level | temporada/pase | puntos de misión | sistema `MissionLevel` |

La identidad visible procede del `id` de `OneDayReward_Classic-<idioma>.dat` del cliente. El XML del servidor no puede crear por sí solo el nombre, descripción o icono visibles.

## Cómo se contabiliza una baja

`Attackable.doDie` publica un único `OnAttackableKill`. Un evaluador central, suscrito a `GlobalEvents.Global`, indexa las reglas por ID de NPC, área de origen y rango de nivel del personaje. Esto incluye `Monster`, `RaidBoss` y `GrandBoss`.

- Recibe crédito solamente el personaje dueño del golpe final.
- Una invocación, mascota o servitor acredita a su dueño.
- No se comparte progreso con party ni command channel.
- El ID evaluado es el ID de plantilla del NPC (`monster.getId()`), no el object ID de la instancia.
- Una regla por área usa el **origen del spawn**. Arrastrar un monstruo externo dentro de la zona no cuenta; sacar fuera uno originado dentro sí cuenta.
- Los minions sin `Spawn` propio heredan el origen de su líder.
- Las reglas combinadas se deduplican: coincidir por ID y área en la misma baja aumenta una sola vez.
- El instante se captura al morir. Si el callback se procesa después de las 06:30, la baja conserva el ciclo en el que ocurrió.

Las misiones genéricas `ANY` aplican la regla del catálogo Classic: un monstruo cinco o más niveles inferior al personaje no cuenta. Es decir, cuenta desde `monsterLevel >= playerLevel - 4`, sin límite superior. Las misiones por ID o área no aplican diferencia relativa de nivel; sólo `minLevel` y `maxLevel` del personaje.

## Persistencia y recompensas

El progreso cambia inmediatamente en memoria y el contador reclamable se sincroniza al pasar a `AVAILABLE`. El detalle de progreso se obtiene al abrir la lista; no se envía el paquete completo en cada baja.

Las entradas modificadas se consolidan por personaje cada dos segundos en una transacción y un `SaveChanges`. Si falla, conservan su versión sucia y se reintentan. Cobrar, guardar/desconectar y el guardado periódico fuerzan un *flush*. Una terminación abrupta del proceso o host puede perder como máximo la ventana pendiente de dos segundos.

El cobro usa `CharacterDailyMissionRewardGrants`, cuya clave única es `(CharacterId, RewardId, CycleStart)`. En la misma transacción se valida `AVAILABLE`, se marca `COMPLETED`, se inserta el ledger y se crean o actualizan todos los items:

- Si caben peso y slots de todo el paquete, todos van al inventario.
- Si no cabe el paquete completo, todos van adjuntos a un único correo de sistema `DAILY_MISSION_REWARD`.
- Nunca se entrega una parte en inventario ni se arrojan items al suelo.
- Una excepción o conflicto revierte toda la transacción y la misión continúa reclamable.
- Una solicitud concurrente o repetida encuentra el grant existente y no duplica la recompensa.

Las migraciones requeridas son `20260803120000_DailyMissionCycles` y `20260804120000_DailyMissionRewardGrants`.

## Contrato de `DailyMission.xml`

Los atributos principales de `<reward>` son:

| Atributo | Regla |
|---|---|
| `id` | Positivo, único, dentro de `Int16`, estable y existente en el DAT del cliente. |
| `requiredCompletion` | Obligatorio y mayor que cero. |
| `dailyReset` | Habilita recurrencia; por defecto `true`. |
| `isOneTime` | Por defecto `true`; una misión de una sola vez no reinicia. |
| `duration` | `DAY`, `WEEK`, `MONTH` o `WEEKEND`; corte local a las 06:30. |
| `isMainClassOnly` | Por defecto `false`. Activarlo sólo cuando el diseño lo exige. |
| `isDualClassOnly` | Por defecto `false`; no combinar con main-only. |
| `classId` | Opcional y repetible; si no existe, cualquier clase activa es elegible. |

`<items>` exige al menos un item existente y cantidades positivas. El catálogo actual utiliza recompensas de item, que son transaccionales.

### Reglas de monstruos

El handler `monster` acepta:

| Parámetro | Uso |
|---|---|
| `targetMode` | `ANY`, `NPC_IDS`, `SPAWN_AREAS` o `NPC_IDS_OR_SPAWN_AREAS`. |
| `ids` | Lista de NPC IDs. Obligatoria en los modos que incluyen IDs. |
| `areas` | Lista de tags de origen. Obligatoria en los modos que incluyen áreas. |
| `excludedIds` | IDs que nunca cuentan aunque su origen coincida. |
| `minLevel`, `maxLevel` | Rango inclusivo del personaje. |
| `minMonsterLevelOffset` | Sólo para `ANY`; por defecto `-4`. |
| `startHour`, `endHour` | Ventana local `HH:mm`, incluida si cruza medianoche. |

Por compatibilidad, una regla antigua sin `targetMode` se interpreta como `NPC_IDS` si contiene `ids`, o `ANY` si no los contiene. Para definiciones nuevas se debe declararlo.

El loader falla al arrancar ante parámetros desconocidos, modos incoherentes, NPC no atacables, IDs duplicados, áreas sin spawns atacables o recompensas inválidas.

## Áreas de origen de spawn

Los XML bajo `DataPack/spawns` aceptan dos atributos:

```xml
<list dailyMissionAreas="varka_silenos_barracks">
    ...
</list>
```

```xml
<spawn dailyMissionAreas="giants_cave" excludedDailyMissionAreas="legacy_area">
    <group dailyMissionAreas="lower_floor">
        <npc id="12345" excludedDailyMissionAreas="lower_floor" />
    </group>
</spawn>
```

`dailyMissionAreas` y `excludedDailyMissionAreas` son listas separadas por coma y se admiten en `list`, `spawn`, `group` y `npc`. La resolución hereda en ese orden: agrega inclusiones y después elimina exclusiones. Los nombres sólo usan letras, números y `_`.

Etiquetar el nivel más alto que describa correctamente el origen. Un archivo dedicado como Varka debe etiquetarse en `list`; si un archivo mezcla regiones, usar `spawn`, `group` o `npc`. Al agregar otro NPC atacable dentro de una estructura ya etiquetada, comenzará a contar automáticamente. Si no debe contar, usar `excludedIds` en la misión o `excludedDailyMissionAreas` en su nodo de spawn.

Los spawns creados por script pueden llamar `Spawn.addDailyMissionArea("area")`. Un spawn sin tag falla de forma cerrada para reglas `SPAWN_AREAS`.

Al cargar se registra un resumen como:

```text
Daily mission spawn-area coverage: varka_silenos_barracks=22 NPCs, ...
```

Para diagnóstico desde código o un comando GM se puede usar `MonsterDailyMissionHandler.diagnoseKill(player, monster)`, que devuelve ID, tipo, nivel, áreas de origen y misiones coincidentes.

## Añadir una misión

1. Reservar un `id` válido en todos los DAT Classic soportados.
2. Elegir el evento. Para bajas, decidir si la descripción habla de cualquier monstruo, especies concretas, una zona o una combinación.
3. Si usa zona, etiquetar primero los spawns y comprobar que todos los NPC de esa zona heredan el tag correcto.
4. Añadir el bloque y reiniciar el Game Server.

Ejemplo público para cualquier clase activa:

```xml
<reward id="ID_CLIENTE" name="Etiqueta operativa" requiredCompletion="300"
        dailyReset="true" isOneTime="false" duration="DAY">
    <handler name="monster">
        <param name="targetMode">SPAWN_AREAS</param>
        <param name="areas">varka_silenos_barracks</param>
        <param name="excludedIds">ID_EXCLUIDO_OPCIONAL</param>
        <param name="minLevel">78</param>
        <param name="maxLevel">99</param>
    </handler>
    <items>
        <item id="ID_ITEM" count="1" />
    </items>
</reward>
```

Probar al menos: baja válida e inválida, NPC antes omitido, monstruo externo arrastrado, minion, raid, clase principal/sub/dual, party, cruce de 06:30, reconexión, cobro concurrente e inventario lleno.

## Modificar una misión

- Mantener `id` si sigue representando la misma entrada del cliente.
- Actualizar también los DAT si cambia el texto, objetivo o presentación.
- No cambiar ciclo o semántica de una misión con progreso existente sin política de migración.
- Si una zona recibe NPC nuevos, no añadirlos uno por uno a `ids`: verificar su tag de origen.
- Si el texto nombra una criatura concreta dentro de una zona, usar `NPC_IDS`; si acepta ambas fuentes, `NPC_IDS_OR_SPAWN_AREAS`.
- Cambiar recompensas afecta los cobros aún no realizados; los grants ya creados conservan `RewardSnapshot` para auditoría.

## Eliminar una misión

1. Retirar su `<reward>` y reiniciar.
2. No reutilizar el `id` para otro significado.
3. Con respaldo previo, limpiar progreso y ledger en una transacción:

```sql
BEGIN;
DELETE FROM "CharacterDailyMissionRewardGrants" WHERE "RewardId" = ID_RETIRED;
DELETE FROM "CharacterDailyRewards" WHERE "RewardId" = ID_RETIRED;
COMMIT;
```

No eliminar correos o items ya concedidos: son recompensas materializadas y el ledger sirve para auditarlas.

## Consultas de diagnóstico

```sql
SELECT "CharacterId", "RewardId", "Status", "Progress", "LastCompleted", "CycleStart"
FROM "CharacterDailyRewards"
WHERE "CharacterId" = CHARACTER_ID
ORDER BY "RewardId";

SELECT "CharacterId", "RewardId", "CycleStart", "DeliveryKind", "MailMessageId",
       "RewardSnapshot", "CreatedAt", "DeliveredAt"
FROM "CharacterDailyMissionRewardGrants"
WHERE "CharacterId" = CHARACTER_ID
ORDER BY "CreatedAt" DESC;
```

Estados: `1 = AVAILABLE`, `2 = NOT_AVAILABLE`, `3 = COMPLETED`. Delivery: `1 = inventario`, `2 = correo`.

## Verificación

```powershell
docker run --rm -v C:/ruta/l2dn-server:/src -w /src/L2Dn `
  mcr.microsoft.com/dotnet/sdk:9.0-alpine `
  dotnet test Tests/L2Dn.GameServer.Model.Tests/L2Dn.GameServer.Model.Tests.csproj

docker run --rm -v C:/ruta/l2dn-server:/src -w /src/L2Dn `
  mcr.microsoft.com/dotnet/sdk:9.0-alpine `
  dotnet test Tests/L2Dn.GameServer.StaticData.Tests/L2Dn.GameServer.StaticData.Tests.csproj
```

Para aplicar migraciones y reiniciar el entorno de desarrollo:

```powershell
cd Docker
.\dev-restart.ps1 -Code -Migrate
```
