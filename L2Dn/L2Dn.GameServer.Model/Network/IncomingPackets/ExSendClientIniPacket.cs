using L2Dn.Network;
using L2Dn.Packets;

namespace L2Dn.GameServer.Network.IncomingPackets;

/// <summary>
/// Client settings uploaded while leaving the game. Shinemaker 447 declares this opcode without a server handler.
/// </summary>
public struct ExSendClientIniPacket: IIncomingPacket<GameSession>
{
    public void ReadContent(PacketBitReader reader)
    {
        // Do not retain or log the client's local settings.
        reader.Skip(reader.Length);
    }

    public ValueTask ProcessAsync(Connection connection, GameSession session) => ValueTask.CompletedTask;
}
