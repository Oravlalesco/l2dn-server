---
name: l2dn-logging
description: >-
  Guía de configuración de NLog en L2Dn Server (GameServer y AuthServer).
  Usar cuando el usuario quiera ajustar niveles de log, entender por qué
  aparecen paquetes en consola, configurar logging para producción, o
  distinguir las opciones de General.ini vs config.json.
---

# L2Dn — Logging (NLog)

El GameServer y AuthServer usan **NLog**. El nivel de log se configura en `config.json`, **no** en `General.ini`.

---

## Archivos de configuración de log

| Archivo | Cuándo aplica |
|---------|---------------|
| `L2Dn/L2Dn.GameServer/config.json` | Imagen Docker de producción (`docker compose up --build`) |
| `Docker/config.gameserver.docker.json` | Publish de desarrollo (`dev-publish.ps1` → copia a `publish/gameserver/config.json`) |
| `L2Dn/L2Dn.AuthServer/config.json` | AuthServer (imagen y publish auth) |

---

## Sección `Logging` en `config.json`

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

Los niveles válidos (de menor a mayor): `Trace` → `Debug` → `Info` → `Warn` → `Error` → `Fatal`.

La base de datos EF ya usa `"LogLevel": "Warn"` en la misma `config.json`.

---

## Nivel `Trace`: qué aparece en consola

Con `Trace` activo, se loguea cada paquete TCP enviado y recibido (desde `Connection.cs`):

```
[03:52:55.610][Trace] S(2)  Sending packet SocialActionPacket (27), length: 15
[03:52:55.612][Trace] C(2)  Received packet RequestMoveToLocation (01), length: 28
```

Esto **no depende de** `DebugClientPackets`, `DebugServerPackets` ni `ExcludedPacketList` en `Config/General.ini` — esos parámetros existen pero **actualmente no están conectados** al logger del protocolo. El filtro efectivo es únicamente `LogLevel` en `config.json`.

---

## Configuración recomendada por entorno

### Desarrollo / debug de red

```json
"Logging": {
  "File": { "Enabled": true, "LogLevel": "Trace" },
  "Console": { "Enabled": true, "LogLevel": "Trace" }
}
```

### Desarrollo con menos ruido en terminal

```json
"Logging": {
  "File": { "Enabled": true, "LogLevel": "Trace" },
  "Console": { "Enabled": true, "LogLevel": "Info" }
}
```

### Producción (recomendado)

```json
"Logging": {
  "File": { "Enabled": true, "LogLevel": "Info" },
  "Console": { "Enabled": true, "LogLevel": "Warn" }
}
```

- `Info` en archivo: eventos del servidor sin volcado de paquetes
- `Warn` en consola: solo avisos y errores en `docker compose logs`

---

## Por qué `Trace` global es costoso en producción

- **Ruido y volumen**: paquetes como `SocialAction`, `MoveToLocation`, `UserInfo` se envían muy frecuentemente
- **Rendimiento**: cada línea implica formatear cadenas y escribir en consola/archivo en caliente
- **Disco**: con `File.Enabled: true` y nivel `Trace`, `logs/` crece rápido (hay rotación diaria pero el tráfico es alto)
- **Docker**: stdout acumulado por el driver de logs del contenedor

No causa memory leak clásico, pero sí coste de I/O y almacenamiento que en producción debe evitarse.

---

## Tabla de variantes útiles en dev

| Objetivo | Console | File |
|----------|---------|------|
| Ver todos los paquetes en tiempo real | `Trace` | `Trace` |
| Historial completo en disco, menos ruido terminal | `Info` o `Warn` | `Trace` |
| Solo terminal, sin archivos | — | `Enabled: false` + Console `Trace` |
| Depuración mínima (rates, spawns, errores) | `Info` | `Info` |

---

## Aplicar cambios de `config.json`

### En modo dev (publish):

```powershell
cd Docker
.\dev-publish.ps1      # regenera config.gameserver.docker.json → publish/gameserver/config.json
.\dev-restart.ps1 -Code
```

### En producción (imagen completa):

```powershell
docker compose up -d --build
```

Hay que **reconstruir** o volver a publicar — el `config.json` se embebe en la imagen o en el publish. No basta con editar el archivo y reiniciar si está en la imagen.

---

## Logger EF (base de datos)

En `DesignTimeGameServerDbContextFactory.cs`:

```csharp
Microsoft.Extensions.Logging.LogLevel logLevel = (Microsoft.Extensions.Logging.LogLevel)config.LogLevel.Ordinal;
Logger logger = LogManager.GetLogger("Database");
optionsBuilder.LogTo((_, level) => level >= logLevel, data => {
    logger.Log(LogLevel.FromOrdinal((int)data.LogLevel), data.ToString());
});
```

El `LogLevel` de la sección `Database` en `config.json` controla cuánto SQL se loguea.

---

## Futuro: `DebugServerPackets` / `ExcludedPacketList`

Cuando estas opciones de `General.ini` estén implementadas en el protocolo, podrán complementar el esquema actual (filtrar tipos concretos con `Trace` global). Hasta entonces, el único control disponible es `LogLevel` en `config.json`.
