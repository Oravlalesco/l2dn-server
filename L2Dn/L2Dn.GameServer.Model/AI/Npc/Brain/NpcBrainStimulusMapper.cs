using L2Dn.GameServer.AI.Scheduling;
using L2Dn.NpcBrain;

namespace L2Dn.GameServer.AI.Runtime;

internal static class NpcBrainStimulusMapper
{
    public static NpcBrainStimulus Map(NpcWakeReason reasons) => (NpcBrainStimulus)(int)reasons;
}
