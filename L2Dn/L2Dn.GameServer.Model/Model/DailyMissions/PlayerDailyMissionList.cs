using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.Db;
using L2Dn.GameServer.Dto;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Handlers;
using L2Dn.GameServer.InstanceManagers;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.ItemContainers;
using L2Dn.GameServer.Model.Items;
using L2Dn.GameServer.Model.Items.Instances;
using L2Dn.GameServer.Network.OutgoingPackets.DailyMissions;
using L2Dn.GameServer.Utilities;
using Microsoft.EntityFrameworkCore;
using System.Data;
using NLog;

namespace L2Dn.GameServer.Model.DailyMissions;

public class PlayerDailyMissionList
{
    private static readonly Logger _logger = LogManager.GetLogger(nameof(PlayerDailyMissionList));
    private readonly Player _owner;
    private readonly Map<int, DailyMissionPlayerEntry> _entries = new();
    private readonly object _syncRoot = new();
    private readonly Dictionary<int, long> _dirtyVersions = [];
    private long _version;
    private bool _restored;

    public PlayerDailyMissionList(Player player)
    {
        _owner = player;
    }

    internal int getOwnerId() => _owner.ObjectId;

    public int getAvailableCount()
    {
        lock (_syncRoot)
        {
            restoreLocked();
            int count = 0;
            DateTimeOffset now = DateTimeOffset.Now;
            foreach (DailyMissionDataHolder holder in DailyMissionData.getInstance().getDailyMissionData())
            {
                if (!holder.isEligible(_owner))
                {
                    continue;
                }

                DailyMissionPlayerEntry? entry = getNormalizedEntryLocked(holder, now);
                if (entry?.getStatus() == DailyMissionStatus.AVAILABLE)
                {
                    count++;
                }
            }

            return count;
        }
    }

    private void restoreLocked()
    {
        if (_restored)
        {
            return;
        }

        int characterId = _owner.ObjectId;
        try
        {
            using GameServerDbContext ctx = DbFactory.Instance.CreateDbContext();
            List<DbCharacterDailyReward> records = ctx.CharacterDailyRewards
                .Where(record => record.CharacterId == characterId)
                .AsNoTracking()
                .ToList();

            DateTimeOffset now = DateTimeOffset.Now;
            foreach (DbCharacterDailyReward record in records)
            {
                DailyMissionDataHolder? holder = DailyMissionData.getInstance().getDailyMissionData(record.RewardId);
                if (holder == null)
                {
                    continue;
                }

                DailyMissionPlayerEntry entry = new(record.CharacterId, record.RewardId, record.Status,
                    record.Progress, record.LastCompleted, record.CycleStart);
                _entries[record.RewardId] = normalizeEntryLocked(holder, entry, now);
            }

            _restored = true;
        }
        catch (Exception e)
        {
            _logger.Warn($"Error while loading daily missions for character {characterId}: {e}");
        }
    }

    public bool updateEntry(DailyMissionDataHolder holder, Action<DailyMissionPlayerEntry> update,
        bool notifyOnProgress = false, bool synchronizeOnStatus = true, DateTimeOffset? occurredAt = null)
    {
        bool synchronizeClient = false;
        lock (_syncRoot)
        {
            restoreLocked();
            if (!holder.isEligible(_owner))
            {
                return false;
            }

            DateTimeOffset now = occurredAt ?? DateTimeOffset.Now;
            DailyMissionPlayerEntry current = getNormalizedEntryLocked(holder, now) ?? createEntry(holder, now);
            DailyMissionPlayerEntry updated = current.copy();
            update(updated);
            updated.setProgress(Math.Clamp(updated.getProgress(), 0, holder.getRequiredCompletions()));

            if (entriesEqual(current, updated))
            {
                return false;
            }

            _entries[holder.getId()] = updated;
            markDirtyLocked(holder.getId());
            synchronizeClient = notifyOnProgress ||
                synchronizeOnStatus && current.getStatus() != updated.getStatus();
        }

        if (synchronizeClient)
        {
            synchronize();
        }

        return true;
    }

    public bool addProgress(DailyMissionDataHolder holder, int amount = 1, DateTimeOffset? occurredAt = null)
    {
        if (amount <= 0)
        {
            return false;
        }

        return updateEntry(holder, entry =>
        {
            if (entry.getStatus() != DailyMissionStatus.NOT_AVAILABLE)
            {
                return;
            }

            entry.setProgress(entry.getProgress() + amount);
            if (entry.getProgress() >= holder.getRequiredCompletions())
            {
                entry.setStatus(DailyMissionStatus.AVAILABLE);
            }
        }, occurredAt: occurredAt);
    }

    public bool setProgress(DailyMissionDataHolder holder, int progress)
    {
        return updateEntry(holder, entry =>
        {
            if (entry.getStatus() == DailyMissionStatus.COMPLETED)
            {
                return;
            }

            entry.setProgress(progress);
            entry.setStatus(progress >= holder.getRequiredCompletions()
                ? DailyMissionStatus.AVAILABLE
                : DailyMissionStatus.NOT_AVAILABLE);
        });
    }

    public bool makeAvailable(DailyMissionDataHolder holder, bool synchronizeClient = true)
    {
        return updateEntry(holder, entry =>
        {
            if (entry.getStatus() == DailyMissionStatus.NOT_AVAILABLE)
            {
                entry.setProgress(holder.getRequiredCompletions());
                entry.setStatus(DailyMissionStatus.AVAILABLE);
            }
        }, synchronizeOnStatus: synchronizeClient);
    }

    public bool claim(DailyMissionDataHolder holder)
    {
        lock (_syncRoot)
        {
            restoreLocked();
            if (!holder.isEligible(_owner))
            {
                return false;
            }

            DailyMissionPlayerEntry? current = getNormalizedEntryLocked(holder, DateTimeOffset.Now);
            if (current?.getStatus() != DailyMissionStatus.AVAILABLE)
            {
                return false;
            }

            DailyMissionPlayerEntry completed = current.copy();
            completed.setStatus(DailyMissionStatus.COMPLETED);
            completed.setLastCompleted(DateTime.UtcNow);
            completed.setRecentlyCompleted(true);

			// Claim is a persistence barrier: progress observed by the request must be durable before reward logic runs.
			if (!flushLocked())
			{
				return false;
			}

			return claimTransactionalLocked(holder, current, completed);
        }
    }

	private bool claimTransactionalLocked(DailyMissionDataHolder holder, DailyMissionPlayerEntry current,
		DailyMissionPlayerEntry completed)
	{
		if (holder.getRewards().Any(reward => reward.getId() is AbstractDailyMissionHandler.CLAN_EXP or
			AbstractDailyMissionHandler.MISSION_LEVEL_POINTS))
		{
			_logger.Error($"Daily mission {holder.getId()} uses a non-item reward without a transactional provider.");
			return false;
		}

		Inventory inventory = _owner.getInventory();
		// Make the DB view used by the transaction agree with any item mutations still pending in memory.
		inventory.updateDatabase();
		long requiredSlots = 0;
		long requiredWeight = 0;
		foreach (ItemHolder reward in holder.getRewards())
		{
			ItemTemplate template = ItemData.getInstance().getTemplate(reward.getId()) ??
				throw new InvalidOperationException($"Unknown daily mission reward item {reward.getId()}.");
			requiredWeight = checked(requiredWeight + template.getWeight() * reward.getCount());
			if (!template.isStackable() || inventory.getItemByItemId(reward.getId()) == null)
			{
				requiredSlots = checked(requiredSlots + (template.isStackable() ? 1 : reward.getCount()));
			}
		}

		bool deliverToInventory = inventory.validateCapacity(requiredSlots) && inventory.validateWeight(requiredWeight);
		List<DbItem> committedItems = [];
		Dictionary<int, long> committedStackIncrements = [];
		DbMailMessage? committedMail = null;
		try
		{
			using GameServerDbContext ctx = DbFactory.Instance.CreateDbContext();
			using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
				ctx.Database.BeginTransaction(IsolationLevel.Serializable);
			int characterId = _owner.ObjectId;
			int rewardId = holder.getId();
			DateTime cycleStart = GameServerDbContext.ToUtc(current.getCycleStart());
			DbCharacterDailyReward? record = ctx.CharacterDailyRewards.SingleOrDefault(entry =>
				entry.CharacterId == characterId && entry.RewardId == rewardId);
			if (record == null || record.Status != DailyMissionStatus.AVAILABLE ||
				GameServerDbContext.ToUtc(record.CycleStart) != cycleStart ||
				ctx.CharacterDailyMissionRewardGrants.Any(grant => grant.CharacterId == characterId &&
					grant.RewardId == rewardId && grant.CycleStart == cycleStart))
			{
				transaction.Rollback();
				return false;
			}

			if (deliverToInventory)
			{
				stageInventoryRewards(ctx, holder.getRewards(), committedItems, committedStackIncrements);
			}
			else
			{
				committedMail = stageMailRewards(ctx, holder.getRewards());
			}

			DateTime completedAt = DateTime.UtcNow;
			record.Status = DailyMissionStatus.COMPLETED;
			record.Progress = completed.getProgress();
			record.LastCompleted = completedAt;
			record.CycleStart = cycleStart;
			ctx.CharacterDailyMissionRewardGrants.Add(new DbCharacterDailyMissionRewardGrant
			{
				CharacterId = characterId,
				RewardId = rewardId,
				CycleStart = cycleStart,
				DeliveryKind = (byte)(deliverToInventory ? 1 : 2),
				MailMessageId = committedMail?.MessageId,
				RewardSnapshot = string.Join(';', holder.getRewards().Select(reward =>
					$"{reward.getId()}:{reward.getCount()}")),
				CreatedAt = completedAt,
				DeliveredAt = completedAt,
			});

			ctx.SaveChanges();
			transaction.Commit();
			completed.setLastCompleted(completedAt);
		}
		catch (Exception e)
		{
			_logger.Error($"Could not atomically grant daily mission {holder.getId()} to character {_owner.ObjectId}: {e}");
			return false;
		}

		_entries[holder.getId()] = completed;
		_dirtyVersions.Remove(holder.getId());
		try
		{
			if (deliverToInventory)
			{
				inventory.applyCommittedDailyMissionRewards(committedItems, committedStackIncrements, _owner);
				_owner.sendItemList();
			}
			else if (committedMail != null)
			{
				MailManager.getInstance().registerCommittedMessage(new Message(committedMail));
			}
		}
		catch (Exception e)
		{
			// The grant and items are already durable. Do not roll them back or attempt a second delivery.
			_logger.Fatal($"Daily mission {holder.getId()} was committed but could not be projected for " +
				$"character {_owner.ObjectId}; the session must be reloaded: {e}");
		}

		return true;
	}

	private void stageInventoryRewards(GameServerDbContext ctx, IEnumerable<ItemHolder> rewards,
		List<DbItem> createdItems, Dictionary<int, long> stackIncrements)
	{
		foreach (ItemHolder reward in rewards)
		{
			ItemTemplate template = ItemData.getInstance().getTemplate(reward.getId())!;
			Item? existing = template.isStackable() ? _owner.getInventory().getItemByItemId(reward.getId()) : null;
			if (existing != null)
			{
				DbItem record = ctx.Items.Single(item => item.ObjectId == existing.ObjectId &&
					item.OwnerId == _owner.ObjectId && item.Location == (int)ItemLocation.INVENTORY);
				record.Count = checked(existing.getCount() + reward.getCount());
				stackIncrements[existing.ObjectId] =
					stackIncrements.GetValueOrDefault(existing.ObjectId) + reward.getCount();
				continue;
			}

			long objectCount = template.isStackable() ? 1 : reward.getCount();
			for (long index = 0; index < objectCount; index++)
			{
				DbItem item = createRewardItem(_owner.ObjectId, reward.getId(),
					template.isStackable() ? reward.getCount() : 1, ItemLocation.INVENTORY, 0);
				ctx.Items.Add(item);
				createdItems.Add(item);
			}
		}
	}

	private DbMailMessage stageMailRewards(GameServerDbContext ctx, IEnumerable<ItemHolder> rewards)
	{
		Message message = new(_owner.ObjectId, "Daily Mission Reward",
			"Your inventory could not receive the complete reward. All items are attached to this message.",
			MailType.DAILY_MISSION_REWARD);
		DbMailMessage record = new()
		{
			MessageId = message.getId(),
			SenderId = message.getSenderId(),
			ReceiverId = message.getReceiverId(),
			Subject = message.getSubject(),
			Content = message.getContent(),
			ExpirationTime = message.getExpiration(),
			RequiredAdena = 0,
			HasAttachments = true,
			IsUnread = true,
			IsDeletedByReceiver = false,
			IsDeletedBySender = true,
			SentBySystem = (byte)MailType.DAILY_MISSION_REWARD,
			IsReturned = false,
			IsLocked = false,
			Elementals = "0;0;0;0;0;0",
		};
		ctx.MailMessages.Add(record);
		foreach (ItemHolder reward in rewards)
		{
			ItemTemplate template = ItemData.getInstance().getTemplate(reward.getId())!;
			long objectCount = template.isStackable() ? 1 : reward.getCount();
			for (long index = 0; index < objectCount; index++)
			{
				ctx.Items.Add(createRewardItem(-1, reward.getId(), template.isStackable() ? reward.getCount() : 1,
					ItemLocation.MAIL, record.MessageId));
			}
		}

		return record;
	}

	private static DbItem createRewardItem(int ownerId, int itemId, long count, ItemLocation location,
		int locationData)
	{
		return new DbItem
		{
			ObjectId = IdManager.getInstance().getNextId(),
			OwnerId = ownerId,
			ItemId = itemId,
			Count = count,
			Location = (int)location,
			LocationData = locationData,
		};
	}

    public void storeEntry(DailyMissionPlayerEntry entry)
    {
        bool synchronizeClient = false;
        lock (_syncRoot)
        {
            restoreLocked();
            DailyMissionDataHolder? holder = DailyMissionData.getInstance().getDailyMissionData(entry.getRewardId());
            if (holder == null || !holder.isEligible(_owner))
            {
                return;
            }

            DailyMissionPlayerEntry? current = getNormalizedEntryLocked(holder, DateTimeOffset.Now);
            DailyMissionPlayerEntry updated = entry.copy();
            updated.setCycleStart(holder.getCycleStartUtc(DateTimeOffset.Now));
            if (current != null)
            {
                updated.setProgress(Math.Max(current.getProgress(), updated.getProgress()));
                if (current.getStatus() == DailyMissionStatus.COMPLETED)
                {
                    updated.setStatus(DailyMissionStatus.COMPLETED);
                }
                else if (current.getStatus() == DailyMissionStatus.AVAILABLE)
                {
                    updated.setStatus(DailyMissionStatus.AVAILABLE);
                }
            }

            updated.setProgress(Math.Clamp(updated.getProgress(), 0, holder.getRequiredCompletions()));
            _entries[holder.getId()] = updated;
            markDirtyLocked(holder.getId());
            synchronizeClient = current == null || current.getStatus() != updated.getStatus();
        }

        if (synchronizeClient)
        {
            synchronize();
        }
    }

    public void store()
    {
        flush();
    }

	public bool flush()
	{
		lock (_syncRoot)
		{
			restoreLocked();
			return flushLocked();
		}
	}

    public DailyMissionStatus getStatus(int rewardId)
    {
        lock (_syncRoot)
        {
            restoreLocked();
            DailyMissionDataHolder? holder = DailyMissionData.getInstance().getDailyMissionData(rewardId);
            if (holder == null)
            {
                return DailyMissionStatus.NOT_AVAILABLE;
            }

            return getNormalizedEntryLocked(holder, DateTimeOffset.Now)?.getStatus() ??
                DailyMissionStatus.NOT_AVAILABLE;
        }
    }

    public int getProgress(int rewardId)
    {
        lock (_syncRoot)
        {
            restoreLocked();
            DailyMissionDataHolder? holder = DailyMissionData.getInstance().getDailyMissionData(rewardId);
            return holder == null
                ? 0
                : getNormalizedEntryLocked(holder, DateTimeOffset.Now)?.getProgress() ?? 0;
        }
    }

    public bool isRecentlyCompleted(int rewardId)
    {
        lock (_syncRoot)
        {
            restoreLocked();
            DailyMissionDataHolder? holder = DailyMissionData.getInstance().getDailyMissionData(rewardId);
            return holder != null &&
                (getNormalizedEntryLocked(holder, DateTimeOffset.Now)?.isRecentlyCompleted() ?? false);
        }
    }

    public DailyMissionPlayerEntry getOrCreateEntry(int rewardId)
    {
        lock (_syncRoot)
        {
            restoreLocked();
            DailyMissionDataHolder holder = DailyMissionData.getInstance().getDailyMissionData(rewardId) ??
                throw new InvalidOperationException($"Unknown daily mission id={rewardId}.");
            DailyMissionPlayerEntry entry = getNormalizedEntryLocked(holder, DateTimeOffset.Now) ??
                createEntry(holder, DateTimeOffset.Now);
            _entries[rewardId] = entry;
            return entry.copy();
        }
    }

    public DailyMissionPlayerEntry? getEntry(int rewardId)
    {
        lock (_syncRoot)
        {
            restoreLocked();
            DailyMissionDataHolder? holder = DailyMissionData.getInstance().getDailyMissionData(rewardId);
            return holder == null
                ? null
                : getNormalizedEntryLocked(holder, DateTimeOffset.Now)?.copy();
        }
    }

    public void resetExpired(DateTimeOffset now)
    {
        bool changed = false;
        lock (_syncRoot)
        {
            restoreLocked();
            foreach (DailyMissionDataHolder holder in DailyMissionData.getInstance().getDailyMissionData())
            {
                if (!_entries.TryGetValue(holder.getId(), out DailyMissionPlayerEntry? current))
                {
                    continue;
                }

                DailyMissionPlayerEntry normalized = normalizeEntryLocked(holder, current, now);
                changed |= !entriesEqual(current, normalized);
                _entries[holder.getId()] = normalized;
            }
        }

        if (changed)
        {
            synchronize();
        }
    }

    public void reset(int rewardId, bool deleteFromDb)
    {
        lock (_syncRoot)
        {
            _entries.TryRemove(rewardId, out _);
			_dirtyVersions.Remove(rewardId);
            if (!deleteFromDb)
            {
                return;
            }

            try
            {
                using GameServerDbContext ctx = DbFactory.Instance.CreateDbContext();
                ctx.CharacterDailyRewards
                    .Where(record => record.CharacterId == _owner.ObjectId && record.RewardId == rewardId)
                    .ExecuteDelete();
            }
            catch (Exception e)
            {
                _logger.Warn($"Error while deleting daily mission {rewardId} for character {_owner.ObjectId}: {e}");
            }
        }
    }

    public void synchronize()
    {
        _owner.sendPacket(new ExOneDayReceiveRewardListPacket(_owner, true));
        _owner.sendPacket(new ExConnectedTimeAndGettableRewardPacket(_owner));
    }

    private DailyMissionPlayerEntry? getNormalizedEntryLocked(DailyMissionDataHolder holder, DateTimeOffset now)
    {
        if (!_entries.TryGetValue(holder.getId(), out DailyMissionPlayerEntry? entry))
        {
            return null;
        }

        DailyMissionPlayerEntry normalized = normalizeEntryLocked(holder, entry, now);
        _entries[holder.getId()] = normalized;
        return normalized;
    }

    private DailyMissionPlayerEntry normalizeEntryLocked(DailyMissionDataHolder holder,
        DailyMissionPlayerEntry entry, DateTimeOffset now)
    {
        DateTime expectedCycleStart = holder.getCycleStartUtc(now);
        DateTime storedCycleStart = GameServerDbContext.ToUtc(entry.getCycleStart());
        if (storedCycleStart == expectedCycleStart)
        {
            return entry;
        }

        if (holder.isRecurring() && storedCycleStart > DateTime.UnixEpoch)
        {
			if (storedCycleStart > expectedCycleStart)
			{
				// An asynchronously delivered kill from the previous cycle must never roll current progress backwards.
				return entry;
			}

            DailyMissionPlayerEntry reset = createEntry(holder, now);
			markDirtyLocked(holder.getId());
			return reset;
        }

        // Legacy rows receive the current cycle without losing their state during the migration.
        DailyMissionPlayerEntry migrated = entry.copy();
        migrated.setCycleStart(expectedCycleStart);
		markDirtyLocked(holder.getId());
		return migrated;
    }

    private DailyMissionPlayerEntry createEntry(DailyMissionDataHolder holder, DateTimeOffset now)
    {
        return new DailyMissionPlayerEntry(_owner.ObjectId, holder.getId(), DailyMissionStatus.NOT_AVAILABLE, 0,
            DateTime.UnixEpoch, holder.getCycleStartUtc(now));
    }

	private void markDirtyLocked(int rewardId)
	{
		_dirtyVersions[rewardId] = ++_version;
		DailyMissionProgressManager.Instance.MarkDirty(this);
	}

	private bool flushLocked()
	{
		if (_dirtyVersions.Count == 0)
		{
			return true;
		}

		Dictionary<int, long> versions = new(_dirtyVersions);
		List<DailyMissionPlayerEntry> entries = versions.Keys
			.Where(_entries.ContainsKey)
			.Select(rewardId => _entries[rewardId].copy())
			.ToList();
		try
		{
			using GameServerDbContext ctx = DbFactory.Instance.CreateDbContext();
			using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = ctx.Database.BeginTransaction();
			int characterId = _owner.ObjectId;
			HashSet<int> rewardIds = entries.Select(entry => entry.getRewardId()).ToHashSet();
			Dictionary<int, DbCharacterDailyReward> records = ctx.CharacterDailyRewards
				.Where(record => record.CharacterId == characterId && rewardIds.Contains(record.RewardId))
				.ToDictionary(record => record.RewardId);

			foreach (DailyMissionPlayerEntry entry in entries)
			{
				if (!records.TryGetValue(entry.getRewardId(), out DbCharacterDailyReward? record))
				{
					record = new DbCharacterDailyReward
					{
						CharacterId = characterId,
						RewardId = entry.getRewardId(),
					};
					ctx.CharacterDailyRewards.Add(record);
				}

				record.Status = entry.getStatus();
				record.Progress = entry.getProgress();
				record.LastCompleted = GameServerDbContext.ToUtc(entry.getLastCompleted());
				record.CycleStart = GameServerDbContext.ToUtc(entry.getCycleStart());
			}

			ctx.SaveChanges();
			transaction.Commit();
			foreach ((int rewardId, long version) in versions)
			{
				if (_dirtyVersions.GetValueOrDefault(rewardId) == version)
				{
					_dirtyVersions.Remove(rewardId);
				}
			}

			return true;
		}
		catch (Exception e)
		{
			_logger.Warn($"Error while flushing daily missions for character {_owner.ObjectId}: {e}");
			return false;
		}
	}

    private static bool entriesEqual(DailyMissionPlayerEntry first, DailyMissionPlayerEntry second)
    {
        return first.getStatus() == second.getStatus() &&
            first.getProgress() == second.getProgress() &&
            first.getLastCompleted() == second.getLastCompleted() &&
            first.getCycleStart() == second.getCycleStart();
    }
}
