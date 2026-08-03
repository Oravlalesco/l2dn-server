# Tienda L-Coin y Special Craft — servidor Classic 447

## Resultado de esta fase

Los dos sistemas quedan separados según el tipo de tienda que envía el protocolo:

- `LimitShop.xml` (`shopType = 3`) contiene los 41 productos reconocidos por el
  `LCoinShopProduct_Classic-eu.dat` actual.
- `LimitShopCraft.xml` (`shopType = 4`) contiene 41 recetas propias de Special
  Craft, organizadas por su categoría semántica.
- `LimitShopClan.xml` (`shopType = 100`) no se modifica.

Los productos `10069` y `10070` siguen excluidos de la tienda L-Coin porque sus
recompensas (`99041` y `99042`) no existen en el datapack del servidor.

## Categorías de Special Craft

| Código | Pestaña | Recetas activas | Decisión |
|---:|---|---:|---|
| 0 | Special Weapon | 0 | Reservada hasta definir una fuente real para los materiales de arma |
| 2 | Spellbook | 4 | Cupones de 1–4 estrellas mediante Giran Seal |
| 3 | Accessories | 14 | Intercambios determinísticos por paquetes +6 |
| 4 | Misc | 11 | Pergaminos y conversión de piedras de augmentación |
| 5 | Blessing | 12 | Combinación de dos accesorios +4/+5 equivalentes |
| 6 | Event | 0 | Reservada para un evento con fecha y moneda activas |

La categoría `1` no corresponde a ninguna de las seis pestañas de Special Craft
de este cliente y no se usa en `LimitShopCraft.xml`.

## Recetas activas

### Spellbook

| ProductId | Resultado | Costo | Éxito | Resultado alternativo |
|---:|---|---:|---:|---|
| 4238 | Cupón de libro 1 estrella | 49 Giran Seal | 50% | 5 Giran Seal |
| 4239 | Cupón de libro 2 estrellas | 60 Giran Seal | 30% | 6 Giran Seal |
| 4240 | Cupón de libro 3 estrellas | 137 Giran Seal | 10% | 13 Giran Seal |
| 4241 | Cupón de libro 4 estrellas | 1.208 Giran Seal | 5% | 120 Giran Seal |

### Accessories

Se activan catorce intercambios determinísticos: Hunter's Earring, Piercing
Mask, Circlet of Hero, Talisman of Authority, Talisman of Eva, Talisman of
Speed, Dragon Belt, Cloak of Protection y seis Agathions (Dragon Egg, Ignis,
Nebula, Procella, Petram y Joy). Cada receta usa el paquete o kit base y Adena.

### Misc

Se activan el pergamino estable de accesorios raros, la Incredible Upgrade
Stone, el pergamino de accesorios y ocho conversiones de piedras de
augmentación. Estas últimas tienen 15% de producir la piedra bendecida y 85% de
devolver una de las dos piedras base consumidas.

### Blessing

Se activan conversiones +4 y +5 para Talisman of Aden, Talisman of Authority,
Circlet of Hero, Dragon Belt, Talisman of Speed y Talisman of Eva. Cada receta
consume exactamente dos objetos del mismo nivel de encantamiento y produce su
versión bendecida.

## Correcciones del flujo de compra

- Los resultados aleatorios usan un único valor por intento y una distribución
  acumulada. Antes cada rama ejecutaba un sorteo nuevo y alteraba las
  probabilidades declaradas.
- Los ingredientes repetidos se agrupan por `ItemId` y encantamiento antes de
  validarlos y consumirlos.
- La cantidad declarada de un ingrediente encantado se respeta; antes se
  destruía sólo un objeto por intento.
- Los puntos VIP se aplican una vez por compra y no una vez por ingrediente.
- Los contadores de cuenta permanecen aislados por `shopType` y `ProductId`.

## Límite de esta fase: cliente

No se modifica ningún archivo del cliente. El DAT Classic 447 actual contiene
los productos de la tienda L-Coin, pero no los ProductId restaurados para
Special Craft. Por ello:

- la tienda L-Coin puede alinearse inmediatamente con sus 41 registros actuales;
- las nuevas recetas de Special Craft quedan cargadas y comprables en el
  servidor, pero no aparecerán correctamente en la interfaz hasta una fase
  posterior de cliente;
- cambiar solamente `category` en el XML no mueve un producto entre pestañas,
  porque la presentación y la categoría visual provienen del DAT.

Cuando se aborde el cliente, cada registro deberá reproducir como mínimo el
`ProductId`, la categoría, todos los resultados posibles y los límites del XML.
