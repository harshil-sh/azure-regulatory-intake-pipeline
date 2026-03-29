# ADR: Local-First Infrastructure Using Azurite

## Status
Accepted

## Context

The project aims to demonstrate a production-grade Azure event-driven architecture without requiring a paid Azure subscription.

Constraints:
- Zero cloud cost
- Fully reproducible locally
- Maintain Azure-native design patterns

## Decision

Use Azurite (Azure Storage emulator) for:
- Blob Storage
- Queue Storage
- Table Storage

Use an HTTP-triggered Azure Function at `POST /api/intake/events/blob-created` to simulate Event Grid ingestion using Event Grid-style payloads.

Keep the Functions layer thin and place intake orchestration, validation, checksum generation, routing, and downstream processing logic in application services.

## Rationale

### Why Azurite?
- Official Microsoft emulator
- Supports Blob, Queue, Table
- Fully compatible SDK usage
- No code changes required for migration

### Why not emulate Event Grid?
- No official local Event Grid emulator
- HTTP simulation preserves the event contract used by the implementation
- Keeps architecture honest and simple

### Why not mock everything?
- Reduces realism
- Weakens portfolio value
- Less impressive to recruiters

## Trade-offs

### Pros
- Zero cost
- Fully local development
- Real Azure SDK usage
- Clean migration path

### Cons
- No true Event Grid infrastructure
- Limited observability vs Azure
- No managed identity support locally

## Alternatives Considered

### Full Azure deployment
Rejected:
- Requires paid subscription
- Adds cost and complexity

### Mock-based system
Rejected:
- Not realistic
- Weak architectural signal

## Migration Plan

To move to Azure:
- Replace connection strings
- Configure Event Grid subscriptions to target the intake endpoint
- Deploy Functions to Azure
- Add managed identity and Key Vault

## Conclusion

This approach preserves architectural integrity while enabling local, cost-free development.
