# ADR-002: AI produces intents

Status: Accepted

## Decision

The target architecture makes NPC AI return immutable intents. Only an Intent Gateway may validate, authorize and execute an intent against current authoritative state.

## Consequences

The phase-1 legacy command adapter is a temporary seam, not the final API. It must be replaced rather than exposed over a transport.
