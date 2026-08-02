using L2Dn.GameServer.Network.OutgoingPackets.Pk;
using L2Dn.Network;
using L2Dn.Packets;

namespace L2Dn.GameServer.Network.IncomingPackets.Pk;

public struct RequestExPkPenaltyListPacket: IIncomingPacket<GameSession>
{
    public void ReadContent(PacketBitReader reader)
    {
    }

    public ValueTask ProcessAsync(Connection connection, GameSession session)
    {
        connection.Send(ExPkPenaltyListPacket.EMPTY);
        return ValueTask.CompletedTask;
    }
}
