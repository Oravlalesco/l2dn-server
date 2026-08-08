# ADR-009: Partition by instance and region affinity

Status: Accepted

## Decision

NPC scheduling and future worker routing use `(InstanceId, WorldRegion)` as the partition key rather than distributing individual NPCs randomly.

## Consequences

Interacting NPCs and nearby players remain colocated, and regions can later be reassigned between workers without changing contracts.
