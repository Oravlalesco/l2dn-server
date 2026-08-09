using L2Dn.GameServer.Model.Actor;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

internal static class NpcPerceptionValidator
{
    public static void Validate(Attackable actor, NpcPerceptionSnapshot snapshot)
    {
        if (snapshot.Envelope.SchemaVersion != NpcPerceptionSnapshot.CurrentSchemaVersion)
            NpcAiTelemetry.RecordPerceptionValidationMismatch("schema", "structural");
        if (snapshot.Envelope.Npc.ObjectId != actor.ObjectId)
            NpcAiTelemetry.RecordPerceptionValidationMismatch("object_id", "structural");
        if (snapshot.Envelope.Npc.Generation != actor.getSpawnGeneration())
            NpcAiTelemetry.RecordPerceptionValidationMismatch("generation", "structural");
        if (snapshot.State.VisibleEntities.IsDefault)
            NpcAiTelemetry.RecordPerceptionValidationMismatch("visible_default", "structural");
        if (snapshot.State.Threats.IsDefault)
            NpcAiTelemetry.RecordPerceptionValidationMismatch("threats_default", "structural");

        // These comparisons are diagnostic only: the world may mutate immediately after capture.
        if (snapshot.State.Physical.Position.X != actor.getX() ||
            snapshot.State.Physical.Position.Y != actor.getY() ||
            snapshot.State.Physical.Position.Z != actor.getZ())
            NpcAiTelemetry.RecordPerceptionValidationMismatch("position", "temporal");
        if (snapshot.State.Combat.CurrentTarget?.ObjectId != actor.getTarget()?.ObjectId)
            NpcAiTelemetry.RecordPerceptionValidationMismatch("target", "temporal");
    }
}
