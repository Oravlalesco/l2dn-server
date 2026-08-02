using L2Dn.Network;
using L2Dn.Packets;

namespace L2Dn.GameServer.Network.IncomingPackets;

/// <summary>
/// Known Shinemaker 447 packet without a server-side feature implementation.
/// </summary>
public struct ExItemUsableListPacket: IIncomingPacket<GameSession>
{
    public void ReadContent(PacketBitReader reader)
    {
        reader.ReadByte();
    }

    public ValueTask ProcessAsync(Connection connection, GameSession session) => ValueTask.CompletedTask;
}
