
# Coding Standards

## General Principles

- Production-grade code only
- No placeholder or demo shortcuts
- Clean, readable, maintainable

## Architecture

- Functions must be thin
- Business logic belongs in services
- Use dependency injection everywhere
- Follow SOLID principles

## Naming

- Use clear, descriptive names
- Avoid abbreviations
- Use consistent naming across layers

## Models

- Prefer strongly typed models
- Avoid dynamic objects
- Keep DTOs explicit

## Error Handling

- Fail fast for invalid input
- Log meaningful errors
- Avoid silent failures

## Logging

- Use structured logging
- Include correlationId where available
- Avoid excessive logging noise

## Storage

- Use Azure SDKs (Blob, Queue, Table)
- Do not abstract unnecessarily
- Keep storage access clean and testable

## Functions

- One responsibility per function
- Keep trigger logic minimal
- Delegate to services

## Testing

- Unit tests for services
- Integration tests for pipeline
- No external dependencies in tests

## Configuration

- Use environment variables
- Do not hardcode secrets
- Support local.settings.json

## Local vs Cloud

- Code must work locally with Azurite
- No Azure-only dependencies
- Migration must require only config changes

## Documentation

- Keep README updated
- Keep architecture docs accurate
- Document tradeoffs clearly

## Final Rule

Code should reflect senior-level engineering standards and be ready for production migration.