namespace L2Dn.GameServer.AI.Runtime;

internal interface INpcRandomSource
{
    int Next(int maxExclusive);

    int Next(int minInclusive, int maxExclusive);

    bool NextBoolean();

    T Pick<T>(IReadOnlyList<T> values);

    T? PickOrDefault<T>(IReadOnlyList<T> values);
}
