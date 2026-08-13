# Evaluación: geodata y pathnodes

Fecha: 2026-08-12  
Alcance: incorporar navegación y colisión geoespacial al GameServer actual, sin cambiar aún su código de juego.

## Conclusión ejecutiva

El servidor **ya contiene una implementación de geodata completa y dos motores de pathfinding**. No es un proyecto que haya que desarrollar desde cero: el bloqueo actual es que el DataPack sólo trae `geodata/Readme.txt`, sin regiones `.l2j` ni `pathnode/`. Con la configuración actual `PathFinding = 2`, el servidor arranca igualmente, pero en zonas sin archivo las consultas se degradan a terreno libre. Eso permite una integración gradual, pero también puede ocultar que no se ha cargado ninguna geodata.

Recomendación: empezar con **geodata + CellPathFinding (modo 2)** y no con pathnodes. Añadir pathnodes (modo 1) sólo si las métricas prueban que el coste del cálculo por celda es excesivo. Los pathnodes son derivados estáticos y aumentan los problemas de compatibilidad y mantenimiento; no mejoran la exactitud de la geodata.

## Estado comprobado del repositorio

| Elemento | Estado | Evidencia |
|---|---|---|
| Carga de geodata | Implementado | `GeoEngine` busca `DataPack/geodata/{x}_{y}.l2j` y también acepta `.l2j.gz`. |
| Formato y geometría | Implementado | Celdas de 16 unidades, bloques flat/complex/multilayer y máscara NSWE. |
| Altura, movimiento y LOS | Implementado | `GeoEngine` ofrece altura, `getValidLocation`, `canMoveToTarget` y `canSeeTarget`; los dos últimos incorporan puertas y fences. |
| Movimiento de actores | Integrado | `Creature` corrige el destino y pide una ruta cuando el trayecto directo queda bloqueado. |
| Pathfinding por celdas | Implementado | `CellPathFinding`: búsqueda A* sobre las celdas de geodata, buffers configurables y postfiltro LOS. Modo `2`. |
| Pathfinding por nodos | Implementado | `GeoPathFinding`: lee `DataPack/pathnode/{x}_{y}.pn`. Modo `1`. |
| Herramientas GM | Disponibles | `admin_geo_*`, editor NSWE, guardado de región y `admin_path_find`. |
| Observabilidad | Parcialmente lista | Histograma OTEL `l2dn.pathfinding.duration` y métricas de consultas geo de NPC. |
| Datos y pruebas | Ausentes | La carpeta sólo contiene el README; no hay tests de geodata/pathfinding. |

El `config.json` puede descargar/actualizar geodata desde una lista remota, pero ambos flags están en `false`. No descarga pathnodes. `GeoEngine.ini` ya selecciona el modo 2 y define buffers, pesos y postfiltro.

## Qué significan los dos modos

| Modo | Entrada | Ventaja | Coste/riesgo | Uso aconsejado |
|---|---|---|---|---|
| 1: `GeoPathFinding` | Geodata `.l2j` + pathnodes `.pn` generados para **esa misma** geodata | Rutas largas más baratas en CPU | Nodo grueso (128 unidades), rutas menos fieles; artefacto adicional que se desincroniza cuando se edita la geo; búsqueda limitada a 550 expansiones | Sólo si la carga medida obliga a ello. |
| 2: `CellPathFinding` | Sólo geodata `.l2j` | Mayor fidelidad; no requiere generar ni versionar `.pn`; adapta automáticamente correcciones NSWE | Más CPU y presión de buffers en picos | Primera implementación y servidores pequeños/medianos. |

La geodata es la fuente de verdad: describe altura/capas y los permisos cardinales NSWE de cada celda. Los pathnodes no sustituyen ni arreglan una geo errónea: son una representación más rala de conectividad para acelerar la planificación. La literatura sobre navegación precomputada en mundos MMO confirma esa contrapartida: las tablas deben preservar conectividad entre tiles y con tiles vecinos, por lo que una alteración local obliga a regenerar el dato derivado.[^mmo]

## Problemas reales, causa y tratamiento

| Síntoma | Causa habitual | Resolución operativa |
|---|---|---|
| Caminar/disparar a través de muros o suelo | Región ausente, formato incompatible, NSWE abierto o capa Z equivocada | Validar la región cargada y la altura; revisar la celda con editor GM; corregir la geodata y conservar el parche versionado. |
| Jugador/NPC bloqueado ante una pared que visualmente no existe | NSWE demasiado cerrado o colisión visual y geodata no coinciden | Corregir sólo la arista NSWE necesaria y comprobar ambos sentidos; probar el trayecto en ambos sentidos. |
| NPC "trepa", se teletransporta o queda en un bucle | Capas verticales incorrectas, destino/spawn fuera de la capa, waypoint a través de una arista inválida | Revisar Z de spawn/teleport y capas de la geo; añadir pruebas del caso; para jefes grandes comprobar radio/altura y ruta real. |
| NPC no llega al objetivo aunque hay ruta | El pathnode más cercano no es alcanzable (limitación explícita del modo 1), límite de 550 nodos, ruta estática obsoleta o buffer agotado | Preferir modo 2; si se usa modo 1, regenerar `.pn` tras cada edición y registrar fallos/longitud. Ajustar buffers sólo tras medir. |
| NPC intenta cruzar una puerta cerrada y se queda | Las puertas/fences son dinámicas; las rutas precalculadas no conocen el estado que cambiará | Revalidar cada segmento antes de mover; invalidar/recalcular ruta al cambio de puerta. Es un hueco que conviene instrumentar antes del despliegue masivo. |
| Rutas zigzagueantes o costosas | Pesos diagonales/obstáculos, grilla de 16 unidades y simplificación | Usar el postfiltro existente, medir primero y ajustar `Low/Medium/HighWeight`, diagonal y `MaxPostfilterPasses` con una batería fija. |
| Lag o picos al pelear con muchos NPC | A* por celda, demasiadas solicitudes simultáneas o buffers temporales | Medir p95/p99, overflow y GC; limitar replanificación, reutilizar buffers y activar pathnodes sólo si los datos lo justifican. |
| Correcciones manuales interminables | Se usa un paquete geo de otra crónica/cliente, no hay casos de prueba ni ownership de parches | Fijar un único proveedor/versión, mantener una capa de parches propia y aceptar sólo fixes reproducibles con coordenadas y prueba de regresión. |

La geodata histórica de L2J se define precisamente como mapeo geográfico y pathnodes orientados a IA.[^l2jgeo] El formato/dataset debe ser compatible con este lector y con el cliente/protocolo en uso; "geodata de la misma crónica" por sí sola no es garantía suficiente. Importar archivos de otro fork sin una prueba piloto es el principal generador de horas de ajuste manual.

## Riesgos específicos detectados en este código

1. **Falso positivo de activación.** `PathFinding=2` está por defecto, pero sin archivos el motor devuelve terreno libre en gran parte de las consultas. El arranque sólo informa cuántas regiones cargó; debe convertirse en un chequeo de salud con mínimo esperado y lista de regiones faltantes.
2. **Pathnodes sin ciclo de generación.** El lector de `.pn` existe, pero no hay archivos ni generador en el repositorio. Importarlos desde otra geo puede producir rutas que atraviesan cambios manuales.
3. **Dinámica de puertas/fences.** La validación directa sí las consulta, pero el buscador por celdas planifica sobre geo estática. Hay que comprobar en pruebas que un waypoint invalidado fuerza replanning y no reintentos indefinidos.
4. **Correcciones en caliente no son persistencia automática.** El editor modifica memoria y `admin_geosave` escribe una región completa a `GeoEditPath` (por defecto `DataPack/saves`). Hay que revisar/difundir ese archivo y reiniciar para tratarlo como versión liberada.
5. **Cobertura automatizada nula.** No existen tests de conversión de coordenadas, NSWE, multicapas, LOS o rutas; sin ellos cada arreglo puede reabrir otra zona.
6. **Cambios de movimiento ya son sensibles.** Hay excepciones deliberadas para vehículos, caídas, objetivos lejanos, jugadores atacando y monstruos en desnivel. La certificación debe incluirlas para evitar que una buena geo vuelva injugables esos casos especiales.

## Plan de implantación propuesto

### Fase 0 — decidir y preparar (1–3 días)

1. Identificar con exactitud cliente/protocolo, mapa disponible y licencia/origen del dataset. No mezclar `.l2j` y `.pn` de fuentes/versiones distintas.
2. Crear una rama/artefacto de datos separado y fijar manifiesto: hash de cada región, versión del proveedor, fecha y parches locales. Los binarios grandes no deberían entrar al repositorio de código sin una política de LFS/artefactos.
3. Definir presupuesto: RAM de arranque, p95/p99 de `l2dn.pathfinding.duration`, máximo de fallos, rutas sin destino y tiempo de recuperación de una ruta bloqueada.

### Fase 1 — piloto con CellPathFinding (1–2 semanas)

1. Cargar un conjunto pequeño pero representativo de regiones: ciudad, exterior con desnivel, dungeon multicapa, zona de raid y un área con puertas/fences.
2. Mantener `PathFinding=2`; no instalar pathnodes. Dejar `DebugPath=false` en pruebas de carga y usarlo sólo con GM en un entorno de QA.
3. Añadir health check al inicio: regiones cargadas/esperadas, formato inválido, tiempo y memoria. Fallar el despliegue de QA si el número es cero o inferior al manifiesto.
4. Ejecutar una matriz manual reproducible: caminar, click lejano, auto-hunt, melee/ranged a través de muro, fear/knockback/blink, follow de summon, puerta abierta/cerrada, caída, teleports y todos los bosses grandes del piloto.
5. Registrar cada defecto como `coordenadas origen/destino/Z + región + dataset hash + vídeo/captura + resultado esperado`; corregir primero el dataset, no mediante excepciones de IA.

### Fase 2 — calidad y automatización (1–2 semanas)

1. Añadir tests de oro: casos de `canSeeTarget`, `canMoveToTarget`, `getValidLocation` y rutas con expected waypoints/resultado. Incluir bordes de región, diagonales y multicapas.
2. Crear un comando/administración de QA que exporte estadísticas de rutas: éxito/fallo, duración, overflow de buffers, replanificación y región. El motor ya expone parte de esos contadores.
3. Establecer revisión de patches: una corrección NSWE requiere prueba ida/vuelta, LOS y ruta NPC; guardarla en `saves/`, revisarla y promoverla como artefacto de geodata versionado.

### Fase 3 — ampliar y decidir pathnodes

1. Ampliar región por región siguiendo uso real y no todo el mundo a ciegas.
2. Hacer pruebas de carga con cantidad realista de NPC activos, auto-play y asedios. Evaluar GC/RAM además de CPU.
3. Sólo si el p99 o la saturación de buffers incumple presupuesto, generar `.pn` desde **el mismo build final** de geodata y comparar modo 1 frente a modo 2 en la misma batería. Conservar modo 2 como referencia de precisión y rollback.

## Criterios de salida y decisión

Continuar a producción sólo si se cumple todo lo siguiente:

- Todas las regiones del alcance cargan y coinciden con el manifiesto.
- No hay LOS/movimiento atravesando geometría en los casos críticos; los fallos conocidos quedan catalogados por región.
- Los NPC encuentran ruta o abandonan/reintentan de forma finita; ninguno queda ciclando frente a puertas, muros o desniveles.
- El p99 de pathfinding, la tasa de fallos y los overflows de buffer permanecen dentro del presupuesto bajo carga.
- Todo cambio manual queda trazable, reproducible y protegido por una prueba; no se sobrescribe el dataset base directamente.
- Existe rollback instantáneo al dataset/manifiesto anterior.

## Fuentes externas

[^l2jgeo]: [L2J Geodata, SourceForge](https://sourceforge.net/projects/l2j-geodata/), descripción del proyecto como geodata y pathnodes para emuladores L2J.
[^mmo]: [Precomputed Pathfinding for Large and Detailed Worlds on MMO Servers, Game AI Pro](https://www.gameaipro.com/GameAIPro/GameAIPro_Chapter20_Precomputed_Pathfinding_for_Large_and_Detailed_Worlds_on_MMO_Servers.pdf), diseño y coste de conectividad/tablas precomputadas entre tiles.

