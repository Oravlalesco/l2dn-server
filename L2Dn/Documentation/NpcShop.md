# NPC Shop (GM Shop) — guía completa

Cómo crear, adaptar o extender un NPC mercader (tienda) en L2Dn. Ejemplo de referencia
en el repo: **NPC 50009 "Suministros"** (junto al Scheme Buffer en Giran; solo consumibles
básicos en `5000901` — no vender Soul Crystals / ensoul en tienda pública).

Un shop es **puro DataPack**: no requiere código C# ni script de AI. El `type="Merchant"`
ya trae todo el comportamiento y el bypass handler `Buy` es global.

## Flujo interno

```
Click NPC → showChatWindow → DataPack/html/merchant/{npcId}.htm
    → botón "bypass -h npc_%objectId%_Buy {buylistId}"
    → BypassHandler "Buy" (exige target Merchant)
    → Merchant.showBuyWindow → valida buyList.isNpcAllowed(npcId)
    → ExBuySellListPacket (ventana de compra/venta)
```

Código relevante:

| Qué | Archivo |
|-----|---------|
| Clase Merchant + `getHtmlPath` + `showBuyWindow` | `L2Dn/L2Dn.GameServer.Model/Model/Actor/Instances/Merchant.cs` |
| Handler bypass `Buy` | `L2Dn/L2Dn.GameServer.Scripts/Handlers/BypassHandlers/Buy.cs` |
| Cargador de buylists | `L2Dn/L2Dn.GameServer.Model/Data/Xml/BuyListData.cs` |
| Cargador de NPCs | `L2Dn/L2Dn.GameServer.Model/Data/Xml/NpcData.cs` |
| Cargador de spawns | `L2Dn/L2Dn.GameServer.Model/Data/Xml/SpawnData.cs` |
| Instanciación por `type` | `NpcTemplate.CreateInstance()` en `L2Dn/L2Dn.GameServer.Model/Model/Actor/Templates/NpcTemplate.cs` |

## Las 4 piezas (rutas bajo `L2Dn/L2Dn.GameServer/DataPack/`)

### 1. Definición del NPC — `stats/npcs/custom/*.xml`

Ejemplo: `stats/npcs/custom/GmShopMerchant.xml`.

- `type="Merchant"` es **obligatorio**: sin eso el bypass `Buy` se ignora (`Buy.cs` hace `target is not Merchant → return false`).
- `displayId` reutiliza el modelo de un NPC vanilla (50009 usa 30081, Helvetia) sin tocar el cliente.
- `usingServerSideName="true"` y `usingServerSideTitle="true"` para que se muestren `name`/`title` del XML.
- El bloque `<collision>` es obligatorio según `xsd/npcs.xsd`.
- Ids custom: usar rango 50000+ libre (comprobar con grep que no exista).
- La carpeta `stats/npcs/` NO es recursiva; los custom van exactamente en `stats/npcs/custom/`
  y requieren `CustomNpcData = True` en `Config/General.ini` (ya activo).

### 2. Menú HTML — `html/merchant/{npcId}.htm`

- El nombre del archivo debe ser exactamente el id del NPC (`Merchant.getHtmlPath` devuelve
  `html/merchant/{npcId}.htm`, o `{npcId}-{n}.htm` para páginas con `Chat n`).
- **Sin fallback**: si falta el archivo, el jugador ve "Html file is missing".
- Botón de compra: `<Button ALIGN=LEFT ICON="NORMAL" action="bypass -h npc_%objectId%_Buy 5000901">Texto</Button>`.
- Placeholders útiles: `%npcname%`, `%objectId%`.
- **Menús multipágina**: `bypass -h npc_%objectId%_Chat N` abre `html/merchant/{npcId}-N.htm`
  (handler `ChatLink.cs` → `showChatWindow(player, N)`); `Chat 0` vuelve a `{npcId}.htm`.
  Ejemplo: NPC 50010 (`50010.htm` menú Guerreros/Magos → `50010-1.htm`, `50010-2.htm`).
  Varias páginas pueden apuntar al mismo buylist (ej. joyería compartida).

### 3. Buylists — `buylists/custom/{buylistId}.xml`

- **El id del buylist es el nombre del archivo** (sin extensión), no un atributo XML.
- Convención (no obligatoria): `{npcId}{sufijo 2 dígitos}` → NPC 50009 → 5000901, 5000902…
- El bloque `<npcs><npc>50009</npc></npcs>` es **obligatorio**: `showBuyWindow` y
  `RequestBuyItemPacket` rechazan la compra si el NPC no está listado.
- Atributos de `<item>`: `id` (req), `price`, `count` + `restock_delay` (minutos, ambos o ninguno), `baseTax`.
- `buylists/` NO es recursiva; los custom van en `buylists/custom/` y requieren
  `CustomBuyListLoad = True` en `Config/General.ini` (ya activo).
- XSD: desde `buylists/custom/` la ruta es `../../xsd/buylist.xsd` (¡no `../../../`!).

### 4. Spawn — `spawns/**/*.xml`

Ejemplo: `spawns/Others/GmShopSpawns.xml`. Esta carpeta SÍ es recursiva; cualquier
subcarpeta vale. Formato:

```xml
<spawn name="GmShop">
    <group>
        <npc id="50009" x="83145" y="147780" z="-3467" heading="32000" respawnTime="60sec" />
    </group>
</spawn>
```

## Precios: la trampa de `CorrectPrices`

En `BuyListData.LoadBuyList`:

- Con `CorrectPrices = True` (valor actual en `General.ini`), todo `price >= 0` menor que
  `referencePrice / 2` del ítem se **sube automáticamente** a ese valor y genera un warning
  en el log por cada ítem.
- **Excepción crítica:** si el ítem tiene `referencePrice = 0` (muchas Soul Crystals / ensoul
  stones no definen `price` en ItemData), entonces `sellPrice = 0` y `CorrectPrices` **no
  corrige** nada → `price="0"` se vende **gratis**. No uses `price="0"` en NPC públicos para
  esos ítems; o pon precio explícito > 0, o no los vendas en tienda pública.
- `price="-1"` (negativo) usa el `referencePrice` **completo** y omite la corrección (sigue
  siendo 0 si el ítem no tiene precio de referencia).
- Para vender realmente gratis/barato a propósito: poner `CorrectPrices = False` en
  `Config/General.ini` (solo entornos de prueba / GM).

## Otras trampas conocidas

- **Ids de buylist duplicados entre archivos** → excepción al arrancar (`ToFrozenDictionary`).
- Ítems duplicados dentro de un mismo buylist → solo warning.
- `<item id>` inexistente en ItemData → warning y se omite el ítem.
- Rutas XSD relativas según profundidad: `stats/npcs/custom/` → `../../../xsd/npcs.xsd`;
  `buylists/custom/` y `spawns/Others/` → `../../xsd/...`.
- El bypass necesita el flag `-h` (`bypass -h npc_...`) para pasar la validación.
- El stock con `restock_delay` se persiste en la tabla `BuyLists` de la BD.

## Extras que el mismo NPC soporta (otros bypass handlers globales)

- `bypass -h npc_%objectId%_multisell {listId}` / `exc_multisell {listId}` — intercambios
  ítem-por-ítem, XMLs en `DataPack/multisell/` (handler `Multisell.cs`).
- `EnsoulWindow`, `Augment`, `Link`, etc. — ver `L2Dn.GameServer.Scripts/Handlers/BypassHandlers/`
  y su registro en `L2Dn.GameServer.Scripts/Scripts.cs`.

## Aplicar cambios (sin recompilar)

El DataPack y Config van montados como volumen en el contenedor (ver regla `l2dn-docker.mdc`):

```powershell
cd Docker
.\dev-restart.ps1
```

## Checklist para un shop nuevo

1. Elegir id NPC libre (50000+) y crear `stats/npcs/custom/MiShop.xml` (copiar de `GmShopMerchant.xml`).
2. Crear `html/merchant/{npcId}.htm` con un botón `Buy {buylistId}` por sección.
3. Crear `buylists/custom/{npcId}01.xml`, `{npcId}02.xml`… cada uno con `<npc>{npcId}</npc>`.
4. Crear spawn en `spawns/Others/`.
5. `cd Docker; .\dev-restart.ps1` y probar in-game.
