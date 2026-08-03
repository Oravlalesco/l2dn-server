using System.Xml.Linq;
using L2Dn.Extensions;
using L2Dn.GameServer.Model.Holders;
using L2Dn.GameServer.Model.Items;
using L2Dn.GameServer.StaticData;
using L2Dn.GameServer.Utilities;
using L2Dn.Utilities;
using NLog;

namespace L2Dn.GameServer.Data.Xml;

/**
 * @author Mobius
 */
public class LimitShopData: DataReaderBase
{
	private static readonly Logger LOGGER = LogManager.GetLogger(nameof(LimitShopData));

	private readonly List<LimitShopProductHolder> _products = new();

	protected LimitShopData()
	{
		load();
	}

	public void load()
	{
		_products.Clear();

		XDocument document = LoadXmlDocument(DataFileLocation.Data, "LimitShop.xml");
		document.Elements("list").Where(l => l.Attribute("enabled").GetBoolean(false)).Elements("product")
			.ForEach(parseElement);

		if (_products.Count != 0)
		{
			LOGGER.Info(GetType().Name + ": Loaded " + _products.Count + " items.");
		}
		else
		{
			LOGGER.Info(GetType().Name + ": System is disabled.");
		}
	}

	private void parseElement(XElement element)
	{
		int id = element.GetAttributeValueAsInt32("id");
		int category = element.GetAttributeValueAsInt32("category");
		int minLevel = element.Attribute("minLevel").GetInt32(1);
		int maxLevel = element.Attribute("maxLevel").GetInt32(999);
		int[] ingredientIds = new int[5];
		ingredientIds[0] = 0;
		ingredientIds[1] = 0;
		ingredientIds[2] = 0;
		ingredientIds[3] = 0;
		ingredientIds[4] = 0;
		long[] ingredientQuantities = new long[5];
		ingredientQuantities[0] = 0;
		ingredientQuantities[1] = 0;
		ingredientQuantities[2] = 0;
		ingredientQuantities[3] = 0;
		ingredientQuantities[4] = 0;
		int[] ingredientEnchants = new int[5];
		int ingredientIndex = 0;
		int productionId = 0;
		int accountDailyLimit = 0;
		int accountMontlyLimit = 0;
		int accountBuyLimit = 0;

		element.Elements("ingredient").ForEach(el =>
		{
			if (ingredientIndex >= ingredientIds.Length)
			{
				LOGGER.Error($"{GetType().Name}: Product {id} has more than {ingredientIds.Length} ingredients.");
				return;
			}

			int ingredientId = el.GetAttributeValueAsInt32("id");
			long ingredientQuantity = el.Attribute("count").GetInt64(1);
			int ingredientEnchant = el.Attribute("enchant").GetInt32(0);

			if (ingredientId > 0)
			{
				ItemTemplate? item = ItemData.getInstance().getTemplate(ingredientId);
				if (item == null)
				{
					LOGGER.Error(GetType().Name + ": Item template null for itemId: " + productionId + " productId: " +
					             id);
					return;
				}
			}

			ingredientIds[ingredientIndex] = ingredientId;
			ingredientQuantities[ingredientIndex] = ingredientQuantity;
			ingredientEnchants[ingredientIndex] = ingredientEnchant;
			ingredientIndex++;
		});

		element.Elements("production").ForEach(el =>
		{
			productionId = el.GetAttributeValueAsInt32("id");
			accountDailyLimit = el.Attribute("accountDailyLimit").GetInt32(0);
			accountMontlyLimit = el.Attribute("accountMontlyLimit").GetInt32(0);
			accountBuyLimit = el.Attribute("accountBuyLimit").GetInt32(0);

			ItemTemplate? item = ItemData.getInstance().getTemplate(productionId);
			if (item == null)
				LOGGER.Error(GetType().Name + ": Item template null for itemId: " + productionId + " productId: " + id);
		});

		_products.Add(new LimitShopProductHolder(id, category, minLevel, maxLevel, ingredientIds,
			ingredientQuantities, ingredientEnchants, productionId, 1, 100, false, 0, 0, 0, 0,
			false, 0, 0, 0, false, 0, 0, 0, false, 0, 0, false, accountDailyLimit,
			accountMontlyLimit, accountBuyLimit));
	}

	public LimitShopProductHolder? getProduct(int id)
	{
		foreach (LimitShopProductHolder product in _products)
		{
			if (product.getId() == id)
			{
				return product;
			}
		}
		return null;
	}

	public List<LimitShopProductHolder> getProducts()
	{
		return _products;
	}

	public static LimitShopData getInstance()
	{
		return SingletonHolder.INSTANCE;
	}

	private static class SingletonHolder
	{
		public static readonly LimitShopData INSTANCE = new();
	}
}
