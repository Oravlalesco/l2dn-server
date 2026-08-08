# ADR-011: Entity incarnation semantics

Status: Accepted

## Decision

NPC references include object ID, entity kind and exact spawn generation. Other actors use generation zero during Phase 2 because the legacy model has no shared incarnation counter for them.

A generation greater than zero must match exactly during legacy resolution. Generation zero is best effort: resolution also validates current entity kind and instance, and the key alone is never authorization for a world mutation.

## Consequences

NPC respawns cannot be confused with an earlier incarnation. Player and non-NPC incarnation remains explicit technical debt to resolve before the Intent Gateway treats external requests as authoritative candidates.
