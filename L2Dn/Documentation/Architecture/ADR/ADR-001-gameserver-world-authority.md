# ADR-001: GameServer is the world authority

Status: Accepted

## Decision

GameServer exclusively owns authoritative position, HP/MP, effects, cooldowns, inventory, targets, damage, skills, movement, collision, spawning, death and client packets. External or in-process brains may observe these values but cannot write them directly.

## Consequences

Every requested state change must return to GameServer for validation and execution. Losing any AI component cannot compromise or split authoritative world state.
