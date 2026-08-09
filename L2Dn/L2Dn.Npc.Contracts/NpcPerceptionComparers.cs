namespace L2Dn.NpcContracts;

public sealed class NpcPerceptionStateComparer: IEqualityComparer<NpcPerceptionState>
{
    public static NpcPerceptionStateComparer Instance { get; } = new();

    private NpcPerceptionStateComparer()
    {
    }

    public bool Equals(NpcPerceptionState? x, NpcPerceptionState? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return IdentityEquals(x.Identity, y.Identity) &&
               x.Physical == y.Physical &&
               x.Combat == y.Combat &&
               x.Environment == y.Environment &&
               x.VisibleEntities.SequenceEqual(y.VisibleEntities) &&
               x.Threats.SequenceEqual(y.Threats) &&
               x.Affordances.SequenceEqual(y.Affordances) &&
               x.SpatialObservations.SequenceEqual(y.SpatialObservations) &&
               x.Skills.SequenceEqual(y.Skills);
    }

    public int GetHashCode(NpcPerceptionState obj)
    {
        HashCode hash = new();
        hash.Add(obj.Identity.TemplateId);
        hash.Add(obj.Identity.Kind);
        hash.Add(obj.Identity.LegacyAiType);
        hash.Add(obj.Identity.Level);
        hash.Add(obj.Identity.ClanHelpRange);
        hash.Add(obj.Identity.Capabilities);
        AddValues(ref hash, obj.Identity.ClanIds);
        hash.Add(obj.Physical);
        hash.Add(obj.Combat);
        hash.Add(obj.Environment);
        AddValues(ref hash, obj.VisibleEntities);
        AddValues(ref hash, obj.Threats);
        AddValues(ref hash, obj.Affordances);
        AddValues(ref hash, obj.SpatialObservations);
        AddValues(ref hash, obj.Skills);
        return hash.ToHashCode();
    }

    private static bool IdentityEquals(NpcIdentity x, NpcIdentity y) =>
        x.TemplateId == y.TemplateId && x.Kind == y.Kind && x.LegacyAiType == y.LegacyAiType &&
        x.Level == y.Level && x.ClanHelpRange == y.ClanHelpRange && x.Capabilities == y.Capabilities &&
        x.ClanIds.SequenceEqual(y.ClanIds);

    private static void AddValues<T>(ref HashCode hash, IEnumerable<T> values)
    {
        foreach (T value in values)
        {
            hash.Add(value);
        }
    }
}

public sealed class NpcPerceptionExactComparer: IEqualityComparer<NpcPerceptionSnapshot>
{
    public static NpcPerceptionExactComparer Instance { get; } = new();

    private NpcPerceptionExactComparer()
    {
    }

    public bool Equals(NpcPerceptionSnapshot? x, NpcPerceptionSnapshot? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        return x is not null && y is not null && x.Envelope == y.Envelope &&
               NpcPerceptionStateComparer.Instance.Equals(x.State, y.State);
    }

    public int GetHashCode(NpcPerceptionSnapshot obj) =>
        HashCode.Combine(obj.Envelope, NpcPerceptionStateComparer.Instance.GetHashCode(obj.State));
}
