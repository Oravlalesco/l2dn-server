using L2Dn.GameServer.AI.Scheduling;
using L2Dn.NpcBrain;

namespace L2Dn.GameServer.AI.Runtime;

internal static class NpcThinkExecutorFactory
{
    public static INpcThinkExecutor Create(NpcBrainOptions options)
    {
        NpcBrainRuntime.Configure(options.Mode);
        NpcAiTelemetry.SetBrainMode(options.Mode);
        return options.Mode switch
        {
            NpcBrainMode.Shadow => new ShadowNpcThinkExecutor(LegacyNpcThinkExecutor.Instance,
                new NpcBrainCoordinator(), options.ReturnDefense),
            NpcBrainMode.Intent => new BrainNpcThinkExecutor(LegacyNpcThinkExecutor.Instance,
                new NpcBrainCoordinator(), NpcIntentGateway.Instance, options.ReturnDefense),
            _ => LegacyNpcThinkExecutor.Instance
        };
    }
}
