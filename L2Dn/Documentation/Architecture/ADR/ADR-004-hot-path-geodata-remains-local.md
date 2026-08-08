# ADR-004: Hot-path geodata remains local

Status: Accepted

## Decision

Height lookup, static line of sight, short movement checks and dynamic door/fence collision remain in the GameServer process.

## Consequences

Combat and movement never wait on a network round trip for immediate geodata validation.
