# Catálogo Special Craft — Classic 447

## Objetivo

`DataPack/LimitShopCraft.xml` contiene únicamente productos reconocidos por el
`LCoinShopProduct_Classic-eu.dat` activo en el cliente Classic 447/Shinemaker.
El catálogo anterior contenía 355 recetas importadas de otra versión y ninguno
de sus `ProductId` coincidía con los 43 registros del cliente activo.

La primera versión compatible publica 41 productos. Los productos `10069` y
`10070` se reservan porque sus recompensas (`99041` y `99042`) no tienen una
definición de ítem en el datapack actual.

## Cobertura

| Categoría | Pestaña del cliente | Productos | Estado |
|---:|---|---:|---|
| 0 | Special Weapons | 3 | Activa |
| 1 | Spellbook | 8 | Activa |
| 2 | Accessories | 7 | Activa |
| 3 | Misc | 8 | Activa |
| 4 | Blessing | 15 | Activa |
| 5 | Event | 0 | Requiere parche del DAT |

Los nombres de pestaña pertenecen a la interfaz de Special Craft. Algunos
productos heredados del DAT no son semánticamente perfectos para su pestaña;
cambiar su recompensa o categoría visual exige modificar el DAT y el XML en
conjunto.

## Economía inicial

El catálogo utiliza solamente recursos obtenibles y definidos en el servidor:

- `57`: Adena, para utilidad permanente y progresión básica;
- `91663`: L-Coin, para consumibles y progresión frecuente;
- `92314`: Giran Seal, para encantamiento y recompensas de mayor valor.

Se conservaron los costos ya presentes en el datapack cuando existía una
equivalencia clara. Los demás valores forman una línea base conservadora y deben
revisarse con telemetría real de adquisición y consumo.

Los límites visuales declarados por el cliente se replican en el servidor:

- `10035`: 20 compras diarias;
- `10066`, `10067`, `10068`: 1 compra diaria;
- `10071`: 10 compras diarias;
- `10073`: 2 compras totales por cuenta;
- `10074`: 100 compras diarias;
- `10124`–`10129`: 5 compras diarias.

## Contrato cliente/servidor

Para cada producto deben coincidir:

1. `ProductId`;
2. categoría visual;
3. ítem y cantidad producida;
4. tipo y cantidad máxima de compra.

El servidor controla los ingredientes, cantidades, nivel permitido,
probabilidades y aplicación efectiva de los límites. El cliente controla la
categoría y la presentación del producto. Cambiar sólo uno de los lados puede
mostrar una recompensa incorrecta o dejar el producto como no disponible.

Las claves de límites se guardan por `shopType` y `ProductId`, por ejemplo
`LCSDailyCount4_10035`. Esto evita que dos productos con la misma recompensa, o
dos tiendas diferentes, compartan accidentalmente el contador.

## Extensión futura

Para poblar Event o reemplazar por completo los productos heredados:

1. editar `LCoinShopProduct_Classic-eu.dat`;
2. asignar un `ProductId` único y categoría `0`–`5`;
3. declarar la recompensa y el límite visual;
4. crear la entrada idéntica en `LimitShopCraft.xml`;
5. ejecutar `ClassicSpecialCraftCatalogTests`;
6. probar listado, materiales, compra, límite y reinicio diario dentro del juego.
