using L2Dn.GameServer.Dto;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Holders;
using L2Dn.GameServer.Model.ItemContainers;
using L2Dn.GameServer.Model.Items.Instances;
using L2Dn.GameServer.Network.Enums;
using L2Dn.GameServer.Network.OutgoingPackets;
using L2Dn.GameServer.Utilities;
using L2Dn.Network;
using L2Dn.Packets;
using Config = L2Dn.GameServer.Configuration.Config;

namespace L2Dn.GameServer.Network.IncomingPackets;

public struct RequestPackageSendPacket: IIncomingPacket<GameSession>
{
    private const int BatchLength = 12;

    private ItemHolder[]? _items;
    private int _objectId;

    public void ReadContent(PacketBitReader reader)
    {
        _objectId = reader.ReadInt32();

        int count = reader.ReadInt32();
        if (count <= 0 || count > Config.Character.MAX_ITEM_IN_PACKET || count * BatchLength != reader.Length)
            return;

        HashSet<int> objectIds = [];
        _items = new ItemHolder[count];
        for (int index = 0; index < count; index++)
        {
            int objectId = reader.ReadInt32();
            long itemCount = reader.ReadInt64();
            if (objectId < 1 || itemCount <= 0 || !objectIds.Add(objectId))
            {
                _items = null;
                return;
            }

            _items[index] = new ItemHolder(objectId, itemCount);
        }
    }

    public ValueTask ProcessAsync(Connection connection, GameSession session)
    {
        Player? player = session.Player;
        if (_items == null || player == null || !Config.GameAssistant.GAME_ASSISTANT_ENABLED ||
            _objectId == player.ObjectId ||
            !player.getAccountChars().ContainsKey(_objectId))
        {
            return ValueTask.CompletedTask;
        }

        if (player.hasItemRequest() || player.isProcessingTransaction())
            return ValueTask.CompletedTask;

        if (!Config.Character.ALT_GAME_KARMA_PLAYER_CAN_USE_WAREHOUSE && player.getReputation() < 0)
            return ValueTask.CompletedTask;

        PlayerFreight freight = new(_objectId);
        try
        {
            long fee;
            try
            {
                fee = checked((long)_items.Length * Config.Character.ALT_FREIGHT_PRICE);
            }
            catch (OverflowException)
            {
                return ValueTask.CompletedTask;
            }

            long availableAdena = player.getAdena();
            long requiredSlots = 0;
            List<(ItemHolder Request, Item Item)> validatedItems = new(_items.Length);

            foreach (ItemHolder request in _items)
            {
                Item? item = player.checkItemManipulation(request.getId(), request.getCount(), "freight");
                if (item == null || !item.isFreightable())
                {
                    PacketLogger.Instance.Warn($"{player} attempted to freight an invalid item {request.getId()}.");
                    return ValueTask.CompletedTask;
                }

                validatedItems.Add((request, item));
                if (item.getId() == Inventory.ADENA_ID)
                {
                    availableAdena -= request.getCount();
                }
                else if (!item.isStackable())
                {
                    requiredSlots = checked(requiredSlots + request.getCount());
                }
                else if (freight.getItemByItemId(item.getId()) == null)
                {
                    requiredSlots++;
                }
            }

            if (!freight.validateCapacity(requiredSlots))
            {
                player.sendPacket(SystemMessageId.YOU_HAVE_EXCEEDED_THE_QUANTITY_THAT_CAN_BE_INPUTTED);
                return ValueTask.CompletedTask;
            }

            if (availableAdena < fee || !player.reduceAdena(freight.getName(), fee, player, false))
            {
                player.sendPacket(SystemMessageId.NOT_ENOUGH_ADENA);
                return ValueTask.CompletedTask;
            }

            List<(Item Item, long Count)> movedItems = new(validatedItems.Count);
            List<ItemInfo> inventoryUpdates = new(validatedItems.Count);
            foreach ((ItemHolder request, Item validatedItem) in validatedItems)
            {
                Item? currentItem;
                Item? freightItem;
                try
                {
                    currentItem = player.checkItemManipulation(request.getId(), request.getCount(), "freight-deposit");
                    freightItem = currentItem == null
                        ? null
                        : player.getInventory().transferItem("Freight", request.getId(), request.getCount(), freight,
                            player, null);
                }
                catch (Exception e)
                {
                    PacketLogger.Instance.Error($"Freight transfer for {player} failed: {e}");
                    RollbackDeposit(player, freight, movedItems, fee);
                    return ValueTask.CompletedTask;
                }

                if (currentItem == null || freightItem == null)
                {
                    RollbackDeposit(player, freight, movedItems, fee);
                    return ValueTask.CompletedTask;
                }

                movedItems.Add((freightItem, request.getCount()));
                inventoryUpdates.Add(new ItemInfo(validatedItem,
                    validatedItem.getCount() > 0 && validatedItem != freightItem
                        ? ItemChangeType.MODIFIED
                        : ItemChangeType.REMOVED));
            }

            foreach ((ItemHolder _, Item oldItem) in validatedItems)
            {
                if (oldItem.getCount() <= 0)
                    World.getInstance().removeObject(oldItem);
            }

            foreach (Item freightItem in movedItems.Select(entry => entry.Item).Distinct())
                World.getInstance().removeObject(freightItem);

            InventoryUpdatePacket update = new(inventoryUpdates);
            player.sendInventoryUpdate(update);
            PacketLogger.Instance.Info($"{player} transferred {_items.Length} item stack(s) to account character {_objectId}.");
            return ValueTask.CompletedTask;
        }
        catch (OverflowException)
        {
            PacketLogger.Instance.Warn($"{player} sent overflowing freight quantities.");
            return ValueTask.CompletedTask;
        }
        finally
        {
            freight.deleteMe();
        }
    }

    private static void RollbackDeposit(Player player, PlayerFreight freight,
        List<(Item Item, long Count)> movedItems, long fee)
    {
        bool success = true;
        foreach ((Item item, long count) in movedItems.AsEnumerable().Reverse())
        {
            try
            {
                success &= freight.transferItem("FreightRollback", item.ObjectId, count, player.getInventory(),
                    player, null) != null;
            }
            catch (Exception e)
            {
                success = false;
                PacketLogger.Instance.Error($"Freight rollback for {player} failed: {e}");
            }
        }

        player.addAdena("FreightRollback", fee, null, false);
        player.sendItemList();
        PacketLogger.Instance.Warn($"Freight transfer for {player} was rolled back. Success={success}.");
    }
}
