# Regulatory Intake Pipeline

Regulatory Intake Pipeline is a local-first Azure portfolio project that demonstrates how to ingest, validate, route, and process regulatory documents with .NET 8 isolated Azure Functions and Azure Storage patterns.

The solution is designed to run end to end without a paid Azure subscription. Blob, Queue, and Table storage are provided locally through Azurite, while Event Grid is represented with an HTTP-triggered Function that accepts Event Grid-style payloads. The code stays Azure-oriented so the move to real Azure is primarily an infrastructure and configuration change rather than an application rewrite.

The business scenario is a compliance intake workflow: documents such as sanctions lists, trade files, or regulatory submissions arrive in a raw landing zone, must be checked for required metadata, recorded for auditability, routed according to validation outcome, and then processed asynchronously by downstream systems.

## Architecture Summary

The pipeline follows a simple event-driven flow:

1. A document is uploaded into the `raw-documents` blob container.
2. The local event publisher posts an Event Grid-style `Microsoft.Storage.BlobCreated` payload to the intake Function.
3. The intake Function stays thin and delegates orchestration to application services.
4. The orchestration layer reads blob metadata and content, validates required metadata, computes a checksum, writes a `DocumentIntake` record to Table Storage, and routes the blob.
5. Valid documents move to `validated-documents` and publish a message to the `document-processing` queue.
6. Invalid documents move to `quarantine-documents`.
7. A queue-triggered processing Function handles validated documents asynchronously and records results in the `DocumentProcessing` table.

This separation keeps triggers minimal, puts business logic in services, and preserves a production-minded structure:

- `src/RegulatoryIntake.Functions`: HTTP and queue-triggered Azure Functions
- `src/RegulatoryIntake.Application`: orchestration, validation, checksum, and processing services
- `src/RegulatoryIntake.Infrastructure`: Blob, Queue, and Table storage integrations
- `src/RegulatoryIntake.Domain`: event contracts, entities, metadata constants, and queue models
- `src/RegulatoryIntake.EventPublisher`: local console publisher for sample document upload plus Event Grid-style event posting
- `tests/RegulatoryIntake.IntegrationTests`: Azurite-backed integration coverage for the intake flow

## Local Run

### Prerequisites

- .NET 8 SDK
- Docker
- Azure Functions Core Tools v4

### 1. Start Azurite

```bash
docker compose -f infra/docker-compose.yml up -d azurite
```

Azurite exposes the default local Azure Storage endpoints:

- Blob: `127.0.0.1:10000`
- Queue: `127.0.0.1:10001`
- Table: `127.0.0.1:10002`

### 2. Create local config files

```bash
cp src/RegulatoryIntake.Functions/local.settings.json.example src/RegulatoryIntake.Functions/local.settings.json
cp src/RegulatoryIntake.EventPublisher/appsettings.json.example src/RegulatoryIntake.EventPublisher/appsettings.json
```

The default examples are already Azurite-compatible and use:

- `UseDevelopmentStorage=true`
- Blob containers: `raw-documents`, `validated-documents`, `quarantine-documents`
- Queue: `document-processing`
- Tables: `DocumentIntake`, `DocumentProcessing`

### 3. Start the Functions host

```bash
cd src/RegulatoryIntake.Functions
func start
```

The intake endpoint is available at:

`POST http://localhost:7071/api/intake/events/blob-created`

### 4. Publish a sample document and event

From the repository root, in a second terminal:

```bash
dotnet run --project src/RegulatoryIntake.EventPublisher
```

The publisher:

- uploads the sample file from `sample-data/`
- writes required metadata to the blob
- posts an Event Grid-style payload to the local intake Function

### 5. Verify the local flow

After running the publisher, the expected outcome is:

- a blob appears in `validated-documents` for valid metadata, or `quarantine-documents` for invalid metadata
- a `DocumentIntake` row is written to Table Storage
- a `document-processing` queue message is created for valid documents
- the queue-triggered Function writes a `DocumentProcessing` row after downstream processing

## Production Migration Path

This project is intentionally structured so migration to Azure is mostly operational:

1. Replace Azurite connection strings with a real Azure Storage account for `AzureWebJobsStorage` and `Storage__ConnectionString`.
2. Provision the equivalent Blob containers, Queue, and Tables in Azure Storage using the same names or update configuration values.
3. Deploy the Functions app to Azure Functions without changing the service-layer orchestration code.
4. Replace the local HTTP event simulation with a real Event Grid subscription that targets the intake endpoint.
5. Move secrets and configuration into Azure app settings, then add managed identity and Key Vault if required by the production environment.
6. Add production concerns that are intentionally out of scope locally, such as monitoring, retry policies, dead-letter handling, and tighter access control.

The important architectural boundary is unchanged in both environments: Functions remain thin, storage access stays in infrastructure services, and workflow logic remains in the application layer.

## Verification

Unit tests:

```bash
dotnet test tests/RegulatoryIntake.Application.Tests/RegulatoryIntake.Application.Tests.csproj
```

Azurite-backed integration tests:

```bash
dotnet test tests/RegulatoryIntake.IntegrationTests/RegulatoryIntake.IntegrationTests.csproj
```
