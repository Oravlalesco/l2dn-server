using L2Dn.Extensions;
using L2Dn.Utilities;

namespace L2Dn.GameServer.AI.Runtime;

internal sealed class ProductionNpcRandomSource: INpcRandomSource
{
    public static ProductionNpcRandomSource Instance { get; } = new();

    private ProductionNpcRandomSource()
    {
    }

    public int Next(int maxExclusive) => Rnd.get(maxExclusive);

    public int Next(int minInclusive, int maxExclusive) => Rnd.get(minInclusive, maxExclusive);

    public bool NextBoolean() => Rnd.nextBoolean();

    public T Pick<T>(IReadOnlyList<T> values) => values.GetRandomElement();

    public T? PickOrDefault<T>(IReadOnlyList<T> values) => values.GetRandomElementOrDefault();
}
