# ADR-006: NPC Brain requires a local fallback

Status: Accepted

## Decision

GameServer must remain operational indefinitely when a future remote NPC Brain is unavailable. Routing always retains a deterministic local fallback appropriate to the NPC category.

## Consequences

Remote timeouts or failures never block the game loop or disconnect players.
