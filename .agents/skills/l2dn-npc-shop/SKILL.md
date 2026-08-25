---
name: l2dn-npc-shop
description: >-
  Guía detallada para crear o modificar NPC shops en el DataPack de L2Dn
  (GM Shop, mercaderes, buylists, multisell). Usar cuando el usuario quiera
  agregar un NPC vendedor, crear una buylist, ajustar precios, o hacer spawnar
  un NPC de tienda en el mundo.
---

# L2Dn — NPC Shops (Tiendas / GM Shop)

Un shop es **solo DataPack**: 4 archivos XML/HTM. No requiere C# ni compilación.
Aplicar cambios: `cd Docker && .\dev-restart.ps1`

**Documentación completa**: [`L2Dn/Documentation/NpcShop.md`](../../../L2Dn/Documentation/NpcShop.md)

**Ejemplo de referencia (funcionando)**: NPC ID `50009`

---

## Los 4 archivos de un shop

```
DataPack/
├── stats/npcs/custom/GmShopMerchant.xml     ← definición del NPC
├── html/merchant/50009.htm                  ← HTML del menú de tienda
├── buylists/custom/5000901.xml              ← lista de ítems y precios
└── spawns/Others/GmShopSpawns.xml           ← dónde aparece en el mundo
```

---

## 1. Definición del NPC (`stats/npcs/custom/`)

```xml
<?xml version="1.0" encoding="utf-8"?>
<list xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xsi:noNamespaceSchemaLocation="../../../xsd/npcs.xsd">
  <npc id="50009" type="Merchant" name="GM Shop" title="">
    <stats>
      <vitals hp="2444" hpRegen="7.5" mp="1345" mpRegen="2.7"/>
    </stats>
    <ai type="Merchant" defaultTeam="NONE" clan="" />
    <appearance sex="MALE" />
  </npc>
</list>
```

- `type="Merchant"` es **obligatorio** para tiendas
- XSD: `../../../xsd/npcs.xsd` (relativo a `stats/npcs/custom/`)
- Carpeta `stats/npcs/custom/` cargada por `CustomNpcData = True` en `Config/General.ini`

---

## 2. HTML del menú (`html/merchant/{npcId}.htm`)

Nombre del archivo: exactamente `{npcId}.htm` — sin fallback si falta.

```html
<html><body>
GM Shop:<br>
<a action="bypass -h npc_%objectId%_Buy 5000901">Armas</a><br>
<a action="bypass -h npc_%objectId%_Buy 5000902">Armaduras</a><br>
</body></html>
```

- El bypass usa el **ID del buylist** (= nombre del archivo sin extensión)
- `%objectId%` se reemplaza automáticamente por el ID de instancia del NPC

---

## 3. Buylist (`buylists/custom/`)

```xml
<?xml version="1.0" encoding="utf-8"?>
<list xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xsi:noNamespaceSchemaLocation="../../xsd/buylist.xsd">
  <list id="5000901" name="GM Shop - Armas">
    <npcs>
      <npc>50009</npc>   <!-- OBLIGATORIO: sin esto la ventana no abre -->
    </npcs>
    <item id="57" price="0" />       <!-- Adena gratis (ojo: solo si referencePrice > 0) -->
    <item id="1146" price="-1" />    <!-- Sword of Damascas al referencePrice completo -->
    <item id="1293" price="500000" /> <!-- Precio fijo en Adena -->
  </list>
</list>
```

**Reglas críticas de IDs y precios:**

| Situación | Resultado |
|---|---|
| ID buylist = nombre de archivo sin extensión | `5000901.xml` → ID `5000901` |
| IDs duplicados entre archivos | **Crash al arrancar** |
| `<npcs>` ausente | Ventana de compra no abre |
| `price="0"` con `CorrectPrices=True` y `referencePrice=0` | Ítem **gratis** (peligroso en tiendas públicas) |
| `price="-1"` | = `referencePrice` completo (puede ser 0) |
| `price="0"` con `referencePrice > 0` | EL precio se corrige a `referencePrice/2` |

- XSD: `../../xsd/buylist.xsd` (relativo a `buylists/custom/`)
- Directorio `buylists/` **no es recursivo**: custom va en `buylists/custom/` (flag `CustomBuyListLoad`)
- Ítems con `referencePrice = 0` (Soul Crystals ensoul, etc.): **no ponerlos con `price="0"` en tiendas públicas**

---

## 4. Spawn (`spawns/`)

```xml
<?xml version="1.0" encoding="utf-8"?>
<list enabled="true"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xsi:noNamespaceSchemaLocation="../../xsd/spawns.xsd">
  <spawn>
    <npc id="50009" x="-80820" y="149770" z="-3043" heading="16384"
         respawnTime="60" />
  </spawn>
</list>
```

- `spawns/` **sí es recursivo**: puede ir en cualquier subcarpeta
- `heading`: 0=Norte, 16384=Este, 32768=Sur, 49152=Oeste (en unidades L2)
- `respawnTime` en segundos (para NPCs vendedores no importa mucho)

---

## Checklist para un shop nuevo

1. [ ] Elegir NPC ID libre (custom: 50000+)
2. [ ] Elegir IDs de buylist únicos (convención: `{npcId}01`, `{npcId}02`, …)
3. [ ] Crear `stats/npcs/custom/{nombre}.xml` con `type="Merchant"`
4. [ ] Crear `html/merchant/{npcId}.htm` con bypasses a cada buylist
5. [ ] Crear `buylists/custom/{buylistId}.xml` por cada categoría
6. [ ] Añadir `<npc>{npcId}</npc>` en cada buylist (¡no olvidar!)
7. [ ] Crear/editar spawn en `spawns/`
8. [ ] Verificar XSD paths relativos
9. [ ] Reiniciar: `cd Docker && .\dev-restart.ps1`
10. [ ] Comprobar log del servidor — crash por ID duplicado se ve al arrancar

---

## Multisell

Para intercambio de ítems (multisell), los archivos van en `DataPack/multisell/` con su propio esquema XSD. Consultar `L2Dn/Documentation/NpcShop.md` para el formato completo.
