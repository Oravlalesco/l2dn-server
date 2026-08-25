---
name: l2dn-ef-migrations
description: >-
  Runbook para crear y aplicar migraciones Entity Framework Core en L2Dn Server
  sin .NET SDK instalado en el host. Usar cuando el usuario modifique entidades
  EF (archivos Db*.cs en L2Dn.GameServer.Db), el DbContext, o cuando aparezca
  el error PendingModelChangesWarning.
---

# L2Dn — Migraciones Entity Framework Core

El proyecto usa **EF Core con PostgreSQL (Npgsql)**. Las migraciones viven en `L2Dn/L2Dn.GameServer.Db/Migrations/`.

---

## Cuándo crear una migración nueva

Crear una migración cuando:
- Se agrega, elimina o modifica una entidad `Db*.cs` en `L2Dn.GameServer.Db/`
- Se cambia `GameServerDbContext.cs` (propiedades `DbSet<>`, configuraciones Fluent API)
- Aparece `PendingModelChangesWarning` al arrancar (aunque está ignorado, indica que el snapshot difiere del modelo)

**No** crear migración por cambios solo en C# de negocio, DataPack XML o config.

---

## Paso 1 — Generar la migración (sin SDK local)

Como no hay SDK en el host, usar el contenedor SDK de .NET 9:

```powershell
# Desde la raíz del repo (l2dn-server/)
docker run --rm `
  -v "${PWD}:/src" `
  -w /src `
  mcr.microsoft.com/dotnet/sdk:9.0-alpine `
  dotnet ef migrations add {NombreMigracion} `
    --project L2Dn/L2Dn.GameServer.Db `
    --startup-project L2Dn/L2Dn.GameServer `
    --context GameServerDbContext
```

Esto genera dos archivos en `L2Dn/L2Dn.GameServer.Db/Migrations/`:
- `{timestamp}_{NombreMigracion}.cs` — los cambios Up/Down
- `{timestamp}_{NombreMigracion}.Designer.cs` — snapshot parcial

Y actualiza `GameServerDbContextModelSnapshot.cs`.

### Convención de nombres para migraciones

`{AñoMesDíaHHMMSS}_{NombreDescriptivo}` — el prefijo timestamp ya lo genera EF automáticamente.

Ejemplos reales en el proyecto:
- `20260803120000_DailyMissionCycles`
- `20260804120000_DailyMissionRewardGrants`
- `20250516120000_ExpandBufferSchemeSkills`

---

## Paso 2 — Aplicar la migración

### En modo dev:

```powershell
cd Docker
.\dev-restart.ps1 -Migrate
```

Equivalente manual:
```powershell
docker compose -f docker-compose.yml -f docker-compose.dev.yml run --rm `
  l2dn-gameserver /App/L2Dn.GameServer -UpdateDatabase
```

### En producción (imagen completa):

```powershell
docker compose up -d --build
```

La imagen de producción ejecuta `-UpdateDatabase` automáticamente en el primer arranque.

---

## `DesignTimeGameServerDbContextFactory`

Para `dotnet ef` (design-time), EF usa `DesignTimeGameServerDbContextFactory` que lee la config desde `config.dev.json` en `L2Dn.GameServer.Db/`. Este archivo contiene la cadena de conexión de desarrollo.

```json
// config.dev.json (ejemplo mínimo)
{
  "Database": {
    "Server": "localhost",
    "DatabaseName": "l2dn",
    "UserName": "postgres",
    "Password": "password"
  }
}
```

Si el contenedor PostgreSQL no está accesible desde el host al generar la migración, usar `-ExposeDatabase` en `start-lan.ps1` o ajustar el bind del puerto en docker-compose.

---

## `PendingModelChangesWarning`

En `DesignTimeGameServerDbContextFactory.cs`:

```csharp
optionsBuilder.ConfigureWarnings(w =>
    w.Ignore(RelationalEventId.PendingModelChangesWarning));
```

Este warning está **ignorado intencionalmente** para permitir arrancar con migraciones pendientes (EF 9+ es más estricto). Lo correcto a medio plazo es crear la migración cuando el snapshot difiera del modelo real.

---

## Verificación

Tras generar la migración:
1. Revisar el `.cs` generado: que el `Up()` refleje los cambios esperados y `Down()` los revierta
2. Verificar que `GameServerDbContextModelSnapshot.cs` fue actualizado
3. Aplicar con `-Migrate` y comprobar que el servidor arranca sin errores de schema

---

## Rollback de migración

```powershell
# Desde el contenedor SDK, revertir a la migración anterior
docker run --rm -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:9.0-alpine `
  dotnet ef migrations remove `
    --project L2Dn/L2Dn.GameServer.Db `
    --startup-project L2Dn/L2Dn.GameServer `
    --context GameServerDbContext
```

Esto elimina la última migración del código (no revierte la BD; para eso usar `database update {MigracionAnterior}`).
