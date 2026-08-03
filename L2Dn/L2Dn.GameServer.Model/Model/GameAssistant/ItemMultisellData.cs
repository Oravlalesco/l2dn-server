using System.Collections.Frozen;
using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Holders;
using L2Dn.GameServer.StaticData;
using L2Dn.GameServer.Utilities;
using L2Dn.Model.Xml;
using NLog;
using Config = L2Dn.GameServer.Configuration.Config;

namespace L2Dn.GameServer.Model.GameAssistant;

public sealed class ItemMultisellData: DataReaderBase
{
    private static readonly Logger Logger = LogManager.GetLogger(nameof(ItemMultisellData));
    private FrozenDictionary<int, ItemMultisellRoute> _routes = FrozenDictionary<int, ItemMultisellRoute>.Empty;

    private ItemMultisellData()
    {
        Load();
    }

    public void Load()
    {
        XmlItemMultisellList document = LoadXmlDocument<XmlItemMultisellList>(DataFileLocation.Data,
            "ItemMultisells.xml");

        Dictionary<int, ItemMultisellRoute> routes = new();
        HashSet<int> itemIds = [];
        int activeCount = 0;

        foreach (XmlItemMultisellRoute xmlRoute in document.Routes)
        {
            if (xmlRoute.ItemId <= 0 || xmlRoute.ListId <= 0 || routes.ContainsKey(xmlRoute.ListId) ||
                !itemIds.Add(xmlRoute.ItemId))
            {
                Logger.Error($"Invalid or duplicate item multisell route item={xmlRoute.ItemId}, list={xmlRoute.ListId}.");
                continue;
            }

            bool active = xmlRoute.Enabled && ValidateList(xmlRoute.ItemId, xmlRoute.ListId);
            routes.Add(xmlRoute.ListId, new ItemMultisellRoute(xmlRoute.ItemId, xmlRoute.ListId, active));
            if (active)
                activeCount++;
        }

        _routes = routes.ToFrozenDictionary();
        Logger.Info($"Loaded {_routes.Count} item multisell routes ({activeCount} active, {_routes.Count - activeCount} disabled).");
    }

    public bool TryOpen(Player player, int listId)
    {
        player.setMultiSell(null);

        if (!Config.GameAssistant.GAME_ASSISTANT_ENABLED)
        {
            GameAssistantUi.ShowError(player, "Game Assistant is temporarily unavailable.");
            return false;
        }

        if (!_routes.TryGetValue(listId, out ItemMultisellRoute? route) || route == null)
        {
            Logger.Warn($"{player} requested unregistered item multisell {listId}.");
            GameAssistantUi.ShowError(player, "This item exchange is not available.");
            return false;
        }

        if (!route.Enabled)
        {
            Logger.Info($"{player} requested disabled item multisell {listId}.");
            GameAssistantUi.ShowError(player, "This exchange is not available for the current server version.");
            return false;
        }

        if (player.getInventory().getInventoryItemCount(route.ItemId, -1) < 1)
        {
            Logger.Warn($"{player} requested item multisell {listId} without required item {route.ItemId}.");
            string itemName = ItemData.getInstance().getTemplate(route.ItemId)?.getName() ?? $"item {route.ItemId}";
            GameAssistantUi.ShowError(player, $"You need {itemName} in your inventory to use this exchange.");
            return false;
        }

        MultisellData.getInstance().separateAndSend(listId, player, null, false, null, null, 4);
        if (player.getMultiSell()?.getId() == listId)
            return true;

        Logger.Warn($"Failed to prepare item multisell {listId} for {player}.");
        GameAssistantUi.ShowError(player, "The exchange could not be opened. Please try again.");
        return false;
    }

    public ItemMultisellRoute? GetRoute(int listId) => _routes.GetValueOrDefault(listId);

    private static bool ValidateList(int itemId, int listId)
    {
        MultisellListHolder? list = MultisellData.getInstance().getMultisell(listId);
        if (list == null || list.getEntries().IsDefaultOrEmpty)
        {
            Logger.Error($"Enabled item multisell {listId} has no valid catalog.");
            return false;
        }

        foreach (MultisellEntryHolder entry in list.getEntries())
        {
            if (entry.getIngredients().Count == 0 || entry.getProducts().Count == 0 ||
                !entry.getIngredients().Any(ingredient => ingredient.getId() == itemId && ingredient.getCount() > 0))
            {
                Logger.Error($"Enabled item multisell {listId} contains an entry that does not consume item {itemId}.");
                return false;
            }
        }

        return true;
    }

    public static ItemMultisellData getInstance() => SingletonHolder.Instance;

    private static class SingletonHolder
    {
        public static readonly ItemMultisellData Instance = new();
    }
}

public sealed record ItemMultisellRoute(int ItemId, int ListId, bool Enabled);
