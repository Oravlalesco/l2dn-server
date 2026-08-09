using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Scheduling;

public interface INpcThinkExecutor
{
    ValueTask ExecuteAsync(NpcKey npc, NpcWakeContext context, CancellationToken cancellationToken);
}

internal interface INpcGenerationValidator
{
    bool IsCurrent(NpcKey npc);
}

internal interface INpcThinkLifecycle
{
    void Remove(NpcKey npc);
}
