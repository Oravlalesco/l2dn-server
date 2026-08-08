# Architecture decision records

This directory records the decisions that govern the NPC brain modernization program. Accepted decisions are constraints for every later phase; changing one requires a superseding ADR.

| ADR | Decision |
| --- | --- |
| 001 | GameServer is the world authority |
| 002 | AI produces intents and never mutates world state |
| 003 | Physical NPCs remain in GameServer initially |
| 004 | Hot-path geodata remains local |
| 005 | Heavy pathfinding may be distributed |
| 006 | NPC Brain requires a local fallback |
| 007 | LLMs never participate in the combat loop |
| 008 | Inter-process contracts cannot depend on GameServer.Model |
| 009 | Partitions use instance and region affinity |
| 010 | World Director operates through directives and policies |
| 011 | Entity keys encode NPC incarnation; non-NPC generation zero is best effort |
