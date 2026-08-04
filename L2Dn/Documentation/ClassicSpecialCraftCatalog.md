# Tienda L-Coin y Special Craft — servidor Classic 447

## Resultado

El catálogo queda alineado con los objetos y árboles de habilidades que existen
en este datapack:

- `LimitShop.xml` conserva sus 41 productos y ya no usa Giran Seal (`92314`):
  los productos propios de la tienda cuestan L-Coin y los pergaminos comunes,
  Adena.
- `LimitShopCraft.xml` contiene 184 recetas reproducibles, repartidas entre las
  cinco categorías permanentes de la interfaz.
- Event queda vacío deliberadamente. No se publica una receta sin evento,
  calendario y fuente de moneda activos.
- Todos los ingredientes y resultados están definidos en `stats/items`, y todos
  los libros ofrecidos son consumidos por los árboles activos de tercera clase.

El archivo completo se regenera con:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\server\Build-ClassicSpecialCraftCatalog.ps1
```

## Distribución

| Categoría | Pestaña | Recetas | Contenido |
|---:|---|---:|---|
| 0 | Special Weapon | 12 | Paquetes de las doce armas Frost Lord |
| 2 | Spellbook | 87 | Todos los libros de tercera clase usados por el servidor |
| 3 | Accessories | 28 | Siete cadenas completas de accesorios de raid, Lv. 2–5 |
| 4 | Misc | 15 | Tabletas, Elixir, tintes, augmentación y pergamino estable |
| 5 | Blessing | 42 | Seis familias bendecidas, desde +4 hasta +10 |
| 6 | Event | 0 | Reservada para eventos temporales reales |

La categoría interna `1` no representa ninguna pestaña de esta versión del
cliente y no se utiliza.

## Special Weapon

Cada receta es determinística y consume:

- 1 Black Frozen Core (`95781`);
- 1.500 Frost Lord's Weapon Crystal (`95782`).

Los resultados son los paquetes `95823`–`95833` y `95835`, uno para cada tipo
de arma Frost Lord. Para que el circuito sea obtenible dentro del juego, los
materiales se incorporan a raids ya existentes:

| Raid | NPC | Cristales garantizados | Probabilidad del core |
|---|---:|---:|---:|
| Scarlet van Halisha / Frintezza | 29047 | 75–125 | 10% |
| Baium | 29020 | 125–200 | 20% |
| Antharas | 29068 | 200–300 | 30% |

## Spellbook

Se publican 87 libros, con resultados `90046`–`90135`, excepto `90052`,
`90074` y `90132`. Esas exclusiones no son consumidas por los árboles activos.
Cada libro cuesta 31 Magical Tablet (`90045`) y 100.000 Adena. Las recetas usan
87 ProductId libres que ya existen en `PurchaseLimitCraft_ClassicAden-eu.dat`;
así el índice interno del cliente reconoce cada producto y muestra nombre,
icono y vista previa correctamente.

La pestaña se conecta con Misc: una Magical Tablet se fabrica con 20 fragmentos
de Fire, Water, Wind o Earth (`91040`, `91039`, `91041`, `91042`) más 200.000
Adena. Esas cuatro recetas usan ProductId oficiales libres `3662`–`3665`.

## Accessories

Hay cuatro etapas para Frintezza, Antharas, Baium, Zaken, Queen Ant, Orfen y
Core:

| Etapa | Materiales | Resultado |
|---|---|---|
| Lv. 2 | 2 accesorios base + 5.000.000 Adena | Lv. 2, 100% |
| Lv. 3 | 2 accesorios Lv. 2 + 20.000.000 Adena | Lv. 3, 100% |
| Lv. 4 | 2 accesorios Lv. 3 + 100.000.000 Adena | Lv. 4, 100% |
| Lv. 5 | 1 accesorio Lv. 4 + 1 base + tarifa | Lv. 5 con 3–4% |

La tarifa Lv. 5 es 300.000.000 Adena para Frintezza, Antharas y Baium, y
150.000.000 para Zaken, Queen Ant, Orfen y Core. Si falla el intento, el servidor
devuelve el accesorio Lv. 4 y reembolsa la tarifa de Adena; el accesorio base sí
se consume. Esta regla se declara con `refundAdenaOnFailure="true"` y se procesa
en el flujo de compra, no como una recompensa ficticia del catálogo.

La sucesión de opciones del cliente se desactiva (`keep_option = 0`) porque el
servidor todavía no implementa ese protocolo. Así no se ofrece una promesa que
el servidor no pueda cumplir.

## Misc

| Recetas | Requisito | Resultado |
|---:|---|---|
| 4 | 20 fragmentos elementales + 200.000 Adena | 1 Magical Tablet |
| 1 | 10 Elixir Powder | 5% Elixir; 95% devolución de 7 polvos |
| 1 | 5 Dye Powder + 500.000 Adena | 8% ×3 / 67% ×1 Enhanced Dye Powder; 25% devolución de 2 polvos |
| 8 | 2 piedras de augmentación + 1.000.000 Adena | 15% piedra bendecida; 85% devolución de 1 piedra base |
| 1 | 5 Scroll: Enchant Rare Accessories + 1.000 L-Coin + 10.000.000 Adena | Stable Scroll: Enchant Rare Accessories |

Todas las probabilidades constituyen una única distribución de 100%; el flujo
del servidor realiza un solo sorteo por intento.

## Blessing

Las seis familias son Dragon Belt, Talisman of Speed, Talisman of Eva, Circlet
of Hero, Talisman of Authority y Talisman of Aden. Para cada nivel de +4 a +10,
la receta consume dos objetos base con exactamente ese encantamiento y entrega
la variante bendecida correspondiente. Son 7 niveles por 6 familias: 42 recetas
determinísticas.

## Event

No hay recetas permanentes. Cuando exista un evento concreto debe agregarse como
un cambio autocontenido: período de vigencia, fuente del material, límites,
premios, probabilidades, entrada de cliente y prueba que impida que quede activo
fuera de fecha.

## Invariantes verificadas

Las pruebas automatizadas fijan el número de recetas por pestaña, ausencia total
de Giran Seal, existencia de cada ItemId, correspondencia con los skill trees,
fuentes de Frost Lord, fórmulas de accesorios, rangos +4–+10 y suma de todas las
probabilidades. El paquete cliente se verifica además contra el XML después de
cifrarlo: ProductId conocidos y nombres/iconos presentes para cada ingrediente
y resultado.
