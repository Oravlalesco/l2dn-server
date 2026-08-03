# L2Dn Server Project

**L2Dn Server** is an open-source server emulator fully written in .NET for the famous Korean MMORPG Lineage 2.

This page only gives very basic information, 
for the detailed information about building, developing and using
L2Dn Server, please visit [the documentation](https://l2dn.readthedocs.io/).

The project code initially is the port of the L2J server written in Java. 

#### Milestones

- [x] Make code compile
- [x] Make the server able to run without crashes
- [ ] Make main functionality work, see [Test Plan](https://l2dn.readthedocs.io/en/latest/TestPlan/) 
  * Teleports, skills, fighting are working
- [ ] Port scripts (quests, events, etc)
  * Most of the handlers have been ported, including GM commands 
  * Tutorial quest for Dwarf fighters has been ported as a quest example 
- [ ] Datapack and Geodata

#### Client

The development branch is for protocol 447. 
The instruction how to set up the client is [here](https://l2dn.readthedocs.io/en/latest/Client/).

#### Other Documentation Pages
- [Changelog](https://l2dn.readthedocs.io/en/latest/Changelog/)
- [Test Plan](https://l2dn.readthedocs.io/en/latest/TestPlan/)
- [Compatibilidad cliente/servidor Classic 447](L2Dn/Documentation/Classic447Compatibility.md)
- [Catálogo Special Craft Classic 447](L2Dn/Documentation/ClassicSpecialCraftCatalog.md)
- [Parche de cliente Special Craft Classic 447](L2Dn/Documentation/ClassicSpecialCraftClient.md)
- [Tiendas NPC](L2Dn/Documentation/NpcShop.md)

---

## Docker y desarrollo local

El entorno Docker vive en la carpeta [`Docker/`](Docker/). Ahí están `docker-compose.yml`, los overrides de desarrollo y los scripts `dev-restart.ps1` / `dev-publish.ps1`.

### Requisitos

- **Solo Docker** (Docker Desktop o Engine + Compose). No hace falta instalar .NET SDK en el PC: la compilación se hace dentro de contenedores (`dev-publish.ps1` usa `mcr.microsoft.com/dotnet/sdk:9.0-alpine`).

Para asistentes de IA en este repo: ver [`.cursor/rules/l2dn-docker.mdc`](.cursor/rules/l2dn-docker.mdc).

### Arranque inicial (primera vez)

**Producción / imágenes compiladas:**

```powershell
cd Docker
docker compose up -d --build
```

La primera ejecución del Game Server aplica migraciones (`-UpdateDatabase` en la imagen).

**Arranque para una red local:**

```powershell
cd Docker
.\start-lan.ps1
```

El script publica `192.168.0.100` por defecto. Para usar otra dirección:

```powershell
.\start-lan.ps1 -PublishAddress 192.168.1.50
```

PostgreSQL permanece accesible solo dentro de Docker. Para exponer también el
puerto `5432` durante una tarea administrativa:

```powershell
.\start-lan.ps1 -ExposeDatabase
```

**Desarrollo habitual (recomendado):** DataPack y Config montados desde el repo:

```powershell
cd Docker
.\dev-up.ps1
```

Si aún no existen imágenes, usa `.\dev-up.ps1 -Build` o ejecuta antes `docker compose build`.

Puertos por defecto:

| Servicio     | Puerto |
|-------------|--------|
| Auth Server | 2106   |
| Game Server | 7777   |

### Modo desarrollo (DataPack y Config sin rebuild)

Para trabajar con XML del datapack, HTML, spawns, buylists, `Config/*.ini`, etc., usa el compose de desarrollo. Monta `L2Dn/L2Dn.GameServer/DataPack` y `Config` desde tu copia del repo; **no hace falta** `docker build` tras cada cambio.

**Activar el modo dev** (una vez):

```powershell
cd Docker
.\dev-up.ps1
```

**Tras editar datapack o INI**, reinicia solo el Game Server:

```powershell
cd Docker
.\dev-restart.ps1
```

Equivalente manual:

```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml restart l2dn-gameserver
```

En modo dev el contenedor **no** ejecuta migraciones en cada arranque (arranque más rápido). El Auth Server sigue usando la imagen compilada.

### Cambios en código C#

**Game Server** (por ejemplo `SchemeBuffer.cs`):

```powershell
cd Docker
.\dev-publish.ps1
.\dev-restart.ps1 -Code
```

`dev-publish.ps1` ejecuta `dotnet publish` **dentro de Docker** y deja los binarios en `Docker/publish/gameserver` con `config.gameserver.docker.json` (auth `l2dn-authserver`, DB `db`).

**Auth Server:**

```powershell
.\dev-publish-auth.ps1
docker compose -f docker-compose.yml -f docker-compose.dev-auth.yml up -d --force-recreate l2dn-authserver
```

O `docker compose build l2dn-authserver && docker compose up -d l2dn-authserver`.

El orden de los `-f` importa con `-Code`: `dev-code` antes que `dev.yml`, para que DataPack/Config del repo sigan montados encima del publish.

### Migraciones de base de datos

Cuando cambien entidades EF o haga falta actualizar el esquema:

```powershell
cd Docker
docker compose -f docker-compose.yml -f docker-compose.dev.yml run --rm l2dn-gameserver /App/L2Dn.GameServer -UpdateDatabase
```

O con el script:

```powershell
.\dev-restart.ps1 -Migrate
```

(En modo dev normal, `dev-restart.ps1` sin `-Migrate` no migra.)

### Reconstruir imágenes (producción o cambios grandes)

Cuando quieras una imagen nueva (Auth + Game, sin volúmenes de dev):

```powershell
cd Docker
docker compose build
docker compose up -d
```

### Logging (consola, archivos y paquetes de red)

El Game Server y el Auth Server usan **NLog**. El nivel mínimo se define en `config.json` (sección `Logging`), no en `General.ini`.

| Archivo | Cuándo aplica |
|---------|----------------|
| `L2Dn/L2Dn.GameServer/config.json` | Imagen Docker de producción (`docker compose up --build`) |
| `Docker/config.gameserver.docker.json` | Publish de desarrollo (`dev-publish.ps1` → copia a `publish/gameserver/config.json`) |
| `L2Dn/L2Dn.AuthServer/config.json` | Auth Server (imagen y publish auth) |

#### Qué aparece en consola con nivel `Trace`

Con `Logging.Console.LogLevel` y `Logging.File.LogLevel` en **`Trace`** (valor por defecto actual en el repo), la consola muestra cada paquete cliente/servidor, por ejemplo:

```text
[03:52:55.610][Trace] S(2)  Sending packet SocialActionPacket (27), length: 15
```

Eso sale del protocolo (`L2Dn.Protocol`, nivel `Trace` en `Connection.cs` y handlers de paquetes). **No** depende de `Developer = True` ni de `DebugServerPackets` en `Config/General.ini`.

En `General.ini` existen `DebugClientPackets`, `DebugServerPackets` y `ExcludedPacketList` (incluye `SocialAction`), pensados para depuración fina de paquetes, pero **hoy no están enlazados** al logger del protocolo; el filtro efectivo es solo el `LogLevel` de NLog en `config.json`.

#### Por qué importa en producción (futuro)

En un servidor en vivo conviene **no** dejar `Trace` como nivel global:

- **Ruido y volumen**: paquetes como `SocialAction`, `MoveToLocation` o `UserInfo` se envían muy a menudo; el log se vuelve ilegible.
- **Rendimiento**: cada línea implica formatear cadenas y escribir en consola/archivo en caliente.
- **Disco**: con `Logging.File.Enabled: true` y nivel `Trace`, la carpeta `logs/` crece rápido (hay rotación diaria, pero el tráfico sigue siendo alto).
- **Docker / orquestación**: stdout acumulado por el driver de logs del contenedor.

No suele provocar un “memory leak” clásico, pero sí coste de I/O y almacenamiento que en producción debe evitarse.

**Recomendación para producción** (cuando despliegues en serio, revisar y ajustar):

```json
"Logging": {
  "File": {
    "Enabled": true,
    "LogLevel": "Info"
  },
  "Console": {
    "Enabled": true,
    "LogLevel": "Warn"
  }
}
```

- **`Info` en archivo**: eventos del servidor sin volcado de cada paquete.
- **`Warn` en consola**: solo avisos y errores en `docker compose logs` (o subir consola a `Info` si necesitas más detalle operativo).
- La base de datos EF ya usa `"LogLevel": "Warn"` en la misma `config.json`; conviene alinear el resto del logging con esa idea.

Tras cambiar `config.json`, hay que **reconstruir la imagen** o volver a publicar y reiniciar el contenedor para que cargue la nueva configuración.

#### Desarrollo y depuración de red

Para **debug local** (investigar protocolo, paquetes desconocidos, estados de sesión), el nivel actual del repo es coherente:

```json
"Logging": {
  "File": {
    "Enabled": true,
    "LogLevel": "Trace"
  },
  "Console": {
    "Enabled": true,
    "LogLevel": "Trace"
  }
}
```

- **`Trace` en consola**: ves envío/recepción de paquetes en tiempo real (útil con el cliente conectado).
- **`Trace` en archivo**: historial completo en `logs/` dentro del contenedor o del directorio de publish.

Variantes útiles en dev:

| Objetivo | Ajuste sugerido |
|----------|-----------------|
| Menos ruido en terminal, historial completo en disco | `Console`: `Info` o `Warn`, `File`: `Trace` |
| Solo terminal, sin archivos | `File.Enabled`: `false`, `Console.LogLevel`: `Trace` |
| Depuración mínima (rates, spawns, errores) | Ambos en `Info` |

Cuando `DebugServerPackets` / `ExcludedPacketList` de `General.ini` estén implementados en el protocolo, podrán complementar este esquema (filtrar tipos concretos con `Trace` global); hasta entonces, subir o bajar `LogLevel` en `config.json` es la palanca disponible.

### Resumen rápido

| Qué cambiaste              | Qué hacer                                      |
|--------------------------|------------------------------------------------|
| XML/HTML/spawns/buylists | `.\dev-restart.ps1` (con modo dev activado)    |
| `Config/*.ini`           | Igual                                          |
| C# (GameServer)          | `.\dev-publish.ps1` + `.\dev-restart.ps1 -Code` |
| Esquema de base de datos | `run ... -UpdateDatabase` o `-Migrate`         |
| Dockerfile / dependencias| `docker compose up -d --build`                 |
| `config.json` → `Logging` | Rebuild o `dev-publish` + restart; prod: preferir `Info`/`Warn` |

### Archivos útiles en `Docker/`

| Archivo | Uso |
|---------|-----|
| `docker-compose.yml` | Stack base (postgres, auth, game) |
| `docker-compose.dev.yml` | Volúmenes DataPack + Config; arranque sin migración automática |
| `docker-compose.dev-code.yml` | Monta `publish/gameserver` (binarios del host) |
| `dev-up.ps1` | Levanta stack en modo dev (`-Build`, `-Code` opcionales) |
| `dev-restart.ps1` | Reinicio rápido; `-Code` y/o `-Migrate` |
| `dev-publish.ps1` | Publica GameServer vía contenedor SDK (sin .NET local) |
| `dev-publish-auth.ps1` | Publica AuthServer vía contenedor SDK |
| `docker-compose.dev-auth.yml` | Monta `publish/authserver` en Auth |
| `config.gameserver.docker.json` | `config.json` para publish del GameServer (incl. `Logging`; ver sección anterior) |
| [`.cursor/rules/l2dn-docker.mdc`](.cursor/rules/l2dn-docker.mdc) | Reglas para Cursor / agentes IA |
