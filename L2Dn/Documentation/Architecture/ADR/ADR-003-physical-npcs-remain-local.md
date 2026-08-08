# ADR-003: Physical NPCs remain in GameServer initially

Status: Accepted

## Decision

`Npc`, `Attackable` and their mutable physical state remain inside GameServer during the initial modernization phases. The brain receives copied perception data instead of remotely owning entities.

## Consequences

The project avoids continuous synchronization of thousands of mutable objects and can extract decision-making independently from simulation ownership.
