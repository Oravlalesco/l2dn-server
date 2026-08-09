namespace L2Dn.NpcContracts;

public readonly record struct NpcKey(int ObjectId, int Generation);

public readonly record struct EntityKey(int ObjectId, int Generation, EntityKind Kind);

public readonly record struct RegionKey(int InstanceId, int RegionX, int RegionY);

public readonly record struct NpcPosition(int X, int Y, int Z, int Heading);

public readonly record struct VisibleEntity(
    int ObservationOrdinal,
    EntityKey Entity,
    NpcPosition Position,
    int Level,
    double CollisionRadius,
    double CollisionHeight,
    double Distance2D,
    EntityStateFlags State,
    EntityRelationFlags Relations);

public readonly record struct ThreatEntry(
    EntityKey Target,
    long Hate,
    long Damage,
    double Distance2D,
    bool Visible,
    bool ValidTarget);

public readonly record struct NpcAffordanceObservation(EntityKey Target, NpcAffordanceFlags Affordances);

public readonly record struct SpatialObservation(EntityKey Target, SpatialObservationFlags Spatial);
