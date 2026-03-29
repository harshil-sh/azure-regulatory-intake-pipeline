# RegulatoryIntake.IntegrationTests

These tests exercise the local intake orchestration against Azurite-backed Blob, Queue, and Table storage.

## Prerequisite

Start Azurite on the default local ports before running the integration suite:

```bash
docker compose -f infra/docker-compose.yml up -d azurite
```

The tests use `UseDevelopmentStorage=true`, so they expect:

- Blob storage on `127.0.0.1:10000`
- Queue storage on `127.0.0.1:10001`
- Table storage on `127.0.0.1:10002`

## Run

```bash
dotnet test tests/RegulatoryIntake.IntegrationTests/RegulatoryIntake.IntegrationTests.csproj
```

Each run creates isolated Azurite containers, queue, and tables, then deletes them during cleanup.
