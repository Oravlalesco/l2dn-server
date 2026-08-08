# ADR-005: Heavy pathfinding may be distributed

Status: Accepted

## Decision

Long paths, navigation planning and multi-point route generation may move to a dedicated service after local geodata has a stable abstraction.

## Consequences

Remote path results are advisory, time-bounded and revalidated locally before movement starts.
