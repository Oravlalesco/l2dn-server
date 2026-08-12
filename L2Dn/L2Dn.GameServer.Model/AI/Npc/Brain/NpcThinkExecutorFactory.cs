using L2Dn.GameServer.AI.Scheduling;
using L2Dn.NpcBrain;

namespace L2Dn.GameServer.AI.Runtime;

internal static class NpcThinkExecutorFactory
{
    public static INpcThinkExecutor Create(NpcBrainOptions options, NpcStrategyOptions strategy)
    {
        NpcBrainRuntime.Configure(options.Mode);
        NpcStrategyRuntime.Configure(strategy.EffectiveMode);
        NpcAiTelemetry.SetBrainMode(options.Mode);
        NpcAiTelemetry.SetStrategyMode(strategy.EffectiveMode);
        return options.Mode switch
        {
            NpcBrainMode.Shadow => new ShadowNpcThinkExecutor(LegacyNpcThinkExecutor.Instance,
                new NpcBrainCoordinator(), options.ReturnDefense),
            NpcBrainMode.Intent => new BrainNpcThinkExecutor(LegacyNpcThinkExecutor.Instance,
                new NpcBrainCoordinator(), NpcIntentGateway.Instance, options.ReturnDefense, strategy),
            _ => LegacyNpcThinkExecutor.Instance
        };
    }
}
