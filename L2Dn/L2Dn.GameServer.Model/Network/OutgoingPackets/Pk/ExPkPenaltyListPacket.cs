using L2Dn.Packets;

namespace L2Dn.GameServer.Network.OutgoingPackets.Pk;

public readonly struct ExPkPenaltyListPacket: IOutgoingPacket
{
    public static readonly ExPkPenaltyListPacket EMPTY = new();

    public void WriteContent(PacketBitWriter writer)
    {
        writer.WritePacketCode(OutgoingPacketCodes.EX_PK_PENALTY_LIST);
        writer.WriteInt32(0); // Last PK time. PK penalty tracking is not implemented yet.
        writer.WriteInt32(0); // Player count.
    }
}
