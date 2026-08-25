# L2Dn — NPC shops (tiendas / GM Shop)

**Guía completa: `L2Dn/Documentation/NpcShop.md`** (leerla antes de crear/adaptar un shop).
Ejemplo de referencia funcionando: NPC 50009 (`stats/npcs/custom/GmShopMerchant.xml`,
`html/merchant/50009.htm`, `buylists/custom/5000901.xml`, `spawns/Others/GmShopSpawns.xml`).

## Reglas duras

- Un shop es solo DataPack (4 XML/HTM); **no** requiere C# ni AI script. El NPC debe ser `type="Merchant"`.
- El id del buylist **es el nombre del archivo** (`5000901.xml` → 5000901). Ids duplicados entre archivos = crash al arrancar.
- Todo buylist necesita `<npcs><npc>{npcId}</npc></npcs>` o la ventana de compra no abre.
- `buylists/` y `stats/npcs/` NO son recursivas: custom va en `buylists/custom/` y `stats/npcs/custom/` (flags `CustomBuyListLoad`/`CustomNpcData` ya activos en `Config/General.ini`). `spawns/` SÍ es recursiva.
- HTML del menú: `html/merchant/{npcId}.htm` exacto, sin fallback si falta. Botones: `action="bypass -h npc_%objectId%_Buy {buylistId}"`.
- XSD relativos: `buylists/custom/` → `../../xsd/buylist.xsd`; `stats/npcs/custom/` → `../../../xsd/npcs.xsd`.
- **Precios**: con `CorrectPrices = True` (actual), `price="0"` solo se corrige si `referencePrice/2 > 0`. Ítems con `referencePrice = 0` (p. ej. Soul Crystals ensoul) se venden **gratis** — no ponerlos en tiendas públicas con `price="0"`. `price="-1"` = referencePrice completo (también 0 si no hay referencia).
- Aplicar cambios sin recompilar: `cd Docker; .\dev-restart.ps1` (ver regla l2dn-docker).
