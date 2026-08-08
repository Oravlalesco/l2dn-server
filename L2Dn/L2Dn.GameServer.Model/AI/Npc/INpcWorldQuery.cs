using L2Dn.GameServer.Model;

namespace L2Dn.GameServer.AI.Runtime;

internal interface INpcWorldQuery
{
    List<T> GetVisibleObjects<T>(WorldObject source)
        where T: WorldObject;

    List<T> GetVisibleObjectsInRange<T>(WorldObject source, int range)
        where T: WorldObject;

    void ForEachVisibleObjectInRange<T>(WorldObject source, int range, Action<T> action)
        where T: WorldObject;

    int GetWorldTick();
}
