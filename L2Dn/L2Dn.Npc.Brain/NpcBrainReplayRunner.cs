using System.Collections.Immutable;
using System.Diagnostics;
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

    public ImmutableArray<StrategyReplayResult> RunComparative(
        IEnumerable<NpcPerceptionSnapshot> perceptions,
        NpcBrainStimulus stimuli = NpcBrainStimulus.PeriodicDue)
    {
        ArgumentNullException.ThrowIfNull(perceptions);
        NpcStrategyProfile[] profiles =
        [
            NpcStrategyProfileResolver.Balanced,
            NpcStrategyProfileResolver.AggressivePressure,
            NpcStrategyProfileResolver.RangedControl,
            NpcStrategyProfileResolver.Survival
        ];
        Dictionary<NpcStrategyArchetype, NpcBrainCoordinator> brains = profiles.ToDictionary(
            static profile => profile.Archetype, static _ => new NpcBrainCoordinator());
        ImmutableArray<StrategyReplayResult>.Builder results =
            ImmutableArray.CreateBuilder<StrategyReplayResult>();

        foreach (NpcPerceptionSnapshot perception in perceptions)
        {
            foreach (NpcStrategyProfile profile in profiles)
            {
                long startedAt = Stopwatch.GetTimestamp();
                NpcStrategyBrainEvaluation evaluation = brains[profile.Archetype]
                    .DecideWithStrategyDiagnostics(perception, new NpcBrainContext(stimuli), profile);
                TimeSpan duration = Stopwatch.GetElapsedTime(startedAt);
                NpcBrainDecision decision = evaluation.Decision;
                NpcStrategyDecisionSummary summary = decision.StrategyDecision ??
                    new NpcStrategyDecisionSummary(profile.Archetype, NpcStrategyAction.None,
                        NpcStrategyDecisionReason.None, NpcStrategyModifierFlags.None);
                results.Add(new StrategyReplayResult(
                    perception.Envelope.StateRevision,
                    profile.Archetype,
                    summary.SelectedAction,
                    decision.Intents.FirstOrDefault(),
                    evaluation.Diagnostics.CandidateScores,
                    evaluation.Diagnostics.AppliedModifiers,
                    summary.Reason,
                    duration));
            }
        }

        return results.ToImmutable();
    }
}
