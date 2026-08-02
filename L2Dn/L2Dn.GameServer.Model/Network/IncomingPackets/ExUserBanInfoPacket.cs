using L2Dn.Network;
using L2Dn.Packets;

namespace L2Dn.GameServer.Network.IncomingPackets;

/// <summary>
/// Known Shinemaker 447 packet without a server-side feature implementation.
/// </summary>
public struct ExUserBanInfoPacket: IIncomingPacket<GameSession>
{
    public void ReadContent(PacketBitReader reader)
    {
        reader.ReadInt32();
    }

    public ValueTask ProcessAsync(Connection connection, GameSession session) => ValueTask.CompletedTask;
}
