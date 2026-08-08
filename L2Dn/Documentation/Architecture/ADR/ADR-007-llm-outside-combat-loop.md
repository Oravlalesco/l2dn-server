# ADR-007: LLMs stay outside the combat loop

Status: Accepted

## Decision

LLMs may produce structured cognitive recommendations through a controlled gateway, but never execute commands or participate synchronously in immediate combat decisions.

## Consequences

LLM latency, cost, malformed output and provider outages cannot interrupt combat.
