# Parche de cliente para Special Craft Classic 447

## Archivo correcto

Special Craft no se define en `LCoinShopProduct_Classic-eu.dat`. Las tablas que
controlan esta ventana son:

- `system/eu/PurchaseLimitCraft_Classic-eu.dat`: productos, resultados,
  probabilidades, categorías y límites visuales;
- `system/PurchaseLimitCraftCategory_Classic.dat`: definición de las pestañas;
- `system/eu/NpcString_Classic-eu.dat`: textos referenciados por `category_sub`.

Los ingredientes no se guardan en el DAT Shinemaker. El servidor los envía desde
`LimitShopCraft.xml`, con un máximo de cinco ingredientes que la interfaz puede
mostrar. Por eso el DAT y el XML deben coincidir en ProductId, categoría,
resultados, probabilidades, encantamientos y niveles, mientras el XML sigue
siendo la fuente de verdad para los costos.

## Diagnóstico del cliente original

El `PurchaseLimitCraft_Classic-eu.dat` original contiene 90 registros de
`shop_index=4`:

| Categoría | Registros originales |
|---:|---:|
| 0 | 25 |
| 3 | 30 |
| 4 | 12 |
| 5 | 23 |

Ninguno de sus 90 ProductId existe en los 41 productos activos de
`LimitShopCraft.xml`. Esa es la causa de los productos no disponibles y de la
pestaña Spellbook vacía.

Los 41 registros del servidor sí existen exactamente en
`PurchaseLimitCraft_ClassicAden-eu.dat`, incluido el contrato completo de
resultados y probabilidades. El generador selecciona esos registros, valida
cada campo contra el XML y crea el DAT Classic con esta distribución:

| Pestaña | Categoría | Registros |
|---|---:|---:|
| Special Weapon | 0 | 0 |
| Spellbook | 2 | 4 |
| Accessories | 3 | 14 |
| Misc | 4 | 11 |
| Blessing | 5 | 12 |
| Event | 6 | 0 |

Special Weapon y Event permanecen vacías por decisión del catálogo del
servidor. `PurchaseLimitCraftCategory_Classic.dat` no necesita cambios: las seis
pestañas ya existen.

## Generar y verificar

El cliente debe incluir un `DSETUP.dll` compatible con la clave RSA de
l2encdec. El script lo comprueba antes de construir el archivo.

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\client\Build-ClassicSpecialCraftDat.ps1 `
  -ClientSystemPath 'C:\ruta\al\cliente\system'
```

El comando no modifica el cliente. Produce:

- `tools/client/output/PurchaseLimitCraft_Classic-eu.dat`;
- `tools/client/output/PurchaseLimitCraft_Classic-eu.json`, manifiesto legible
  de los 41 registros.

Para instalarlo después de revisar el resultado:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\client\Install-ClassicSpecialCraftDat.ps1 `
  -ClientSystemPath 'C:\ruta\al\cliente\system'
```

El instalador crea primero un respaldo con sufijo
`.backup-yyyyMMdd-HHmmss` y verifica el SHA-256 de la copia.

## Herramientas y fuentes auditadas

El generador propio está en `L2Dn/Tools/L2Dn.ClientDat` y la estructura binaria
Shinemaker está en
`L2Dn/Tools/L2Dn.Packages/DatDefinitions/Definitions/PurchaseLimitCraftV7.cs`.

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

Los ZIP, fuentes extraídas, binarios y artefactos generados quedan bajo
`tools/client/downloads` y `tools/client/output`; ambas rutas están ignoradas
por Git.
