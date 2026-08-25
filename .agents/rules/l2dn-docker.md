# L2Dn — Desarrollo solo con Docker

## Regla principal

**No pedir ni asumir .NET SDK instalado en la máquina del usuario.** Compilar y publicar siempre con:

- `mcr.microsoft.com/dotnet/sdk:9.0-alpine` vía `docker run`, o
- los scripts `Docker/dev-publish.ps1` / `Docker/dev-publish-auth.ps1`, o
- `docker compose build` para imágenes de producción.

El usuario solo necesita **Docker Desktop** (o Docker Engine + Compose).

## Directorio de trabajo

Todos los comandos `docker compose` y scripts `.ps1` se ejecutan desde **`Docker/`** (raíz del repo + un nivel: `l2dn-server/Docker`).

## Modos

### 1. DataPack / Config (XML, HTML, spawns, buylists, `Config/*.ini`)

No rebuild. Activar una vez:

```powershell
cd Docker
.\dev-up.ps1
# o: docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --force-recreate l2dn-gameserver
```

Tras cada cambio:

```powershell
.\dev-restart.ps1
```

Volúmenes: `L2Dn/L2Dn.GameServer/DataPack` → `/App/DataPack`, `Config` → `/App/Config`.

### 2. C# GameServer

```powershell
cd Docker
.\dev-publish.ps1      # publish dentro de contenedor SDK → Docker/publish/gameserver
.\dev-restart.ps1 -Code
```

Compose: `docker-compose.yml` + `docker-compose.dev-code.yml` + `docker-compose.dev.yml` (este orden: **dev.yml al final** para que DataPack/Config del repo tapen el publish).

### 3. C# AuthServer

```powershell
.\dev-publish-auth.ps1
docker compose -f docker-compose.yml -f docker-compose.dev-auth.yml up -d --force-recreate l2dn-authserver
```

O rebuild de imagen: `docker compose build l2dn-authserver && docker compose up -d l2dn-authserver`.

### 4. Migraciones EF

```powershell
.\dev-restart.ps1 -Migrate
```

Solo cuando cambie el modelo de base de datos.

### 5. Imagen completa (producción / Dockerfile)

```powershell
docker compose up -d --build
```

## Errores frecuentes

- **Buylist XSD**: en `buylists/custom/` usar `../../xsd/buylist.xsd`, no `../../../`.
- **EF PendingModelChanges**: ver `DesignTimeGameServerDbContextFactory.cs` y migraciones; en dev no migrar en cada restart (`docker-compose.dev.yml` quita `-UpdateDatabase` del CMD).
- **Scheme buffer dances/songs**: effector debe ser el `Player`, no el NPC (`SchemeBuffer.cs`).

## Documentación humana

Ver `README.md` sección «Docker y desarrollo local» y comentarios en `Docker/docker-compose*.yml`.
Para el flujo completo paso a paso, usar la skill `l2dn-docker-dev`.
