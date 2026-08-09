using System.Runtime.CompilerServices;
using L2Dn.GameServer.AI;
using L2Dn.GameServer.AI.Runtime;
using L2Dn.GameServer.AI.Scheduling;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Utilities;
using NLog;
using ThreadPool = L2Dn.GameServer.Utilities.ThreadPool;

namespace L2Dn.GameServer.TaskManagers;

/**
 * @author Mobius
 */
public class AttackableThinkTaskManager
{
	private static readonly Logger LOGGER = LogManager.GetLogger(nameof(AttackableThinkTaskManager));
	private static readonly Set<Set<Attackable>> POOLS = new();
	private const int POOL_SIZE = 1000;
	private const int TASK_DELAY = 1000;
	private static int _nextPoolId;
	
	protected AttackableThinkTaskManager()
	{
	}
	
	private class AttackableThink: Runnable
	{
		private readonly Set<Attackable> _attackables;
		private readonly int _sourcePoolId;
		private long _batchSequence;
		
		public AttackableThink(Set<Attackable> attackables, int sourcePoolId)
		{
			_attackables = attackables;
			_sourcePoolId = sourcePoolId;
		}
		
		public void run()
		{
			long poolStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
			if (_attackables.isEmpty())
			{
				NpcAiTelemetry.RecordPoolIteration(_attackables.Count, poolStartedAt);
				return;
			}
			
			List<NpcPerceptionCycle> perceptionPublications = [];
			foreach (Attackable attackable in _attackables)
			{
				if (!attackable.hasAI() || attackable.getAI() == null)
				{
					_attackables.remove(attackable);
					NpcReactivity.Remove(attackable);
					continue;
				}

				NpcReactiveSchedulerMode mode = NpcReactivity.Mode;
				if (mode == NpcReactiveSchedulerMode.Enabled)
				{
					NpcReactivity.Wake(attackable, NpcWakeReason.PeriodicDue, sourcePoolId: _sourcePoolId);
					continue;
				}

				if (mode == NpcReactiveSchedulerMode.Observe)
				{
					NpcThinkCoordinator.Instance.ObservePeriodic(attackable);
				}
				else if (mode == NpcReactiveSchedulerMode.Shadow)
				{
					NpcReactivity.Wake(attackable, NpcWakeReason.PeriodicDue, sourcePoolId: _sourcePoolId);
				}

				try
				{
					NpcPerceptionCycle? perception = LegacyNpcThinkExecutor.Instance.ExecuteDirect(attackable);
					if (perception is { Publication: not NpcPerceptionPublicationKind.None })
					{
						perceptionPublications.Add(perception);
					}
				}
				catch (Exception exception)
				{
					// A single actor must not abort the remaining pool iteration.
					LOGGER.Error(exception, $"NPC think failed for object {attackable.ObjectId}; continuing pool {_sourcePoolId}.");
				}
			}

			long batchSequence = Interlocked.Increment(ref _batchSequence);
			foreach (L2Dn.NpcContracts.NpcPerceptionRegionBatch batch in
			         NpcPerceptionBatchBuilder.Build(_sourcePoolId, batchSequence, perceptionPublications))
			{
				NpcPerceptionBatchHub.Publish(batch);
			}

			NpcAiTelemetry.RecordPoolIteration(_attackables.Count, poolStartedAt);
		}
	}
	
	[MethodImpl(MethodImplOptions.Synchronized)]
	public void add(Attackable attackable)
	{
		foreach (Set<Attackable> pool in POOLS)
		{
			if (pool.Contains(attackable))
			{
				return;
			}
		}
		
		foreach (Set<Attackable> pool in POOLS)
		{
			if (pool.Count < POOL_SIZE)
			{
				pool.add(attackable);
				NpcReactivity.Wake(attackable, NpcWakeReason.Respawned);
				return;
			}
		}
		
		Set<Attackable> pool1 = new();
		pool1.add(attackable);
		int sourcePoolId = Interlocked.Increment(ref _nextPoolId);
		ThreadPool.scheduleAtFixedRate(new AttackableThink(pool1, sourcePoolId), TASK_DELAY, TASK_DELAY); // TODO: high priority task
		POOLS.add(pool1);
		NpcReactivity.Wake(attackable, NpcWakeReason.Respawned);
	}
	
	public void remove(Attackable attackable)
	{
		foreach (Set<Attackable> pool in POOLS)
		{
			if (pool.remove(attackable))
			{
				NpcPerceptionCoordinator.Instance.Remove(attackable.ObjectId);
				NpcReactivity.Remove(attackable);
				return;
			}
		}

		// Event wake-ups can create coordination state before the periodic pool registration completes.
		NpcPerceptionCoordinator.Instance.Remove(attackable.ObjectId);
		NpcReactivity.Remove(attackable);
	}

	internal Attackable[] GetAttackablesSnapshot()
	{
		return POOLS.SelectMany(static pool => pool).ToArray();
	}
	
	public static AttackableThinkTaskManager getInstance()
	{
		return SingletonHolder.INSTANCE;
	}
	
	private static class SingletonHolder
	{
		public static readonly AttackableThinkTaskManager INSTANCE = new AttackableThinkTaskManager();
	}
}
