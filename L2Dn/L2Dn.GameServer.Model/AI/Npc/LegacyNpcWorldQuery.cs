using L2Dn.GameServer.Model;
using L2Dn.GameServer.TaskManagers;

namespace L2Dn.GameServer.AI.Runtime;

internal sealed class LegacyNpcWorldQuery: INpcWorldQuery
{
    public static LegacyNpcWorldQuery Instance { get; } = new();

    private LegacyNpcWorldQuery()
    {
    }

    public List<T> GetVisibleObjects<T>(WorldObject source)
        where T: WorldObject =>
        NpcAiTelemetry.ObserveWorldQuery("get_visible", typeof(T),
            () => World.getInstance().getVisibleObjects<T>(source));

    public List<T> GetVisibleObjectsInRange<T>(WorldObject source, int range)
        where T: WorldObject =>
        NpcAiTelemetry.ObserveWorldQuery("get_visible_in_range", typeof(T),
            () => World.getInstance().getVisibleObjectsInRange<T>(source, range));

    public void ForEachVisibleObjectInRange<T>(WorldObject source, int range, Action<T> action)
        where T: WorldObject =>
        NpcAiTelemetry.ObserveWorldQuery("for_each_visible_in_range", typeof(T), () =>
        {
            World.getInstance().forEachVisibleObjectInRange(source, range, action);
            return true;
        });

    public int GetWorldTick() => GameTimeTaskManager.getInstance().getGameTicks();
}
