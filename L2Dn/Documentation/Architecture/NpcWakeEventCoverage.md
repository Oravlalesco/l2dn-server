# NPC Wake Event Coverage

This matrix is the implementation checklist for Phase 2.5. A wake-up means only that an NPC receives an opportunity to run its existing `AttackableAI`; the AI remains responsible for deciding whether the event matters.

| Event | Source reliability | Volume | Coalescing key | Initial rollout |
|---|---|---:|---|---|
| Periodic due | Exact | One per scheduled NPC/pool tick | `NpcKey` | Enabled-mode integration |
| Attacked | Exact | Combat hot path | `NpcKey`, reason flag | First reactive event |
| Threat changed | Exact for `EVT_AGGRESSION` | Potentially high in assists | `NpcKey`, reason flag | First reactive event |
| Target lost/dead | Exact when current target is forgotten | Moderate | `NpcKey`, reason flag | First reactive event |
| Player became relevant on spawn | Reuses current visibility traversal | Spawn/teleport | `NpcKey`, reason flag | Feature flagged |
| Player became relevant on region crossing | Reuses newly surrounding regions from `World.switchRegion` | Lower than movement packets | `NpcKey`, reason flag | Feature flagged after combat |
| Region activated | Exact region transition | Low burst | `NpcKey`, reason flag | Normal priority |
| Respawned | Exact lifecycle event but AI registration ordering matters | Low | `NpcKey`, reason flag | Wake from scheduler registration |
| Action ready / explicit think event | Exact legacy `EVT_THINK` source | Moderate | `NpcKey`, reason flag | Routed through coordinator only in Enabled mode |
| Ally attacked | Currently propagated as aggression/minion assist | Raid burst | `NpcKey`, reason flag | Classified as threat change |
| Combat ended | No single reliable source | Low | N/A | Deferred |

Explicit non-sources:

- Every player movement packet: prohibited because it creates event storms.
- A second visibility/spatial scan: prohibited because `World` already computes relevant region transitions.
- Polling all NPCs from an event handler: prohibited.
- `Task.Run` per wake-up: prohibited.
