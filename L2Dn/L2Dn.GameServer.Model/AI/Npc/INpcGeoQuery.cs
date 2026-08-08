using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.InstanceZones;
using L2Dn.Geometry;

namespace L2Dn.GameServer.AI.Runtime;

internal interface INpcGeoQuery
{
    bool CanSeeTarget(WorldObject source, WorldObject target);

    bool CanMoveToTarget(Location3D source, Location3D target, Instance? instance);

    Location3D GetValidLocation(Location3D source, Location3D target, Instance? instance);
}
