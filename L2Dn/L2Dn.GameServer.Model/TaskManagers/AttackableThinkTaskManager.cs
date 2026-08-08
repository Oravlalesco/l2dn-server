using System.Runtime.CompilerServices;
using L2Dn.GameServer.AI;
using L2Dn.GameServer.AI.Runtime;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Utilities;
using ThreadPool = L2Dn.GameServer.Utilities.ThreadPool;

namespace L2Dn.GameServer.TaskManagers;

/**
 * @author Mobius
 */
public class AttackableThinkTaskManager
{
	private static readonly Set<Set<Attackable>> POOLS = new();
	private const int POOL_SIZE = 1000;
	private const int TASK_DELAY = 1000;
	
	protected AttackableThinkTaskManager()
	{
	}
	
	private class AttackableThink: Runnable
	{
		private readonly Set<Attackable> _attackables;
		
		public AttackableThink(Set<Attackable> attackables)
		{
			_attackables = attackables;
		}
		
		public void run()
		{
			if (_attackables.isEmpty())
			{
				return;
			}
			
			CreatureAI ai;
			foreach (Attackable attackable in _attackables)
			{
				if (attackable.hasAI())
				{
					ai = attackable.getAI();
					if (ai != null)
					{
						CtrlIntention intention = ai.getIntention();
						bool measure = NpcAiTelemetry.ThinkMeasurementsEnabled;
						long startedAt = measure ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
						long allocatedBytesBefore = measure ? GC.GetAllocatedBytesForCurrentThread() : 0;
						try
						{
							ai.onEvtThink();
						}
						catch
						{
							NpcAiTelemetry.RecordThinkError(ai, intention);
							throw;
						}
						finally
						{
							if (measure)
							{
								NpcAiTelemetry.RecordThink(ai, intention, startedAt, allocatedBytesBefore);
							}
						}
					}
					else
					{
						_attackables.remove(attackable);
					}
				}
				else
				{
					_attackables.remove(attackable);
				}
			}
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
				return;
			}
		}
		
		Set<Attackable> pool1 = new();
		pool1.add(attackable);
		ThreadPool.scheduleAtFixedRate(new AttackableThink(pool1), TASK_DELAY, TASK_DELAY); // TODO: high priority task
		POOLS.add(pool1);
	}
	
	public void remove(Attackable attackable)
	{
		foreach (Set<Attackable> pool in POOLS)
		{
			if (pool.remove(attackable))
			{
				return;
			}
		}
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
