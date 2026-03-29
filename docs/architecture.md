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
- Includes metadata required for validation

### 2. Event Publisher (Local)
- Simulates Azure Event Grid
- Sends Event Grid-compliant payloads to HTTP-triggered function

### 3. Intake Function
- Trigger: HTTP (Event Grid-style event)
- Responsibilities:
  - Parse event
  - Validate metadata
  - Compute checksum
  - Persist intake record
  - Route document

### 4. Table Storage
- `DocumentIntake` → intake records
- `DocumentProcessing` → processing status

### 5. Queue Storage
- Queue: `document-processing`
- Decouples intake from processing

### 6. Processing Function
- Trigger: Queue
- Performs downstream processing
- Updates processing status

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
4. Metadata validated
5. Intake record written to Table Storage
6. Document routed:
   - Valid → validated-documents + queue message
   - Invalid → quarantine-documents
7. Processing function consumes queue message
8. Processing result stored

## Key Design Decisions

- Event Grid is simulated locally using HTTP events
- Azurite replaces Azure Storage
- Storage SDKs are used exactly as in production
- Functions remain thin; logic lives in services

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
