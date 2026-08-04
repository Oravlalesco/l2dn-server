# Parche de cliente para Special Craft Classic 447

## Qué archivo controla la ventana

Special Craft no se define en `LCoinShopProduct_Classic-eu.dat`. Según el perfil
activo del cliente, la interfaz usa la familia `Classic` o `ClassicAden` de:

- `system/eu/PurchaseLimitCraft_<variante>-eu.dat`: productos, resultados,
  probabilidades, categorías y límites visuales;
- `system/eu/PurchaseLimitCraftCategory_Classic.dat`: las pestañas;
- `system/eu/NpcString_Classic-eu.dat`: textos referenciados por
  `category_sub`;
- `system/eu/L2GameDataName.dat` y `ItemName_<variante>-eu.dat`: cadenas e
  índices de los nombres de los ítems;
- `system/eu/EtcItemgrp_<variante>.dat`, `Armorgrp_<variante>.dat` y
  `Weapongrp_<variante>.dat`: iconos y recursos visuales;
- `system/eu/item_baseinfo_<variante>.dat`, `AdditionalItemGrp_<variante>.dat`
  e `ItemStatData_<variante>.dat`: ficha base, metadata adicional y
  estadísticas que permiten al cliente materializar correctamente cada ítem.

Los ingredientes pertenecen al servidor. `LimitShopCraft.xml` los envía por
protocolo, con un máximo de cinco por receta. El DAT y el XML deben coincidir en
ProductId, categoría, resultado, cantidad, probabilidad, encantamiento y rango
de nivel.

## Diagnóstico y solución

El DAT Classic original contiene 90 registros para `shop_index = 4`, pero no
representa el nuevo catálogo del servidor. El generador conserva metadatos
compatibles de los DAT Classic y ClassicAden y reconstruye las entradas desde
`LimitShopCraft.xml`. Cada ProductId debe existir previamente en uno de esos dos
DAT: los IDs inventados `20000+` se descartaron porque el cliente mostraba el
registro incompleto y podía dejar otra categoría vacía.

Classic tampoco incluye todos los registros usados por el catálogo. El paquete
importa desde Classic Aden solo los 31 ítems necesarios en las tablas de nombre,
recurso visual, ficha base, metadata adicional y estadísticas. Copiar únicamente
el nombre y el icono deja cuadros negros o textos atenuados en Special Craft.
Además, el `ItemName_Classic` original apunta a nombres rusos en 170 de los 201
ítems usados por este catálogo; el generador los reindexa hacia los nombres
ingleses de `stats/items` y agrega cadenas al final de `L2GameDataName` sin
desplazar ningún índice existente.
La verificación cubre los 201 ItemId distintos usados tanto como resultado como
ingrediente y falla si cualquiera queda incompleto.

Las capturas de prueba demostraron que este cliente 447 carga la familia
`ClassicAden`: el ProductId 4238 seguía mostrando exactamente el cupón de
Spellbook y Giran Seal del DAT Aden original, aunque el servidor ya enviaba
Magical Tablet y Adena. Por eso el bundle genera e instala ambas familias. Aden
también importa 7 ítems ausentes desde Classic y corrige sus propios índices de
nombre.

El resultado contiene exactamente:

| Pestaña | Categoría | Registros |
|---|---:|---:|
| Special Weapon | 0 | 12 |
| Spellbook | 2 | 87 |
| Accessories | 3 | 28 |
| Misc | 4 | 15 |
| Blessing | 5 | 42 |
| Event | 6 | 0 |

Los 184 nombres visibles se toman de `stats/items`, incluidos los niveles de
encantamiento. La sucesión de opciones queda desactivada en todos los registros
porque aún no existe soporte equivalente en el servidor.

`PurchaseLimitCraftCategory_Classic.dat` no necesita cambios: las seis pestañas
ya están presentes.

## Generar y verificar

El cliente fuente debe incluir los DAT Classic/ClassicAden, `NpcString` y un
`DSETUP.dll` compatible con la clave RSA de l2encdec. El script comprueba todo
antes de construir el archivo.

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\client\Build-ClassicSpecialCraftDat.ps1 `
  -ClientSystemPath 'C:\ruta\al\cliente\system'
```

El comando no modifica el cliente. Produce un paquete inseparable:

- `tools/client/output/L2GameDataName.dat`;
- ocho DAT para `Classic` y los mismos ocho para `ClassicAden`:
  `PurchaseLimitCraft`, `ItemName`, `EtcItemgrp`, `Armorgrp`, `Weapongrp`,
  `item_baseinfo`, `AdditionalItemGrp` e `ItemStatData`;
- manifiestos legibles `PurchaseLimitCraft_Classic-eu.json` y
  `PurchaseLimitCraft_ClassicAden-eu.json`, cada uno con 184 registros.

La verificación vuelve a abrir ambas familias y exige igualdad exacta con el
servidor, ausencia de registros adicionales, nombres válidos en `NpcString`,
igualdad de los 201 nombres con `stats/items`, `keep_option = 0` y presencia de
los 201 ítems en las tres tablas auxiliares de cada variante.

Para instalarlo únicamente después de revisar el resultado:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\client\Install-ClassicSpecialCraftDat.ps1 `
  -ClientSystemPath 'C:\ruta\al\cliente\system'
```

El instalador actualiza los 17 DAT juntos. Primero crea respaldos con el
mismo sufijo `.backup-yyyyMMdd-HHmmss`, comprueba el SHA-256 de cada copia y,
si una operación falla, restaura automáticamente todos los originales.

## Implementación auditable

El generador está en `L2Dn/Tools/L2Dn.ClientDat`; la estructura binaria
Shinemaker está en
`L2Dn/Tools/L2Dn.Packages/DatDefinitions/Definitions/PurchaseLimitCraftV7.cs`.
El catálogo del servidor se regenera mediante
`tools/server/Build-ClassicSpecialCraftCatalog.ps1`.

Las herramientas externas se descargan fuera de Git mediante:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\client\Get-ClientDatTools.ps1
```

Versiones fijadas:

- open-l2encdec 1.3.9, licencia MIT. ZIP Windows SHA-256:
  `3A7743C03A635DBBF7892C4F3D65D0D13D5FC04830721540AD9E430C7BD0495E`;
- fuentes open-l2encdec 1.3.9, SHA-256:
  `4B2359EE64BA97BDAE04AE37F8F939C7ED9F61C03DBE1E710E8AFAA36E209513`;
- L2ClientDat, commit `fa94655ad19fdecfc9611c52dc7c67ffd416a1a8`,
  licencia GPL-3.0-or-later. ZIP de fuentes SHA-256:
  `A841E7342AE8D6F0ADB49389ACBD6304484477E4D7F4EF45772AD67BFC66D90C`.

Los ZIP, fuentes, binarios y artefactos generados quedan bajo
`tools/client/downloads` y `tools/client/output`; ambas rutas están ignoradas por
Git.
