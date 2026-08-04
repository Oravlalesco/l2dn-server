using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Request;
using L2Dn.GameServer.Model.Holders;
using L2Dn.GameServer.Model.ItemContainers;
using L2Dn.GameServer.Model.Items.Instances;
using L2Dn.GameServer.Model.Variables;
using L2Dn.GameServer.Network.Enums;
using L2Dn.GameServer.Network.OutgoingPackets;
using L2Dn.GameServer.Network.OutgoingPackets.LimitShop;
using L2Dn.GameServer.Network.OutgoingPackets.PrimeShop;
using L2Dn.GameServer.Utilities;
using L2Dn.Network;
using L2Dn.Packets;
using L2Dn.Utilities;
using Config = L2Dn.GameServer.Configuration.Config;

namespace L2Dn.GameServer.Network.IncomingPackets.LimitShop;

public struct RequestPurchaseLimitShopItemBuyPacket: IIncomingPacket<GameSession>
{
    private int _shopIndex;
    private int _productId;
    private int _amount;
    private LimitShopProductHolder? _product;

    public void ReadContent(PacketBitReader reader)
    {
        _shopIndex = reader.ReadByte(); // 3 Lcoin Store, 4 Special Craft, 100 Clan Shop
        _productId = reader.ReadInt32();
        _amount = reader.ReadInt32();

        switch (_shopIndex)
        {
            case 3: // Normal Lcoin Shop
            {
                _product = LimitShopData.getInstance().getProduct(_productId);
                break;
            }
            case 4: // Lcoin Special Craft
            {
                _product = LimitShopCraftData.getInstance().getProduct(_productId);
                break;
            }
            case 100: // Clan Shop
            {
                _product = LimitShopClanData.getInstance().getProduct(_productId);
                break;
            }
            default:
            {
                _product = null;
                break;
            }
        }

        reader.ReadInt32(); // SuccessionItemSID
        reader.ReadInt32(); // MaterialItemSID
    }

    public ValueTask ProcessAsync(Connection connection, GameSession session)
    {
        Player? player = session.Player;
        if (player == null)
            return ValueTask.CompletedTask;

		if (_product == null)
			return ValueTask.CompletedTask;

		if (_amount < 1 || _amount > 10000)
		{
			player.sendPacket(new ExBRBuyProductPacket(ExBrProductReplyType.INVENTORY_OVERFLOW));
			player.sendPacket(new ExPurchaseLimitShopItemResultPacket(false, _shopIndex, _productId, 0,
				new List<LimitShopRandomCraftReward>()));

			return ValueTask.CompletedTask;
		}

		if (!player.isInventoryUnder80(false))
		{
			player.sendPacket(new ExBRBuyProductPacket(ExBrProductReplyType.INVENTORY_OVERFLOW));
			player.sendPacket(new ExPurchaseLimitShopItemResultPacket(false, _shopIndex, _productId, 0,
				new List<LimitShopRandomCraftReward>()));

			return ValueTask.CompletedTask;
		}

		if (player.getLevel() < _product.getMinLevel() || player.getLevel() > _product.getMaxLevel())
		{
			player.sendPacket(SystemMessageId.YOUR_LEVEL_CANNOT_PURCHASE_THIS_ITEM);
			player.sendPacket(new ExPurchaseLimitShopItemResultPacket(false, _shopIndex, _productId, 0,
				new List<LimitShopRandomCraftReward>()));

			return ValueTask.CompletedTask;
		}

		if (player.hasItemRequest() || player.hasRequest<PrimeShopRequest>())
		{
			player.sendPacket(new ExBRBuyProductPacket(ExBrProductReplyType.INVALID_USER_STATE));
			player.sendPacket(new ExPurchaseLimitShopItemResultPacket(false, _shopIndex, _productId, 0,
				new List<LimitShopRandomCraftReward>()));

			return ValueTask.CompletedTask;
		}

		// Add request.
		player.addRequest(new PrimeShopRequest(player));

		// Check limits.
		int remainingInfo = 0;
		if (_product.getAccountDailyLimit() > 0) // Sale period.
		{
			string countName = AccountVariables.getLCoinShopProductDailyCountName(_shopIndex, _product.getId());
			long currentCount = player.getAccountVariables().Get(countName, 0);
			remainingInfo = (int)Math.Max(_product.getAccountDailyLimit() - currentCount, 0);
			if (_amount > remainingInfo)
			{
				player.sendMessage("You have reached your daily limit."); // TODO: Retail system message?
				player.removeRequest<PrimeShopRequest>();
				player.sendPacket(new ExPurchaseLimitShopItemResultPacket(false, _shopIndex, _productId, 0,
					new List<LimitShopRandomCraftReward>()));

				return ValueTask.CompletedTask;
			}
		}
		else if (_product.getAccountMontlyLimit() > 0)
		{
			string countName = AccountVariables.getLCoinShopProductMontlyCountName(_shopIndex, _product.getId());
			long currentCount = player.getAccountVariables().Get(countName, 0);
			remainingInfo = (int)Math.Max(_product.getAccountMontlyLimit() - currentCount, 0);
			if (_amount > remainingInfo)
			{
				player.sendMessage("You have reached your montly limit.");
				player.removeRequest<PrimeShopRequest>();
				player.sendPacket(new ExPurchaseLimitShopItemResultPacket(false, _shopIndex, _productId, 0,
					new List<LimitShopRandomCraftReward>()));

				return ValueTask.CompletedTask;
			}

		}
		else if (_product.getAccountBuyLimit() > 0) // Count limit.
		{
			string countName = AccountVariables.getLCoinShopProductCountName(_shopIndex, _product.getId());
			long currentCount = player.getAccountVariables().Get(countName, 0);
			remainingInfo = (int)Math.Max(_product.getAccountBuyLimit() - currentCount, 0);
			if (_amount > remainingInfo)
			{
				player.sendMessage("You cannot buy any more of this item."); // TODO: Retail system message?
				player.removeRequest<PrimeShopRequest>();
				player.sendPacket(new ExPurchaseLimitShopItemResultPacket(false, _shopIndex, _productId, 0,
					new List<LimitShopRandomCraftReward>()));

				return ValueTask.CompletedTask;
			}
		}

		// Merge equal ingredients before validating or consuming them. Several
		// Special Craft recipes require two items with the same ID and enchant.
		Dictionary<(int ItemId, int Enchant), long> ingredients = new();
		long adenaPerAttempt = 0;
		for (int i = 0; i < _product.getIngredientIds().Length; i++)
		{
			int ingredientId = _product.getIngredientIds()[i];
			if (ingredientId == 0)
				continue;

			if (ingredientId == Inventory.ADENA_ID)
				adenaPerAttempt += _product.getIngredientQuantities()[i];

			(int ItemId, int Enchant) key = (ingredientId, _product.getIngredientEnchants()[i]);
			ingredients[key] = ingredients.GetValueOrDefault(key) + _product.getIngredientQuantities()[i] * _amount;
		}

		// Check existing items.
		foreach (((int ingredientId, int enchant), long amount) in ingredients)
		{
			if (amount < 1)
			{
				player.sendPacket(SystemMessageId.INCORRECT_ITEM_COUNT_2);
				player.removeRequest<PrimeShopRequest>();
				player.sendPacket(new ExPurchaseLimitShopItemResultPacket(false, _shopIndex, _productId,
					remainingInfo, new List<LimitShopRandomCraftReward>()));

				return ValueTask.CompletedTask;
			}

			if (ingredientId == Inventory.ADENA_ID)
			{
				if (player.getAdena() < amount)
				{
					player.sendPacket(SystemMessageId.INCORRECT_ITEM_COUNT_2);
					player.removeRequest<PrimeShopRequest>();
					player.sendPacket(new ExPurchaseLimitShopItemResultPacket(false, _shopIndex, _productId,
						remainingInfo, new List<LimitShopRandomCraftReward>()));

					return ValueTask.CompletedTask;
				}
			}
			else if (ingredientId == (int)SpecialItemType.HONOR_COINS)
			{
				if (player.getHonorCoins() < amount)
				{
					player.sendPacket(SystemMessageId.INCORRECT_ITEM_COUNT_2);
					player.removeRequest<PrimeShopRequest>();
					player.sendPacket(new ExPurchaseLimitShopItemResultPacket(false, _shopIndex, _productId,
						remainingInfo, new List<LimitShopRandomCraftReward>()));

					return ValueTask.CompletedTask;
				}
			}
			else if (ingredientId == (int)SpecialItemType.PC_CAFE_POINTS)
			{
				if (player.getPcCafePoints() < amount)
				{
					player.sendPacket(SystemMessageId.INCORRECT_ITEM_COUNT_2);
					player.removeRequest<PrimeShopRequest>();
					player.sendPacket(new ExPurchaseLimitShopItemResultPacket(false, _shopIndex, _productId,
						remainingInfo, new List<LimitShopRandomCraftReward>()));

					return ValueTask.CompletedTask;
				}
			}
			else
			{
				if (player.getInventory().getInventoryItemCount(ingredientId, enchant == 0 ? -1 : enchant, true) < amount)
				{
					player.sendPacket(SystemMessageId.INCORRECT_ITEM_COUNT_2);
					player.removeRequest<PrimeShopRequest>();
					player.sendPacket(new ExPurchaseLimitShopItemResultPacket(false, _shopIndex, _productId,
						remainingInfo, new List<LimitShopRandomCraftReward>()));

					return ValueTask.CompletedTask;
				}
			}
		}

		// Remove items.
		foreach (((int ingredientId, int enchant), long amount) in ingredients)
		{
			if (ingredientId == Inventory.ADENA_ID)
			{
				player.reduceAdena("LCoinShop", amount, player, true);
			}
			else if (ingredientId == (int)SpecialItemType.HONOR_COINS)
			{
				player.setHonorCoins(player.getHonorCoins() - amount);
			}
			else if (ingredientId == (int)SpecialItemType.PC_CAFE_POINTS)
			{
				int newPoints = (int)(player.getPcCafePoints() - amount);
				player.setPcCafePoints(newPoints);
				player.sendPacket(new ExPcCafePointInfoPacket(player.getPcCafePoints(), (int)-amount, 1));
			}
			else
			{
				if (enchant > 0)
				{
					long remaining = amount;
					ICollection<Item> items = player.getInventory().getAllItemsByItemId(ingredientId, enchant);
					foreach (Item item in items)
					{
						if (remaining == 0)
							break;

						long itemCount = Math.Min(item.getCount(), remaining);
						player.destroyItem("LCoinShop", item, itemCount, player, true);
						remaining -= itemCount;
					}
				}
				else
				{
					player.destroyItemByItemId("LCoinShop", ingredientId, amount, player, true);
				}
			}
		}

		if (Config.VipSystem.VIP_SYSTEM_L_SHOP_AFFECT)
			player.updateVipPoints(_amount);

		// Reward.
		Map<int, LimitShopRandomCraftReward> rewards = new();
		for (int i = 0; i < _amount; i++)
		{
			LimitShopRewardOutcome? selectedOutcome = LimitShopRewardSelector.Select(_product, Rnd.get(100d));
			if (_product.isRefundAdenaOnFailure() &&
				(selectedOutcome is null || selectedOutcome.Value.Index != 0) && adenaPerAttempt > 0)
			{
				player.addAdena("SpecialCraftFailureRefund", adenaPerAttempt, player, true);
			}

			if (selectedOutcome is not { } outcome)
				continue;

			rewards.GetOrAdd(outcome.Index,
				_ => new LimitShopRandomCraftReward(outcome.ItemId, 0, outcome.Index)).Count += (int)outcome.Count;

			Item? item = player.addItem("LCoinShop", outcome.ItemId, outcome.Count, outcome.Enchant, player, true);
			if (item == null)
			{
				player.sendPacket(SystemMessageId.YOUR_INVENTORY_IS_FULL); // TODO: atomic inventory update
				return ValueTask.CompletedTask;
			}

			if (outcome.Announce)
				Broadcast.toAllOnlinePlayers(new ExItemAnnouncePacket(player, item,
					ExItemAnnouncePacket.SPECIAL_CREATION));
		}

		// Update account variables.
		if (_product.getAccountDailyLimit() > 0)
		{
			string countName = AccountVariables.getLCoinShopProductDailyCountName(_shopIndex, _product.getId());
			player.getAccountVariables()
				.Set(countName, player.getAccountVariables().Get(countName, 0) + _amount);
		}

		if (_product.getAccountMontlyLimit() > 0)
		{
			string countName = AccountVariables.getLCoinShopProductMontlyCountName(_shopIndex, _product.getId());
			player.getAccountVariables()
				.Set(countName, player.getAccountVariables().Get(countName, 0) + _amount);
		}
		else if (_product.getAccountBuyLimit() > 0)
		{
			string countName = AccountVariables.getLCoinShopProductCountName(_shopIndex, _product.getId());
			player.getAccountVariables().Set(countName,
				player.getAccountVariables().Get(countName, 0) + _amount);
		}

		player.sendPacket(new ExPurchaseLimitShopItemResultPacket(true, _shopIndex, _productId,
			Math.Max(remainingInfo - _amount, 0), rewards.Values));

		player.sendItemList();

		// Remove request.
		player.removeRequest<PrimeShopRequest>();

        return ValueTask.CompletedTask;
    }
}
