using L2Dn.GameServer.Geo;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.InstanceZones;
using L2Dn.Geometry;

namespace L2Dn.GameServer.AI.Runtime;

internal sealed class LegacyNpcGeoQuery: INpcGeoQuery
{
    public static LegacyNpcGeoQuery Instance { get; } = new();

    private readonly Func<WorldObject, WorldObject, bool> _canSeeTarget;
    private readonly Func<Location3D, Location3D, Instance?, bool> _canMoveToTarget;
    private readonly Func<Location3D, Location3D, Instance?, Location3D> _getValidLocation;

    private LegacyNpcGeoQuery(): this(
        static (source, target) => GeoEngine.getInstance().canSeeTarget(source, target),
        static (source, target, instance) => GeoEngine.getInstance().canMoveToTarget(source, target, instance),
        static (source, target, instance) => GeoEngine.getInstance().getValidLocation(source, target, instance))
    {
    }

    internal LegacyNpcGeoQuery(
        Func<WorldObject, WorldObject, bool> canSeeTarget,
        Func<Location3D, Location3D, Instance?, bool> canMoveToTarget,
        Func<Location3D, Location3D, Instance?, Location3D> getValidLocation)
    {
        _canSeeTarget = canSeeTarget;
        _canMoveToTarget = canMoveToTarget;
        _getValidLocation = getValidLocation;
    }

    public bool CanSeeTarget(WorldObject source, WorldObject target) =>
        NpcAiTelemetry.ObserveGeoQuery("can_see", () => _canSeeTarget(source, target),
            static result => result ? "allowed" : "blocked");

    public bool CanMoveToTarget(Location3D source, Location3D target, Instance? instance) =>
        NpcAiTelemetry.ObserveGeoQuery("can_move", () => _canMoveToTarget(source, target, instance),
            static result => result ? "allowed" : "blocked");

    public Location3D GetValidLocation(Location3D source, Location3D target, Instance? instance) =>
        NpcAiTelemetry.ObserveGeoQuery("get_valid_location",
            () => _getValidLocation(source, target, instance), static _ => "success");
}
