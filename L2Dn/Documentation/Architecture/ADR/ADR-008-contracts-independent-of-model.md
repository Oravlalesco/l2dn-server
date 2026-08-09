# ADR-008: Transport contracts are independent of GameServer.Model

Status: Accepted

## Decision

Future `L2Dn.Npc.Contracts` assemblies and inter-process schemas cannot reference `L2Dn.GameServer.Model` or serialize its objects.

## Consequences

Contracts use immutable, versioned values. CI architecture tests will enforce the dependency direction when the contracts project is introduced.

The assembly is `L2Dn.Npc.Contracts`, while its CLR namespace is `L2Dn.NpcContracts`. This avoids the C# namespace/type ambiguity between `L2Dn.Npc` and the existing `L2Dn.GameServer.Model.Actor.Npc` type without changing the architectural boundary.
