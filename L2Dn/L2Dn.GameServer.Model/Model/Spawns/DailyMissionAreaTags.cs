using System.Collections.Immutable;

namespace L2Dn.GameServer.Model.Spawns;

public static class DailyMissionAreaTags
{
    public static ImmutableHashSet<string> Resolve(IEnumerable<string>? inherited, string? included,
        string? excluded)
    {
        ImmutableHashSet<string>.Builder areas = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
        if (inherited != null)
        {
            areas.UnionWith(inherited);
        }

        areas.UnionWith(Parse(included));
        areas.ExceptWith(Parse(excluded));
        return areas.ToImmutable();
    }

    public static IEnumerable<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (string part in value.Split(','))
        {
            string area = part.Trim();
            if (!string.IsNullOrEmpty(area))
            {
                yield return area;
            }
        }
    }
}
