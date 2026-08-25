# L2Dn Server — Reglas generales del proyecto

## Stack y entorno

- **.NET 9 / C#** para todo el código del servidor
- **PostgreSQL** como base de datos (via Npgsql + EF Core)
- **NLog** para logging (configurado en `config.json`, no en `General.ini`)
- **Protocolo cliente**: Classic 447
- **Docker** como único entorno de compilación y ejecución en la máquina del desarrollador

## Restricción fundamental

**Nunca asumir que .NET SDK está instalado en el host.** Todo `dotnet` debe correr dentro de un contenedor Docker:

```powershell
# MAL: dotnet build ...
# BIEN: usar Docker/dev-publish.ps1 o docker run mcr.microsoft.com/dotnet/sdk:9.0-alpine
```

## Estructura del proyecto

```
L2Dn/
├── L2Dn.GameServer/          # Servidor de juego principal
├── L2Dn.GameServer.Db/       # Entidades EF Core + Migrations
├── L2Dn.GameServer.Scripts/  # Quests, Handlers, AI scripts
├── L2Dn.GameServer.Model/    # Modelo de dominio, actores, AI
├── L2Dn.GameServer.Enums/    # Enumeraciones compartidas
├── L2Dn.GameServer.StaticData/ # Carga de XML DataPack en memoria
├── L2Dn.AuthServer/          # Servidor de autenticación
├── L2Dn.Npc.Brain/           # Sistema IA de NPCs (capas Strategy/Reflex/Tactical)
├── L2Dn.Npc.Contracts/       # Contratos del Brain (sin dependencia de GameServer)
├── L2Dn.Protocol/            # Red, paquetes, criptografía
└── L2Dn.Common/              # Utilidades compartidas
```

## DataPack

Los datos del juego (spawns, stats NPCs, buylists, multisell, HTML) viven en:
`L2Dn/L2Dn.GameServer/DataPack/`

Los archivos custom van en subdirectorios `custom/` (ya habilitados en `Config/General.ini`).

## Convenciones de código

- Namespaces alineados con la estructura de carpetas del proyecto
- Entidades EF Core tienen prefijo `Db` (e.g. `DbCharacter`, `DbItem`)
- Paquetes outgoing terminan en `Packet` (e.g. `UserInfoPacket`)
- Quests: `Q{id}{NombreDescriptivo}` en `L2Dn.GameServer.Scripts/Quests/`

## Documentación interna

- ADRs y documentación de arquitectura: `L2Dn/Documentation/Architecture/`
- Guías de features: `L2Dn/Documentation/*.md`
- Regla Docker detallada: ver skill `l2dn-docker-dev`
- Regla NPC shops: ver skill `l2dn-npc-shop`
