using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Events;

namespace L2Dn.GameServer.Scripts.Quests.DwarvenVillage;

/**
 * Replaces EP30-unsafe sealed jewelry (98474–98478) with classic IDs the client can render.
 * Always compensates 1:1 for removed sealed items (does not skip when a classic already exists).
 */
internal static class Ep30DwarvenItemCleanup
{
	public static void ReplaceSealedJewelry(Player player)
	{
		foreach ((int sealedId, int classicId) in DwarvenNewbieIds.Ep30UnsafeJewelryRemap)
		{
			long sealedCount = player.getInventory().getInventoryItemCount(sealedId, -1);
			if (sealedCount <= 0)
			{
				continue;
			}

			// takeItems(-1) only clears one non-stackable instance; remove one-by-one and
			// compensate exactly what was destroyed.
			long removed = 0;
			for (long i = 0; i < sealedCount; i++)
			{
				if (!AbstractScript.takeItems(player, sealedId, 1))
				{
					break;
				}

				removed++;
			}

			if (removed > 0)
			{
				AbstractScript.giveItems(player, classicId, removed);
			}
		}

		AbstractScript.takeItems(player, DwarvenNewbieIds.ItemHerbRoots, -1);
	}
}
