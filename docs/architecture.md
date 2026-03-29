# Architecture Overview

## System Summary

The Regulatory Intake Pipeline is an event-driven, serverless-style system designed to ingest, validate, and process regulatory documents using Azure-native patterns.

The system is built using:
- Azure Functions (.NET 8 isolated worker)
- Azure Blob Storage (via Azurite locally)
- Azure Queue Storage (via Azurite)
- Azure Table Storage (via Azurite)
- Event Grid-style event contracts (locally simulated)

## Architecture Principles

- Event-driven design
- Serverless compute model
- Separation of concerns (Functions vs Services)
- Local-first cloud architecture
- Infrastructure abstraction (swap local ↔ cloud without code changes)
- Idempotent and traceable processing

## High-Level Components

### 1. Blob Storage (Raw Zone)
- Container: `raw-documents`
- Stores incoming regulatory files
- Stores metadata required for validation:
  - `correlationId`
  - `documentType`
  - `sourceSystem`

### 2. Event Publisher (Local)
- Simulates Azure Event Grid
- Sends Event Grid-compliant payloads to HTTP-triggered function

### 3. Intake Function
- Trigger: HTTP (Event Grid-style event)
- Route: `POST /api/intake/events/blob-created`
- Responsibilities:
  - Parse event
  - Read blob metadata and content from `raw-documents`
  - Validate metadata
  - Compute checksum
  - Persist intake audit record to `DocumentIntake`
  - Route document with copy-then-delete semantics
  - Publish a queue message for valid documents

### 4. Table Storage
- `DocumentIntake` → intake audit record including status, checksum, correlation ID, and validation errors when present
- `DocumentProcessing` → downstream processing status for validated documents

### 5. Queue Storage
- Queue: `document-processing`
- Decouples intake from processing

### 6. Processing Function
- Trigger: Queue
- Queue binding: `%Storage__Queues__DocumentProcessing%`
- Performs thin trigger handling, deserializes the queue payload, and delegates to `DocumentProcessingService`
- Writes a completed processing record to `DocumentProcessing`

### 7. Blob Storage (Processed Zones)
- `validated-documents`
- `quarantine-documents`

## Configuration

The local Functions host uses the following configuration keys for storage-backed execution:
- `AzureWebJobsStorage`
- `Storage__ConnectionString`
- `Storage__Containers__RawDocuments`
- `Storage__Containers__ValidatedDocuments`
- `Storage__Containers__QuarantineDocuments`
- `Storage__Queues__DocumentProcessing`
- `Storage__Tables__DocumentIntake`
- `Storage__Tables__DocumentProcessing`

## Data Flow

1. Document uploaded to Blob Storage (raw-documents)
2. Event publisher sends Event Grid-style event
3. Intake function processes event
4. Orchestrator reads blob metadata and content from `raw-documents`
5. Metadata validated and checksum computed
6. Blob routed with copy-then-delete behavior:
   - Valid → `validated-documents` + queue message
   - Invalid → `quarantine-documents`
7. Intake record written to `DocumentIntake`
8. Processing function consumes queue message for validated documents
9. Processing result stored in `DocumentProcessing`

## Key Design Decisions

- Event Grid is simulated locally using HTTP events
- Azurite replaces Azure Storage
- Storage SDKs are used exactly as in production
- Functions remain thin; logic lives in services
- Correlation IDs are sourced from blob metadata when present, otherwise from the incoming event payload

## Non-Goals

- Full Event Grid emulation
- Full production security (Key Vault, Managed Identity)
- Distributed tracing (future enhancement)

## Future Enhancements

- Durable Functions orchestration
- Dead-letter queue handling
- Retry policies
- OpenTelemetry tracing
- Schema validation engine
- Integration with AI-based document classification
