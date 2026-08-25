---
name: l2dn-docker-dev
description: >-
  Runbook completo del flujo de desarrollo Docker para L2Dn Server.
  Usar cuando el usuario necesite compilar C#, reiniciar servicios, aplicar
  migraciones EF, publicar GameServer o AuthServer, o resolver errores de
  Docker Compose en este proyecto.
---

# L2Dn — Flujo de desarrollo con Docker

Todo el desarrollo se hace **sin .NET SDK en el host**. Solo necesitas Docker Desktop (o Engine + Compose). Los scripts `.ps1` siempre se ejecutan desde el directorio `Docker/`.

---

## Mapa rápido de decisión

| ¿Qué cambié? | ¿Qué ejecuto? |
|---|---|
| XML / HTML / spawns / buylists / `Config/*.ini` | `.\dev-restart.ps1` |
| C# del GameServer | `.\dev-publish.ps1` → `.\dev-restart.ps1 -Code` |
| C# del AuthServer | `.\dev-publish-auth.ps1` → compose up auth |
| Modelo EF (entidades/DbContext) | `.\dev-restart.ps1 -Migrate` |
| Dockerfile / dependencias NuGet | `docker compose up -d --build` |
| `config.json` → sección `Logging` | rebuild o dev-publish + restart |

---

## Paso 0 — Activar modo dev (solo primera vez o tras reset)

```powershell
cd Docker
.\dev-up.ps1
# Si aún no hay imágenes:
.\dev-up.ps1 -Build
```

Monta `L2Dn/L2Dn.GameServer/DataPack` → `/App/DataPack` y `Config` → `/App/Config` en el contenedor. El AuthServer sigue usando imagen compilada.

---

## Modo 1 — DataPack / Config (sin rebuild)

```powershell
cd Docker
.\dev-restart.ps1
```

Equivalente manual:
```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml restart l2dn-gameserver
```

En modo dev el contenedor **no** ejecuta migraciones en cada arranque (más rápido).

---

## Modo 2 — Cambios en C# del GameServer

```powershell
cd Docker
.\dev-publish.ps1          # publica dentro de contenedor SDK → Docker/publish/gameserver/
.\dev-restart.ps1 -Code    # reinicia montando los binarios publicados
```

`dev-publish.ps1` usa `mcr.microsoft.com/dotnet/sdk:9.0-alpine` y genera `config.gameserver.docker.json` (apunta a `l2dn-authserver` y DB `db`).

**Orden correcto de `-f` flags** (importante para que DataPack/Config del repo prevalezcan):
```
docker-compose.yml + docker-compose.dev-code.yml + docker-compose.dev.yml
```
`dev.yml` siempre al final para que los volúmenes del repo "tapen" el publish.

---

## Modo 3 — Cambios en C# del AuthServer

```powershell
.\dev-publish-auth.ps1
docker compose -f docker-compose.yml -f docker-compose.dev-auth.yml up -d --force-recreate l2dn-authserver
```

O rebuild completo de imagen:
```powershell
docker compose build l2dn-authserver && docker compose up -d l2dn-authserver
```

---

## Modo 4 — Migraciones Entity Framework

```powershell
cd Docker
.\dev-restart.ps1 -Migrate
```

Equivalente manual:
```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml run --rm l2dn-gameserver /App/L2Dn.GameServer -UpdateDatabase
```

Usar **solo cuando cambien entidades EF** (`L2Dn.GameServer.Db`). Ver skill `l2dn-ef-migrations` para crear migraciones nuevas.

---

## Modo 5 — Imagen completa (producción)

```powershell
cd Docker
docker compose up -d --build
```

La primera ejecución aplica migraciones automáticamente (`-UpdateDatabase` en la imagen de producción).

---

## Red local (LAN)

```powershell
cd Docker
.\start-lan.ps1                          # publica 192.168.0.100 por defecto
.\start-lan.ps1 -PublishAddress 192.168.1.50   # otra IP
.\start-lan.ps1 -ExposeDatabase          # expone puerto 5432 además
```

---

## Puertos por defecto

| Servicio     | Puerto |
|-------------|--------|
| Auth Server | 2106   |
| Game Server | 7777   |
| PostgreSQL  | 5432 (solo dentro de Docker en modo normal) |

---

## Archivos clave en `Docker/`

| Archivo | Uso |
|---------|-----|
| `docker-compose.yml` | Stack base (postgres, auth, game) |
| `docker-compose.dev.yml` | Volúmenes DataPack + Config; sin migración automática |
| `docker-compose.dev-code.yml` | Monta `publish/gameserver` (binarios del host) |
| `docker-compose.dev-auth.yml` | Monta `publish/authserver` |
| `dev-up.ps1` | Levanta stack en modo dev (`-Build`, `-Code` opcionales) |
| `dev-restart.ps1` | Reinicio rápido; `-Code` y/o `-Migrate` |
| `dev-publish.ps1` | Publica GameServer vía contenedor SDK |
| `dev-publish-auth.ps1` | Publica AuthServer vía contenedor SDK |
| `config.gameserver.docker.json` | `config.json` para publish del GameServer |

---

## Errores frecuentes

- **Buylist XSD**: en `buylists/custom/` usar `../../xsd/buylist.xsd`, **no** `../../../`.
- **EF PendingModelChanges**: el warning está ignorado en `DesignTimeGameServerDbContextFactory.cs` para permitir arrancar; crear migración cuando sea apropiado.
- **Scheme buffer dances/songs**: el effector en `SchemeBuffer.cs` debe ser el `Player`, no el NPC.
- **`-Code` sin dev.yml al final`**: DataPack/Config del contenedor tapan los del repo. Usar `dev-restart.ps1 -Code` que los aplica en el orden correcto.
