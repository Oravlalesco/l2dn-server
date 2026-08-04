using L2Dn.GameServer.Db;

namespace L2Dn.GameServer.Model;

public class DailyMissionPlayerEntry
{
    private readonly int _objectId;
    private readonly int _rewardId;
    private DailyMissionStatus _status = DailyMissionStatus.NOT_AVAILABLE;
    private int _progress;
    private DateTime _lastCompleted;
    private DateTime _cycleStart;
    private bool _recentlyCompleted;
	
    public DailyMissionPlayerEntry(int objectId, int rewardId)
    {
        _objectId = objectId;
        _rewardId = rewardId;
    }
	
    public DailyMissionPlayerEntry(int objectId, int rewardId, DailyMissionStatus status, int progress,
        DateTime lastCompleted, DateTime cycleStart):this(objectId, rewardId)
    {
        _status = status;
        _progress = progress;
        _lastCompleted = lastCompleted;
        _cycleStart = cycleStart;
    }
	
    public int getObjectId()
    {
        return _objectId;
    }
	
    public int getRewardId()
    {
        return _rewardId;
    }
	
    public DailyMissionStatus getStatus()
    {
        return _status;
    }
	
    public void setStatus(DailyMissionStatus status)
    {
        _status = status;
    }
	
    public int getProgress()
    {
        return _progress;
    }
	
    public void setProgress(int progress)
    {
        _progress = progress;
    }
	
    public int increaseProgress()
    {
        _progress++;
        return _progress;
    }
	
    public DateTime getLastCompleted()
    {
        return _lastCompleted;
    }
	
    public void setLastCompleted(DateTime lastCompleted)
    {
        _lastCompleted = lastCompleted;
    }

    public DateTime getCycleStart()
    {
        return _cycleStart;
    }

    public void setCycleStart(DateTime cycleStart)
    {
        _cycleStart = cycleStart;
    }
	
    public bool isRecentlyCompleted()
    {
        return _recentlyCompleted;
    }
	
    public void setRecentlyCompleted(bool recentlyCompleted)
    {
        _recentlyCompleted = recentlyCompleted;
    }

    public DailyMissionPlayerEntry copy()
    {
        DailyMissionPlayerEntry copy = new(_objectId, _rewardId, _status, _progress, _lastCompleted, _cycleStart);
        copy.setRecentlyCompleted(_recentlyCompleted);
        return copy;
    }
}
