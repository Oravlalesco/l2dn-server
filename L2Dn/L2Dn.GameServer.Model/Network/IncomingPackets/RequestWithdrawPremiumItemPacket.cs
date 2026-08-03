using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.GameAssistant;
using L2Dn.GameServer.Network.Enums;
using L2Dn.GameServer.Network.OutgoingPackets;
using L2Dn.GameServer.Utilities;
using L2Dn.Network;
using L2Dn.Packets;
using Config = L2Dn.GameServer.Configuration.Config;

namespace L2Dn.GameServer.Network.IncomingPackets;

public struct RequestWithdrawPremiumItemPacket: IIncomingPacket<GameSession>
{
    private int _itemNum;
    private int _charId;
    private long _itemCount;

    public void ReadContent(PacketBitReader reader)
    {
        _itemNum = reader.ReadInt32();
        _charId = reader.ReadInt32();
        _itemCount = reader.ReadInt64();
    }

    public ValueTask ProcessAsync(Connection connection, GameSession session)
    {
        Player? player = session.Player;
        if (player == null || !Config.GameAssistant.GAME_ASSISTANT_ENABLED)
            return ValueTask.CompletedTask;

        if (player.ObjectId != _charId)
        {
            Util.handleIllegalPlayerAction(player, "[RequestWithDrawPremiumItem] Incorrect owner, Player: " + player.getName(), Config.General.DEFAULT_PUNISH);
            return ValueTask.CompletedTask;
        }

        PremiumItemClaimResult result = PremiumItemService.getInstance().Claim(player, _itemNum, _itemCount);
        switch (result)
        {
            case PremiumItemClaimResult.InventoryFull:
            case PremiumItemClaimResult.WeightExceeded:
                player.sendPacket(SystemMessageId.YOU_CANNOT_RECEIVE_THE_DIMENSIONAL_ITEM_BECAUSE_YOU_HAVE_EXCEED_YOUR_INVENTORY_WEIGHT_QUANTITY_LIMIT);
                return ValueTask.CompletedTask;
            case PremiumItemClaimResult.TransactionInProgress:
                player.sendPacket(SystemMessageId.ITEMS_FROM_GAME_ASSISTANTS_CANNOT_BE_EXCHANGED);
                return ValueTask.CompletedTask;
            case PremiumItemClaimResult.InvalidCount:
                player.sendPacket(SystemMessageId.INCORRECT_ITEM_COUNT_2);
                return ValueTask.CompletedTask;
            case not PremiumItemClaimResult.Success:
                player.sendMessage("The premium item could not be received. Please try again.");
                return ValueTask.CompletedTask;
        }

        if (player.getPremiumItemList().Count == 0)
        {
            player.sendPacket(SystemMessageId.THERE_ARE_NO_MORE_DIMENSIONAL_ITEMS_TO_BE_FOUND);
        }
        else
        {
            player.sendPacket(new ExGetPremiumItemListPacket(player));
        }

        return ValueTask.CompletedTask;
    }
}
