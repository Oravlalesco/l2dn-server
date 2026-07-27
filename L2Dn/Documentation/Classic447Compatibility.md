# Compatibilidad cliente/servidor — Classic 447 (Shinemaker)

> Estado: decisión técnica canónica para este repositorio.
>
> Última validación: 2026-07-26.

Este documento registra la investigación realizada sobre la incompatibilidad entre
cliente, `system`, datapack y quests. Debe consultarse antes de importar una quest,
un ítem, un NPC o una skill.

## Decisión

La rama objetivo del proyecto es:

| Componente | Decisión |
|---|---|
| Producto | Lineage II **Classic** |
| Protocolo | **447** |
| Chronicle usada por el código | **Shinemaker** |
| `ServerType` | `Classic` |
| `ServerListType` | `Classic` |
| Tablas activas del cliente | `*_Classic*.dat` |
| `ClassicAden` / Essence | Fuente incompatible que debe aislarse |

El número de protocolo no basta para determinar el producto:

- Classic usa protocolo 447 en esta versión del proyecto.
- Essence/Aden Seven Signs también usa protocolo 447.
- El nombre de una carpeta o archivo descargado tampoco demuestra qué rama ejecuta
  el cliente.
- La decisión se obtiene cruzando configuración del servidor, formato de paquetes y
  tablas que el ejecutable carga realmente.

La documentación oficial de L2Dn también declara que el servidor trabaja con un
cliente Classic protocolo 447:
<https://l2dn.readthedocs.io/en/latest/Client/>.

## Resultado resumido

1. Los dos clientes probados están funcionando en modalidad Classic.
2. El servidor activo está configurado y serializa paquetes como Classic.
3. El datapack está mezclado: contiene una cantidad importante de datos
   ClassicAden/Essence.
4. Que un ID exista en el XML del servidor no significa que exista en la tabla
   Classic activa del cliente.
5. Los IDs `98464` y `98474`–`98478` existen en ambos paquetes de cliente, pero
   solamente en `ItemName_ClassicAden`; por eso no aparecen en Alt+G cuando el
   ejecutable funciona como Classic.
6. Los IDs `99226`–`99228` existen solamente en las tablas Classic de ambos
   clientes, aparecen en Alt+G y pueden utilizarse correctamente.
7. Las 288 entradas de `DataPack/NewQuestData.xml` proceden del catálogo
   ClassicAden. No representan 288 quests implementadas ni 288 quests nativas
   Classic.

## Clientes analizados

### Cliente distribuido por L2Dn

```text
C:\Users\Lenovo\Documents\l2 cliente\L2-P447-EN\L2-P447
```

### Cliente descargado como “Essence EN 447”

```text
C:\Users\Lenovo\Documents\l2 cliente\Lineage II - Essence EN 447
```

El segundo cliente no incluía `system` en el archivo principal. Después se instaló
un `system` separado. Aunque la carpeta principal se llame “Essence”, el `system`
instalado se comporta como Classic.

### Huellas del entorno analizado

Los dos `L2.exe` informan versión `4.0.76.0`, pero no son el mismo binario.

| Cliente | Archivo | SHA-256 |
|---|---|---|
| L2Dn P447 | `L2.exe` | `33D76E2C466D52ECACC38D944A19B7B3A9AE6AC882E8FC4ACFF097061571264C` |
| Segundo cliente | `L2.exe` | `03E920994B733BDC10CEF262E0DB6261101A24BD9C685268B37D7F0515A3A966` |
| Ambos | `DSETUP.dll` | `2847A43777F187196ED66D00E10BE93C7E58E05DFF234E66F3204DF8C265879A` |
| L2Dn P447 | `ItemName_Classic-eu.dat` | `B68992337EF93ACCFE23A221F6F461D662EF3027933837E8727D8352D6CBA20A` |
| Segundo cliente | `ItemName_Classic-eu.dat` | `5E6A77858C01456837F2DAC2DD3A5836F95A4814351887B868CA5F041900965A` |
| L2Dn P447 | `ItemName_ClassicAden-eu.dat` | `3C4AE08DBA766F5523464220CEFEA7237FA326591A941FDEEB14254664870BF0` |
| Segundo cliente | `ItemName_ClassicAden-eu.dat` | `80B14F7CB6DFDFF29476D81195453A1F063B2E8446A28CF2BAA0FB1C18AABB3D` |

`L2.exe`, `Engine.dll` y `Core.dll` presentaron firma válida de NCSOFT.
`DSETUP.dll` es idéntico en ambos systems y no está firmado; corresponde al
componente de conexión/parche instalado para entrar al servidor privado.

En ambos `L2.ini` se observó:

```ini
UseAutoSoulShotClassic=true
```

Los dos paquetes contienen simultáneamente archivos como:

```text
ItemName_Classic-eu.dat
ItemName_ClassicAden-eu.dat
NpcName_Classic-eu.dat
NpcName_ClassicAden-eu.dat
NewQuestData_Classic-eu.dat
NewQuestData_ClassicAden-eu.dat
```

La mera presencia de las dos familias no significa que las dos estén activas. El
ejecutable selecciona una familia en tiempo de ejecución.

## Evidencia del servidor activo

La configuración dentro del contenedor `l2dn-gameserver` fue verificada en
ejecución:

```ini
# Shinemaker: 447
AllowedProtocolRevisions = 447
ServerListType = Classic
```

```json
"ServerType": "Classic"
```

Archivos de origen:

- `L2Dn/L2Dn.GameServer/Config/Server.ini`
- `L2Dn/L2Dn.GameServer/config.json`
- `Docker/config.gameserver.docker.json`

`ServerListType` no es una etiqueta cosmética. Cambia el formato de paquetes. Por
ejemplo:

- `AcquireSkillListPacket` escribe el nivel de skill como entero de 16 bits en
  Classic y como entero de 32 bits en otras ramas.
- `AbnormalStatusUpdatePacket` omite `subLevel` en Classic.

Por eso no es seguro cambiar solamente `ServerType` a `Essence`. Una migración real
debe revisar todos los paquetes y sistemas dependientes de la rama.

El servidor activo informó:

```text
Highest item id used: 100532
Loaded 14298 Etc Items
Loaded 3216 Armor Items
Loaded 2340 Weapon Items
Loaded 19854 Items in total
```

El cargador `ItemData` recorre todos los XML ubicados en `stats/items` sin filtrar
por Classic o Essence. Esto permite que el servidor cargue IDs que el cliente
Classic no conoce.

## Inventario cuantitativo de ítems

Las tablas Ver413 fueron descifradas y leídas usando el formato `ItemNameV18` que
incluye el propio repositorio.

| Cliente | Tabla | Registros del cliente | Ítems del datapack cubiertos |
|---|---:|---:|---:|
| L2Dn P447 | Classic | 16.833 | 15.365 — 77,39 % |
| L2Dn P447 | ClassicAden | 18.206 | 18.127 — 91,30 % |
| “Essence EN 447” + system separado | Classic | 16.818 | 15.365 — 77,39 % |
| “Essence EN 447” + system separado | ClassicAden | 18.194 | 18.127 — 91,30 % |

La mayor cobertura de ClassicAden demuestra el origen mezclado del datapack. No
convierte al núcleo en Essence: el núcleo, la configuración, los paquetes y la
documentación oficial siguen siendo Classic.

### IDs de control

| ID | Nombre | Classic | ClassicAden | Servidor | Uso |
|---:|---|:---:|:---:|:---:|---|
| 57 | Adena | Sí | Sí | Sí | Control común |
| 114 | Earring of Strength | Sí | Sí | Sí | Control común |
| 115 | Earring of Wisdom | Sí | Sí | Sí | Control común |
| 877 | Ring of Wisdom | Sí | Sí | Sí | Control común |
| 878 | Blue Coral Ring | Sí | Sí | Sí | Control común |
| 909 | Blue Diamond Necklace | Sí | Sí | Sí | Control común |
| 99226 | Zaken's Earring Lv. 5 | Sí | No | Sí | Detector Classic |
| 99227 | Antharas' Earring Lv. 5 | Sí | No | Sí | Detector Classic |
| 99228 | Frintezza's Necklace Lv. 5 | Sí | No | Sí | Detector Classic |
| 98464 | Herb Roots | No | Sí | Sí | Excluir de Classic |
| 98474 | Earring of Strength — Sealed | No | Sí | Sí | Excluir de Classic |
| 98475 | Earring of Wisdom — Sealed | No | Sí | Sí | Excluir de Classic |
| 98476 | Ring of Wisdom — Sealed | No | Sí | Sí | Excluir de Classic |
| 98477 | Blue Coral Ring — Sealed | No | Sí | Sí | Excluir de Classic |
| 98478 | Blue Diamond Necklace — Sealed | No | Sí | Sí | Excluir de Classic |

Los IDs `99226`–`99228` son buenos detectores técnicos, pero son joyas de jefe
nivel 5. Que sean compatibles no significa que sean recompensas apropiadas para una
quest inicial.

## Prueba reproducible en el juego

### Identificar la tabla activa

En cada cliente:

1. Abrir Alt+G.
2. Buscar `57`; debe aparecer Adena y confirma que la búsqueda funciona.
3. Buscar `99226`, `99227` y `99228`.
4. Buscar `98464`, `98474` y `98478`.

Interpretación:

| Resultado | Rama efectiva |
|---|---|
| Aparecen `57` y `99226`–`99228`; no aparecen los `984xx` | Classic |
| Aparecen `57` y los `984xx`; no aparecen `99226`–`99228` | ClassicAden/Essence |
| Aparecen los dos grupos | System modificado o mezcla de tablas |
| Solo aparece `57` | Alt+G tiene otro filtro; continuar con prueba de inventario |

Resultado obtenido en ambos clientes: aparecen `99226`–`99228` y pueden utilizarse
sin problemas; los `984xx` no aparecen. Esto confirma Classic.

### Validar cliente y servidor juntos

Con un personaje GM:

```text
//create_item 114 1
//create_item 99226 1
```

La validación se considera completa si:

- el servidor reconoce el ID;
- el ítem llega al inventario;
- aparecen nombre, additional name, descripción e icono;
- el tooltip no queda vacío;
- se puede equipar o utilizar según corresponda;
- no se desconecta ni se bloquea el cliente.

No se debe crear un ítem conocido como exclusivo de ClassicAden mientras se usa el
cliente Classic. Un `InventoryUpdate` con un ID desconocido por la tabla activa
puede mostrar `Unknown`, dejar información incompleta o provocar un fallo del
cliente.

## Origen histórico de la mezcla

La historia de Git explica por qué el datapack y el núcleo no representan una sola
distribución coherente.

### 2024-03-07 — importación del datapack

Commit:

```text
1c7946198a0b9db6411c1c3288fc5113f7d9c4c2 — Add datapack
```

La configuración importada decía:

```ini
# Seven Signs: 447
ServerListType = Essence
```

En esa importación entraron `NewQuestData.xml`, los ítems `984xx` y miles de
archivos del datapack.

### 2024-03-10 — cambio del núcleo a Classic

Commit:

```text
12e5631fd8dc499a634462bd16b50e17ee33eb60 — Fix acquire skill list
```

La configuración cambió a:

```ini
ServerListType = Classic
```

Sin embargo, estos archivos conservaron exactamente el mismo blob:

```text
DataPack/NewQuestData.xml
DataPack/stats/items/98400-98499.xml
```

### 2024-04-07 — identificación como Shinemaker

Commit:

```text
dd82fd6f28dc094d92fceb584c7f1c970e384015 — Port updates from L2J mobius
```

El comentario de protocolo pasó a:

```ini
# Shinemaker: 447
```

Conclusión histórica: el proyecto cambió su núcleo a Classic, pero quedaron
catálogos importados durante la etapa Essence.

## Implicaciones para quests

### Metadata no equivale a implementación

El repositorio original solo tenía registrado el tutorial:

```text
Q00206Tutorial
```

El README del proyecto mantiene pendiente el port de scripts, quests y datapack. Por
lo tanto:

- una entrada en `NewQuestData.xml` no significa que exista un script;
- una entrada en el XML tampoco garantiza que corresponda a Classic;
- sin registro en `QuestManager`, el cliente puede ofrecer una quest que el servidor
  no sabe aceptar;
- lógica de servidor operativa no garantiza textos, imágenes ni diálogos en el
  cliente.

### Comparación de metadata de quest del cliente

Los dos clientes analizados tienen:

| Archivo | Registros |
|---|---:|
| `NewQuestData-eu.dat` | 200 |
| `NewQuestData_Classic-eu.dat` | 2 |
| `NewQuestData_ClassicAden-eu.dat` | 288 |
| `DataPack/NewQuestData.xml` del servidor | 288 |

La igualdad de 288 registros demuestra que el XML del servidor fue construido desde
la rama ClassicAden.

Las quests `10071`–`10079`, incluyendo `Ore from the Strip Mine`, están en ese
catálogo ClassicAden. Los scripts desarrollados localmente consiguen ejecutar su
lógica en el servidor Classic, pero eso es un piloto de compatibilidad, no una
implementación nativa Classic.

### Por qué el panel de quest queda sin textos

Los paquetes `ExQuest*` transmiten estado, objetivos, aceptación, teleport y
finalización. Los títulos, descripciones, imágenes y parte del diálogo pertenecen a
los DAT del cliente.

En modalidad Classic:

```text
NewQuestData_Classic-eu.dat
QuestName_Classic-eu.dat
```

son las tablas relevantes. La metadata de `10071`–`10079` está en:

```text
NewQuestData_ClassicAden-eu.dat
```

Por eso es posible que:

- funcione el cuadro de teleport;
- el servidor reciba Accept, Teleport y Complete;
- la quest avance y entregue recompensas;
- pero el panel pequeño no muestre textos o conversaciones.

No se corrige cambiando solamente el script C#: el contenido textual no está en la
tabla Classic activa.

### Estado de la cadena enana

`10071`–`10079` debe conservarse como piloto técnico y mantenerse aislada hasta
tomar una decisión explícita:

1. archivarla como referencia ClassicAden;
2. convertirla en contenido custom Classic con UI/HTML propios;
3. reemplazarla por quests verificadas contra una fuente Classic 447 exacta.

No se debe ampliar la cadena ni copiarla a otras razas como si fuera contenido
Classic nativo.

Adaptaciones ya realizadas para evitar incompatibilidades:

| Quest | Dato ClassicAden original | Adaptación Classic |
|---|---|---|
| 10074 | Goal con `98464` Herb Roots | Kill count, sin crear `98464` |
| 10076 | Recompensas `98474`–`98478` | IDs comunes `114`, `115`, `877`, `878`, `909` |

No eliminar estas implementaciones sin conservar antes una rama o copia de
referencia: contienen trabajo útil sobre el flujo de paquetes y el motor de quests.

## Regla de aceptación para contenido nuevo

### Ítems

Un ítem solo se considera compatible con Classic cuando pasa todas las capas:

1. existe en el XML del servidor;
2. existe en `ItemName_Classic`;
3. existe en el grupo visual Classic correspondiente:
   `EtcItemgrp_Classic`, `Armorgrp_Classic` o `Weapongrp_Classic`;
4. Alt+G lo encuentra;
5. `//create_item <id> 1` lo entrega sin errores;
6. nombre, icono, tooltip y acción son correctos.

### NPC

1. existe en stats y, si corresponde, en spawns del servidor;
2. existe en `NpcName_Classic`;
3. existe en `Npcgrp_Classic`;
4. modelo, nombre, interacción y HTML funcionan en el juego.

### Skills

1. existen definición, niveles y efectos en el servidor;
2. existen nombre y grupo en las tablas `Skill*_Classic`;
3. el formato del paquete coincide con Classic;
4. aprendizaje, uso, icono y estado anormal funcionan.

### Quests

1. confirmar origen Classic 447; no deducirlo por el ID;
2. validar quest ID en las tablas Classic activas;
3. validar todos los NPC, ítems, skills y teleports dependientes;
4. crear o portar el script;
5. registrar el script en `Scripts.RegisterQuests()`;
6. probar Accept, progreso, DONE, Complete y persistencia;
7. probar login/relogin en cada estado;
8. verificar textos y UI desde el cliente;
9. si requiere HTML o un System modificado, declararla **custom**, no nativa.

## Políticas de desarrollo

1. **Rama canónica:** Classic 447/Shinemaker.
2. **No confiar en el protocolo 447 como identificador de producto.**
3. **No confiar en el nombre del directorio del cliente.**
4. **No confiar solo en `DataPack/NewQuestData.xml`.**
5. **No confiar solo en que el servidor cargue un ID.**
6. Usar las tablas `*_Classic` como fuente de verdad del cliente activo.
7. Tratar `*_ClassicAden` como inventario de otra rama.
8. Clasificar explícitamente cualquier importación externa antes de implementarla.
9. Mantener las adaptaciones Classic separadas de las fuentes Essence.
10. No cambiar el servidor a Essence sin una migración integral de paquetes,
    clases, skills, items, NPC, sistemas y quests.

## Problema de servidor independiente del cliente

Durante la ejecución de las quests se observó:

```text
System.FormatException: The input string '' was not in a correct format
```

Origen conocido:

```text
QuestState.set(...)
int.Parse(old ?? string.Empty)
```

Cuando `COND_VAR` todavía no existe, `old` es `null` y se intenta convertir una
cadena vacía. La excepción se registra, se usa el valor anterior `0` y la quest
continúa.

Esto es un bug/ruido del servidor, no una prueba de incompatibilidad del cliente.
Debe corregirse independientemente y validarse con tests de estado inicial.

## Herramienta de inventario propuesta

Para evitar repetir una auditoría manual, la futura herramienta de compatibilidad
debería:

1. recibir la ruta de uno o más clientes;
2. descifrar DAT Ver413;
3. leer por separado tablas Main, Classic y ClassicAden;
4. leer los IDs del datapack;
5. producir JSON/CSV y un panel HTML;
6. clasificar cada registro como:
   - común;
   - solo Classic;
   - solo ClassicAden;
   - solo servidor;
   - cliente sin servidor;
   - sin grupo visual;
7. permitir buscar por ID o nombre;
8. mostrar nombre, descripción, icono, tipo y rama;
9. generar listas seguras para quests y recompensas;
10. registrar la fecha, hash del ejecutable y hash de cada DAT analizado.

El panel sería una ayuda de inventario y análisis. No puede “activar” en tiempo de
ejecución una tabla que el ejecutable no carga; para eso se requeriría modificar el
System o utilizar el ejecutable correcto.

## Checklist antes de continuar una quest

```text
[ ] La fuente es Classic 447.
[ ] El quest ID existe en la tabla Classic activa o se declara custom.
[ ] Todos los NPC existen en NpcName/Npcgrp Classic.
[ ] Todos los ítems existen en ItemName y su grp Classic.
[ ] Todas las skills existen en las tablas Classic.
[ ] Los teleports existen en servidor y cliente.
[ ] El script está registrado.
[ ] Alt+G reconoce los IDs relevantes.
[ ] Accept/Teleport/Progress/DONE/Complete funcionan.
[ ] Relog conserva el estado.
[ ] Textos, iconos y paneles se ven correctamente.
[ ] No se utilizaron IDs solo porque estaban en el XML del servidor.
```
