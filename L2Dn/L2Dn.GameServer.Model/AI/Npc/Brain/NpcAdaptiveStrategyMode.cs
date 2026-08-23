namespace L2Dn.GameServer.AI.Runtime;

/// <summary>
/// Controls the rollout mode of the Phase 4B.5 Stateful Strategic Utility system.
/// Follows the same Disabled/Shadow/Enabled pattern as <see cref="NpcStrategyMode"/>.
///
/// <list type="bullet">
///   <item><see cref="Disabled"/> — behavior is identical to Phase 4B (Static Strategy V1). This is the
///     only supported mode in 4B.5.0 (baseline). Any later subfase that causes Disabled to diverge from
///     Phase 4B is a regression detected by AdaptiveBaselineTests.</item>
///   <item><see cref="Shadow"/> — Phase 4B.5 evaluator runs in parallel; result is logged but NOT applied.
///     Introduced in 4B.5.12. Requesting Shadow before that subfase falls back to Disabled with a warning.</item>
///   <item><see cref="Enabled"/> — Phase 4B.5 evaluator drives gameplay.
///     Introduced in 4B.5.14 (R1–R5) and rolled out in waves. Requesting Enabled before that subfase
///     falls back to Disabled with a warning.</item>
/// </list>
/// </summary>
public enum NpcAdaptiveStrategyMode
{
    Disabled = 0,
    Shadow = 1,
    Enabled = 2
}
