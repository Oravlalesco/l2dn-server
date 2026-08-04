# Parche de cliente para Special Craft Classic 447

## Qué archivo controla la ventana

Special Craft no se define en `LCoinShopProduct_Classic-eu.dat`. La interfaz usa:

- `system/eu/PurchaseLimitCraft_Classic-eu.dat`: productos, resultados,
  probabilidades, categorías y límites visuales;
- `system/PurchaseLimitCraftCategory_Classic.dat`: las pestañas;
- `system/eu/NpcString_Classic-eu.dat`: textos referenciados por
  `category_sub`.

Los ingredientes pertenecen al servidor. `LimitShopCraft.xml` los envía por
protocolo, con un máximo de cinco por receta. El DAT y el XML deben coincidir en
ProductId, categoría, resultado, cantidad, probabilidad, encantamiento y rango
de nivel.

## Diagnóstico y solución

El DAT Classic original contiene 90 registros para `shop_index = 4`, pero no
representa el nuevo catálogo del servidor. El generador conserva metadatos
compatibles de los DAT Classic y ClassicAden y sintetiza las entradas faltantes
desde `LimitShopCraft.xml`. No se depende de que exista un registro donante para
cada ProductId nuevo.

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

El comando no modifica el cliente. Produce:

- `tools/client/output/PurchaseLimitCraft_Classic-eu.dat`;
- `tools/client/output/PurchaseLimitCraft_Classic-eu.json`, manifiesto legible
  de los 184 registros.

La verificación vuelve a abrir el DAT generado y exige igualdad exacta con el
servidor, ausencia de registros adicionales, nombres válidos en `NpcString` y
`keep_option = 0`.

Para instalarlo únicamente después de revisar el resultado:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\client\Install-ClassicSpecialCraftDat.ps1 `
  -ClientSystemPath 'C:\ruta\al\cliente\system'
```

El instalador crea primero un respaldo con sufijo
`.backup-yyyyMMdd-HHmmss` y comprueba el SHA-256 de la copia.

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
