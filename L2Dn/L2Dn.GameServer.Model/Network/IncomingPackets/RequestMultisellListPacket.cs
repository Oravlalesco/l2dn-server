using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.GameAssistant;
using L2Dn.Network;
using L2Dn.Packets;

namespace L2Dn.GameServer.Network.IncomingPackets;

public struct RequestMultisellListPacket: IIncomingPacket<GameSession>
{
    private int _multiSellId;

    public void ReadContent(PacketBitReader reader)
    {
        _multiSellId = reader.ReadInt32();
    }

    public ValueTask ProcessAsync(Connection connection, GameSession session)
    {
        Player? player = session.Player;
        if (player == null)
            return ValueTask.CompletedTask;

        ItemMultisellData.getInstance().TryOpen(player, _multiSellId);
        return ValueTask.CompletedTask;
    }
}
