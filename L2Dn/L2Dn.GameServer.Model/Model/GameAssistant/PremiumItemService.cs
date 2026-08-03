using System.Collections.Concurrent;
using System.Data;
using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.Db;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Items;
using L2Dn.GameServer.Model.Items.Instances;
using L2Dn.GameServer.Network.OutgoingPackets;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace L2Dn.GameServer.Model.GameAssistant;

public sealed class PremiumItemService
{
    private static readonly Logger Logger = LogManager.GetLogger(nameof(PremiumItemService));
    private readonly ConcurrentDictionary<int, object> _characterLocks = new();

    private PremiumItemService()
    {
    }

    public PremiumItemAssignmentResult Assign(int characterId, int itemId, long count, string? sender)
    {
        if (characterId <= 0)
            return PremiumItemAssignmentResult.Failed("Character does not exist.");

        ItemTemplate? template = ItemData.getInstance().getTemplate(itemId);
        if (template == null)
            return PremiumItemAssignmentResult.Failed("Item does not exist.");

        if (template.hasExImmediateEffect())
            return PremiumItemAssignmentResult.Failed("Immediate-use items cannot be assigned as premium items.");

        if (count <= 0 || (!template.isStackable() && count > int.MaxValue))
            return PremiumItemAssignmentResult.Failed("Invalid item count.");

        string normalizedSender = string.IsNullOrWhiteSpace(sender) ? "Game Assistant" : sender.Trim();
        if (normalizedSender.Length > 50)
            return PremiumItemAssignmentResult.Failed("Sender must be at most 50 characters.");

        int itemNumber;
        lock (GetCharacterLock(characterId))
        {
            try
            {
                using GameServerDbContext ctx = DbFactory.Instance.CreateDbContext();
                using var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable);

                if (!ctx.Characters.Any(character => character.Id == characterId))
                    return PremiumItemAssignmentResult.Failed("Character does not exist.");

                int currentItemNumber = ctx.CharacterPremiumItems
                    .Where(item => item.CharacterId == characterId)
                    .Select(item => (int?)item.ItemNumber)
                    .Max() ?? 0;

                if (currentItemNumber == int.MaxValue)
                    return PremiumItemAssignmentResult.Failed("Premium item sequence is exhausted.");

                itemNumber = currentItemNumber + 1;
                ctx.CharacterPremiumItems.Add(new DbCharacterPremiumItem
                {
                    CharacterId = characterId,
                    ItemNumber = itemNumber,
                    ItemId = itemId,
                    ItemCount = count,
                    ItemSender = normalizedSender,
                });
                ctx.SaveChanges();
                transaction.Commit();
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to assign premium item {itemId} x{count} to character {characterId}.");
                return PremiumItemAssignmentResult.Failed("Could not persist the premium item.");
            }

            Player? onlinePlayer = World.getInstance().getPlayer(characterId);
            if (onlinePlayer != null)
            {
                onlinePlayer.getPremiumItemList().put(itemNumber, new PremiumItem(itemId, count, normalizedSender));
                onlinePlayer.sendPacket(ExNotifyPremiumItemPacket.STATIC_PACKET);
                onlinePlayer.sendPacket(new ExGetPremiumItemListPacket(onlinePlayer));
            }
        }

        Logger.Info($"Assigned premium item number {itemNumber}: item={itemId}, count={count}, character={characterId}, sender={normalizedSender}.");
        return PremiumItemAssignmentResult.Succeeded(itemNumber);
    }

    public PremiumItemClaimResult Claim(Player player, int itemNumber, long count)
    {
        if (count <= 0)
            return PremiumItemClaimResult.InvalidCount;

        if (player.isProcessingTransaction())
            return PremiumItemClaimResult.TransactionInProgress;

        lock (GetCharacterLock(player.ObjectId))
        {
            DbCharacterPremiumItem? record;
            try
            {
                using GameServerDbContext ctx = DbFactory.Instance.CreateDbContext();
                record = ctx.CharacterPremiumItems.SingleOrDefault(item =>
                    item.CharacterId == player.ObjectId && item.ItemNumber == itemNumber);

                if (record == null)
                    return PremiumItemClaimResult.NotFound;

                if (count > record.ItemCount)
                    return PremiumItemClaimResult.InvalidCount;

                ItemTemplate? template = ItemData.getInstance().getTemplate(record.ItemId);
                if (template == null || template.hasExImmediateEffect())
                    return PremiumItemClaimResult.InvalidItem;

                long slots = template.isStackable()
                    ? player.getInventory().getItemByItemId(record.ItemId) == null ? 1 : 0
                    : count;

                long weight;
                try
                {
                    weight = checked(template.getWeight() * count);
                }
                catch (OverflowException)
                {
                    return PremiumItemClaimResult.InvalidCount;
                }

                if (!player.getInventory().validateCapacity(slots))
                    return PremiumItemClaimResult.InventoryFull;

                if (!player.getInventory().validateWeight(weight))
                    return PremiumItemClaimResult.WeightExceeded;

                Item? deliveredItem;
                try
                {
                    deliveredItem = player.addItem("PremiumItem", record.ItemId, count, null, true);
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Failed to deliver premium item {record.ItemId} x{count} to {player}.");
                    return PremiumItemClaimResult.DeliveryFailed;
                }

                if (deliveredItem == null)
                    return PremiumItemClaimResult.DeliveryFailed;

                try
                {
                    long remaining = record.ItemCount - count;
                    if (remaining == 0)
                        ctx.CharacterPremiumItems.Remove(record);
                    else
                        record.ItemCount = remaining;

                    ctx.SaveChanges();

                    if (remaining == 0)
                        player.getPremiumItemList().remove(itemNumber);
                    else if (player.getPremiumItemList().get(itemNumber) is PremiumItem memoryItem)
                        memoryItem.updateCount(remaining);

                    Logger.Info($"{player} claimed premium item number {itemNumber}: item={record.ItemId}, count={count}, remaining={remaining}.");
                    return PremiumItemClaimResult.Success;
                }
                catch (Exception e)
                {
                    bool rolledBack = player.destroyItemByItemId("PremiumItemRollback", record.ItemId, count, null, false);
                    Logger.Error(e,
                        $"Failed to persist premium claim item={record.ItemId}, count={count}, player={player}. Inventory rollback={rolledBack}.");
                    return PremiumItemClaimResult.PersistenceFailed;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to load premium item number {itemNumber} for {player}.");
                return PremiumItemClaimResult.PersistenceFailed;
            }
        }
    }

    private object GetCharacterLock(int characterId) => _characterLocks.GetOrAdd(characterId, static _ => new object());

    public static PremiumItemService getInstance() => SingletonHolder.Instance;

    private static class SingletonHolder
    {
        public static readonly PremiumItemService Instance = new();
    }
}

public readonly record struct PremiumItemAssignmentResult(bool Success, int ItemNumber, string Message)
{
    public static PremiumItemAssignmentResult Succeeded(int itemNumber) => new(true, itemNumber, string.Empty);
    public static PremiumItemAssignmentResult Failed(string message) => new(false, 0, message);
}

public enum PremiumItemClaimResult
{
    Success,
    NotFound,
    InvalidCount,
    InvalidItem,
    InventoryFull,
    WeightExceeded,
    TransactionInProgress,
    DeliveryFailed,
    PersistenceFailed,
}
