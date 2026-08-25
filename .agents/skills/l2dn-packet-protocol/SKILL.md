---
name: l2dn-packet-protocol
description: >-
  Guía para agregar, modificar o depurar paquetes de red en L2Dn Server
  (protocolo Classic 447). Usar cuando el usuario quiera implementar un
  paquete nuevo (incoming/outgoing), entender el flujo de conexión, o
  depurar comunicación cliente-servidor.
---

# L2Dn — Protocolo de Red (Classic 447)

El proyecto implementa el protocolo de Lineage 2 Classic 447. El código de red vive en `L2Dn.Protocol/` con referencias a handlers en `L2Dn.GameServer/`.

---

## Estructura de `L2Dn.Protocol`

```
L2Dn.Protocol/
├── Network/
│   ├── Connection.cs          ← TCP connection, envío/recepción, logging Trace
│   ├── Connector.cs           ← cliente (AuthServer ↔ GameServer)
│   ├── Listener.cs            ← servidor TCP
│   └── Session.cs             ← sesión abstracta
├── Packets/                   ← definiciones de paquetes
├── Cryptography/              ← cifrado L2 (Blowfish, RSA, GameCrypt)
├── ProtocolVersionClassic.cs  ← versiones Classic 447
└── ProtocolVersionMain.cs     ← versiones Main/Essence
```

---

## Tipos de paquetes

| Tipo | Dirección | Carpeta en GameServer |
|------|-----------|----------------------|
| **Incoming** | Cliente → Servidor | `L2Dn.GameServer/Network/IncomingPackets/` |
| **Outgoing** | Servidor → Cliente | `L2Dn.GameServer/Network/OutgoingPackets/` |

---

## Paquete Outgoing (Servidor → Cliente)

```csharp
using L2Dn.Packets;

namespace L2Dn.GameServer.Network.OutgoingPackets;

// Nombre convención: {Descripcion}Packet
public readonly struct UserInfoPacket : IOutgoingPacket
{
    private readonly Player _player;

    public UserInfoPacket(Player player)
    {
        _player = player;
    }

    public void WriteContent(PacketBitWriter writer)
    {
        writer.WritePacketCode(OutgoingPacketCodes.USER_INFO);  // opcode
        writer.WriteInt32(_player.ObjectId);
        writer.WriteString(_player.Name);
        // ... más campos según el protocolo
    }
}
```

Envío desde el GameServer:
```csharp
session.SendPacket(new UserInfoPacket(player));
// o en broadcast:
player.BroadcastPacket(new UserInfoPacket(player));
```

---

## Paquete Incoming (Cliente → Servidor)

```csharp
using L2Dn.GameServer.Network;
using L2Dn.Packets;

namespace L2Dn.GameServer.Network.IncomingPackets;

[PacketVersion(ServerType.Game, ProtocolVersions.CLASSIC_447)]
internal sealed class RequestMoveToLocationPacket: IIncomingPacket<GameSession>
{
    private int _x, _y, _z;
    private int _originX, _originY, _originZ;

    public void ReadContent(PacketBitReader reader)
    {
        _x = reader.ReadInt32();
        _y = reader.ReadInt32();
        _z = reader.ReadInt32();
        _originX = reader.ReadInt32();
        _originY = reader.ReadInt32();
        _originZ = reader.ReadInt32();
    }

    public ValueTask ProcessAsync(Connection connection, GameSession session)
    {
        // Lógica de procesamiento
        Player? player = session.Player;
        if (player == null) return default;

        player.MoveTo(new Location(_x, _y, _z));
        return default;
    }
}
```

---

## Registro de paquetes

Los paquetes incoming se registran en el handler de conexión. Buscar en:
- `L2Dn.GameServer/Network/GamePacketHandler.cs` — para paquetes del juego
- `L2Dn.AuthServer/Network/` — para paquetes de autenticación

El atributo `[PacketVersion]` filtra por versión de protocolo.

---

## Opcodes

Los opcodes están definidos en:
- `OutgoingPacketCodes.cs` — opcodes de respuesta servidor
- `IncomingPacketCodes.cs` — opcodes de solicitud cliente

Para Classic 447, verificar que el opcode existe en `ProtocolVersionClassic.cs`.

---

## Logging de paquetes

El protocolo loguea **cada paquete** enviado/recibido con nivel `Trace` desde `Connection.cs`:

```
[Trace] S(2)  Sending packet SocialActionPacket (27), length: 15
[Trace] C(2)  Received packet RequestMoveToLocation (01), length: 28
```

**Esto NO depende de** `DebugClientPackets`/`DebugServerPackets` en `Config/General.ini` (actualmente no están conectados al logger). El filtro real es `Logging.Console.LogLevel` en `config.json`.

Para ver paquetes: `Logging.Console.LogLevel = "Trace"` en `config.json` → rebuild o `dev-publish.ps1`.

Para silenciarlos en producción: `Logging.Console.LogLevel = "Warn"`.

---

## Flujo de conexión completa

```
Cliente TCP
  ↓
Listener (L2Dn.Protocol/Network/Listener.cs)
  ↓
Connection.cs (handshake, cifrado Blowfish/GameCrypt)
  ↓
ISessionFactory → GameSession / AuthSession
  ↓
IIncomingPacket.ReadContent() → ProcessAsync()
  ↓
Lógica de GameServer / AuthServer
  ↓
session.SendPacket(IOutgoingPacket)
  ↓
Connection.cs → TCP → Cliente
```

---

## Criptografía

| Fase | Algoritmo |
|------|-----------|
| Handshake inicial | RSA (clave pública del servidor) |
| Autenticación (Auth Server) | Blowfish |
| Tráfico de juego | GameCrypt (XOR + checksum) |

El código está en `L2Dn.Protocol/Cryptography/`. No modificar sin entender el handshake completo.

---

## Checklist paquete nuevo

1. [ ] Identificar el opcode en la captura de red (Wireshark / L2PacketSniffer)
2. [ ] Verificar que el opcode existe en `ProtocolVersionClassic.cs`
3. [ ] Crear la clase en `IncomingPackets/` o `OutgoingPackets/`
4. [ ] Implementar `ReadContent` / `WriteContent` en el orden correcto de bytes
5. [ ] Registrar el paquete en el handler correspondiente
6. [ ] Compilar: `dev-publish.ps1` + `dev-restart.ps1 -Code`
7. [ ] Verificar con log `Trace` que el paquete se envía/recibe
