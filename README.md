# Regulatory Intake Pipeline

Production-grade starter structure for a local-first Azure Functions regulatory document intake pipeline.

## Solution Layout

- `src/RegulatoryIntake.Functions` - .NET 8 isolated Azure Functions host
- `src/RegulatoryIntake.Application` - application services and orchestration contracts
- `src/RegulatoryIntake.Domain` - domain models and core concepts
- `src/RegulatoryIntake.Infrastructure` - Azure storage integrations
- `src/RegulatoryIntake.EventPublisher` - local console publisher for Event Grid-style events
- `tests/RegulatoryIntake.Application.Tests` - application-layer test scaffolding
- `tests/RegulatoryIntake.Functions.Tests` - function host test scaffolding
- `infra/` - local infrastructure assets
- `sample-data/` - sample regulatory documents for local demos

## Current Status

This repository currently contains Task 1 foundation scaffolding only:

- solution and project structure
- Azure Functions host bootstrap
- dependency injection registration boundaries
- test project shells

Pipeline features, local settings, Azurite compose, and function implementations are intentionally deferred to later tasks.
