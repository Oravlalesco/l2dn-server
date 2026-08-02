using FluentAssertions;
using L2Dn.GameServer.Network.OutgoingPackets.Pk;
using L2Dn.Packets;

namespace L2Dn.GameServer.Model.Tests;

public class ExPkPenaltyListPacketTests
{
    [Fact]
    public void Empty_packet_writes_ShineMaker_447_opcode_and_empty_list()
    {
        byte[] buffer = new byte[11];
        int offset = 0;
        PacketBitWriter writer = new(buffer, ref offset);

        ExPkPenaltyListPacket.EMPTY.WriteContent(writer);

        offset.Should().Be(buffer.Length);
        buffer.Should().Equal(0xFE, 0x7B, 0x02, 0, 0, 0, 0, 0, 0, 0, 0);
    }
}
