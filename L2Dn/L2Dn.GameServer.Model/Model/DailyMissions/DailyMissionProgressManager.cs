using System.Collections.Concurrent;
using NLog;
using ThreadPool = L2Dn.GameServer.Utilities.ThreadPool;

namespace L2Dn.GameServer.Model.DailyMissions;

public sealed class DailyMissionProgressManager
{
    private static readonly Logger _logger = LogManager.GetLogger(nameof(DailyMissionProgressManager));
    private static readonly Lazy<DailyMissionProgressManager> _instance = new(() => new DailyMissionProgressManager());

    private readonly ConcurrentDictionary<int, WeakReference<PlayerDailyMissionList>> _lists = new();

    private DailyMissionProgressManager()
    {
        ThreadPool.scheduleAtFixedRate(FlushDirty, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    public static DailyMissionProgressManager Instance => _instance.Value;

    public void MarkDirty(PlayerDailyMissionList list)
    {
        _lists[list.getOwnerId()] = new WeakReference<PlayerDailyMissionList>(list);
    }

    public void FlushDirty()
    {
        foreach ((int characterId, WeakReference<PlayerDailyMissionList> reference) in _lists)
        {
            if (!reference.TryGetTarget(out PlayerDailyMissionList? list))
            {
                _lists.TryRemove(characterId, out _);
                continue;
            }

            try
            {
                list.flush();
            }
            catch (Exception e)
            {
                // The list retains its dirty versions, so the next tick retries without losing progress.
                _logger.Warn($"Could not flush daily mission progress for character {characterId}: {e}");
            }
        }
    }

    public void FlushAll()
    {
        FlushDirty();
    }
}
