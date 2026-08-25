using System.Collections.Immutable;
using L2Dn.GameServer.AI.Scheduling;
using L2Dn.NpcBrain;

namespace L2Dn.GameServer.AI.Runtime;

internal sealed record NpcStrategyOptions(
    NpcStrategyMode ConfiguredMode,
    NpcStrategyMode EffectiveMode,
    NpcStrategyTemplateRegistry Registry,
    NpcAdaptiveStrategyMode AdaptiveMode)
{
    public static NpcStrategyOptions FromEnvironment(NpcBrainMode brainMode,
        NpcReactiveSchedulerMode reactiveMode, Func<string, string?>? read = null,
        Action<string>? warning = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        NpcStrategyMode configured = ParseMode(read("NPC_STRATEGY_MODE"), warning);
        bool supported = reactiveMode == NpcReactiveSchedulerMode.Enabled &&
            brainMode == NpcBrainMode.Intent;
        NpcStrategyMode effective = supported ? configured : NpcStrategyMode.Disabled;
        if (!supported && configured != NpcStrategyMode.Disabled)
        {
            warning?.Invoke($"NPC Strategy {configured} requires Reactive=Enabled and Brain=Intent; Strategy is disabled.");
        }

        NpcStrategyTemplateRegistry registry = effective == NpcStrategyMode.Disabled
            ? NpcStrategyTemplateRegistry.Empty
            : NpcStrategyTemplateRegistry.Parse(read("NPC_STRATEGY_TEMPLATE_PROFILES"), warning);

        // 4B.5.0: parse the adaptive mode env var but only honor Disabled.
        // Shadow and Enabled will be activated in 4B.5.12 and 4B.5.14 respectively.
        NpcAdaptiveStrategyMode adaptiveMode = ParseAdaptiveMode(read("NPC_STRATEGY_ADAPTIVE_MODE"), warning);

        return new NpcStrategyOptions(configured, effective, registry, adaptiveMode);
    }

    private static NpcStrategyMode ParseMode(string? value, Action<string>? warning)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return NpcStrategyMode.Disabled;
        }
        string normalized = value.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal).Trim();
        if (Enum.TryParse(normalized, true, out NpcStrategyMode mode))
        {
            return mode;
        }
        warning?.Invoke($"Unknown NPC_STRATEGY_MODE '{value}'; Strategy is disabled.");
        return NpcStrategyMode.Disabled;
    }

    /// <summary>
    /// Parses NPC_STRATEGY_ADAPTIVE_MODE. In Phase 4B.5.0 only Disabled is active;
    /// requesting Shadow or Enabled logs a warning and falls back to Disabled.
    /// Shadow support: 4B.5.12. Enabled support: 4B.5.14.
    /// </summary>
    private static NpcAdaptiveStrategyMode ParseAdaptiveMode(string? value, Action<string>? warning)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return NpcAdaptiveStrategyMode.Disabled;
        }
        string normalized = value.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal).Trim();
        if (!Enum.TryParse(normalized, true, out NpcAdaptiveStrategyMode mode))
        {
            warning?.Invoke($"Unknown NPC_STRATEGY_ADAPTIVE_MODE '{value}'; Adaptive Strategy is disabled.");
            return NpcAdaptiveStrategyMode.Disabled;
        }
        if (mode != NpcAdaptiveStrategyMode.Disabled)
        {
            // Shadow activates in 4B.5.12; Enabled in 4B.5.14.
            warning?.Invoke($"NPC_STRATEGY_ADAPTIVE_MODE={mode} is not yet supported in this build; Adaptive Strategy is disabled.");
            return NpcAdaptiveStrategyMode.Disabled;
        }
        return NpcAdaptiveStrategyMode.Disabled;
    }
}

internal sealed class NpcStrategyTemplateRegistry
{
    private readonly ImmutableDictionary<int, NpcStrategyProfile> _profiles;

    public static NpcStrategyTemplateRegistry Empty { get; } = new(
        ImmutableDictionary<int, NpcStrategyProfile>.Empty);

    private NpcStrategyTemplateRegistry(ImmutableDictionary<int, NpcStrategyProfile> profiles) =>
        _profiles = profiles;

    public int Count => _profiles.Count;

    public bool TryResolve(int templateId, out NpcStrategyProfile profile, out bool usedRuntimeFallback)
    {
        if (!_profiles.TryGetValue(templateId, out NpcStrategyProfile? configured))
        {
            profile = null!;
            usedRuntimeFallback = false;
            return false;
        }
        profile = NpcStrategyProfileResolver.ResolveArchetype(configured.Archetype,
            out usedRuntimeFallback);
        return true;
    }

    public static NpcStrategyTemplateRegistry Parse(string? value, Action<string>? warning = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Empty;
        }

        ImmutableDictionary<int, NpcStrategyProfile>.Builder profiles =
            ImmutableDictionary.CreateBuilder<int, NpcStrategyProfile>();
        foreach (string entry in value.Split(',', StringSplitOptions.TrimEntries |
                     StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = entry.Split(':', StringSplitOptions.TrimEntries);
            if (fields.Length != 2 || !int.TryParse(fields[0], out int templateId) || templateId <= 0)
            {
                warning?.Invoke($"Rejected malformed NPC Strategy template entry '{entry}'.");
                continue;
            }
            if (!TryParseProfile(fields[1], out NpcStrategyProfile profile))
            {
                warning?.Invoke($"Rejected unknown NPC Strategy profile '{fields[1]}' for template {templateId}.");
                continue;
            }
            if (profiles.ContainsKey(templateId))
            {
                warning?.Invoke($"Duplicate NPC Strategy template {templateId}; the last entry wins.");
            }
            profiles[templateId] = profile;
        }
        return profiles.Count == 0
            ? Empty
            : new NpcStrategyTemplateRegistry(profiles.ToImmutable());
    }

    private static bool TryParseProfile(string value, out NpcStrategyProfile profile)
    {
        string normalized = value.Trim().Replace('-', '_').ToLowerInvariant();
        NpcStrategyProfile? parsed = normalized switch
        {
            "balanced" => NpcStrategyProfileResolver.Balanced,
            "aggressive_pressure" => NpcStrategyProfileResolver.AggressivePressure,
            "ranged_control" => NpcStrategyProfileResolver.RangedControl,
            "survival" => NpcStrategyProfileResolver.Survival,
            _ => null
        };
        profile = parsed!;
        return parsed != null;
    }
}
