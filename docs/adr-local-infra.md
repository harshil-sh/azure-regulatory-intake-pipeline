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

Use HTTP-triggered Azure Function to simulate Event Grid ingestion using Event Grid-compliant payloads.

## Rationale

### Why Azurite?
- Official Microsoft emulator
- Supports Blob, Queue, Table
- Fully compatible SDK usage
- No code changes required for migration

### Why not emulate Event Grid?
- No official local Event Grid emulator
- HTTP simulation preserves event contract
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
- Configure Event Grid subscriptions
- Deploy Functions to Azure
- Add managed identity and Key Vault

## Conclusion

This approach preserves architectural integrity while enabling local, cost-free development.