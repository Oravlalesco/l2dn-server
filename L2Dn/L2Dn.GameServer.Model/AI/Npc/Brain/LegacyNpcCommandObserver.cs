using System.Collections.Immutable;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Instances;
using L2Dn.GameServer.Model.Skills;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

internal static class LegacyNpcCommandObserver
{
    private static readonly AsyncLocal<Scope?> Current = new();

    public static Scope Begin(NpcPerceptionSnapshot perception)
    {
        Scope scope = new(perception, Current.Value);
        Current.Value = scope;
        return scope;
    }

    public static void RecordSetIntention(AbstractAI ai, CtrlIntention intention, object? argument)
    {
        Scope? scope = Current.Value;
        if (scope == null || ai.getActor().ObjectId != scope.Actor.ObjectId)
        {
            return;
        }

        if (intention == CtrlIntention.AI_INTENTION_ATTACK && argument is Creature target)
        {
            scope.Add(new AcquireTargetIntent(scope.Envelope(NpcIntentType.AcquireTarget), ToEntityKey(target)));
        }
        else if (intention == CtrlIntention.AI_INTENTION_ACTIVE && scope.Perception.State.Combat.CurrentTarget != null)
        {
            scope.Add(new StopCombatIntent(scope.Envelope(NpcIntentType.StopCombat)));
        }
    }

    public static void RecordMove(AbstractAI ai)
    {
        Scope? scope = Current.Value;
        EntityKey? target = scope?.Perception.State.Combat.CurrentTarget;
        if (scope != null && target.HasValue && ai.getActor().ObjectId == scope.Actor.ObjectId)
        {
            scope.Add(new ApproachTargetIntent(scope.Envelope(NpcIntentType.ApproachTarget), target.Value,
                scope.Perception.State.Combat.PhysicalAttackRange));
        }
    }

    public static void RecordTarget(Creature actor, WorldObject? target)
    {
        Scope? scope = Current.Value;
        if (scope == null || actor.ObjectId != scope.Actor.ObjectId)
        {
            return;
        }

        scope.Add(target == null
            ? new ClearTargetIntent(scope.Envelope(NpcIntentType.ClearTarget),
                scope.Perception.State.Combat.CurrentTarget)
            : new AcquireTargetIntent(scope.Envelope(NpcIntentType.AcquireTarget), ToEntityKey(target)));
    }

    public static void RecordReturnHome(Attackable actor)
    {
        Scope? scope = Current.Value;
        if (scope != null && actor.ObjectId == scope.Actor.ObjectId)
        {
            scope.Add(new ReturnHomeIntent(scope.Envelope(NpcIntentType.ReturnHome)));
        }
    }

    public static void RecordAutoAttack(Creature actor, Creature target)
    {
        Scope? scope = Current.Value;
        if (scope != null && actor.ObjectId == scope.Actor.ObjectId)
        {
            scope.Add(new BasicAttackIntent(scope.Envelope(NpcIntentType.BasicAttack), ToEntityKey(target)));
        }
    }

    public static void RecordCast(Creature actor, Skill skill)
    {
        Scope? scope = Current.Value;
        if (scope != null && actor.ObjectId == scope.Actor.ObjectId)
        {
            EntityKey? target = actor.getTarget() is { } liveTarget ? ToEntityKey(liveTarget) : null;
            scope.Add(new CastSkillIntent(scope.Envelope(NpcIntentType.CastSkill), skill.getId(), skill.getLevel(),
                target));
        }
    }

    public static void RecordStopCombat(Creature actor)
    {
        Scope? scope = Current.Value;
        if (scope != null && actor.ObjectId == scope.Actor.ObjectId)
        {
            scope.Add(new StopCombatIntent(scope.Envelope(NpcIntentType.StopCombat)));
        }
    }

    private static EntityKey ToEntityKey(WorldObject entity) => new(
        entity.ObjectId,
        entity is Npc npc ? npc.getSpawnGeneration() : 0,
        entity switch
        {
            Player => EntityKind.Player,
            Summon => EntityKind.Summon,
            Guard => EntityKind.Guard,
            Monster => EntityKind.Monster,
            Npc => EntityKind.Npc,
            Creature => EntityKind.OtherCreature,
            _ => EntityKind.Unknown
        });

    internal sealed class Scope: IDisposable
    {
        private readonly Scope? _previous;
        private readonly List<NpcIntent> _intents = [];
        private long _sequence;
        private int _disposed;

        internal Scope(NpcPerceptionSnapshot perception, Scope? previous)
        {
            Perception = perception;
            Actor = perception.Envelope.Npc;
            _previous = previous;
        }

        public NpcPerceptionSnapshot Perception { get; }
        public NpcKey Actor { get; }
        public ImmutableArray<NpcIntent> Intents => [.. _intents];

        internal NpcIntentEnvelope Envelope(NpcIntentType type) => new(
            NpcIntent.CurrentSchemaVersion, Actor, Perception.Envelope.StateRevision,
            ++_sequence, type);

        internal void Add(NpcIntent intent) => _intents.Add(intent);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Current.Value = _previous;
            }
        }
    }
}
