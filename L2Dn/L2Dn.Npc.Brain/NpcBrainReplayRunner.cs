using System.Collections.Immutable;
using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

public sealed class NpcBrainReplayRunner
{
    private readonly Func<INpcBrain> _brainFactory;

    public NpcBrainReplayRunner(Func<INpcBrain>? brainFactory = null) =>
        _brainFactory = brainFactory ?? (() => new NpcBrainCoordinator());

    public ImmutableArray<NpcIntent> Run(IEnumerable<NpcPerceptionSnapshot> perceptions,
        NpcBrainStimulus stimuli = NpcBrainStimulus.PeriodicDue)
    {
        ArgumentNullException.ThrowIfNull(perceptions);
        INpcBrain brain = _brainFactory();
        ImmutableArray<NpcIntent>.Builder intents = ImmutableArray.CreateBuilder<NpcIntent>();
        foreach (NpcPerceptionSnapshot perception in perceptions)
        {
            intents.AddRange(brain.Decide(perception, new NpcBrainContext(stimuli)).Intents);
        }
        return intents.ToImmutable();
    }
}
