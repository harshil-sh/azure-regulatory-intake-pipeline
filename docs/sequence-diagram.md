# Sequence Diagram

## Document Intake Flow

```text
User / Script
    |
    | Upload document with metadata
    v
Azurite Blob Storage (raw-documents)
    |
    | Trigger event (simulated)
    v
Event Publisher (Console App)
    |
    | POST Event Grid payload
    v
BlobCreatedEventIntakeFunction (HTTP Trigger)
    |
    | Parse event payload
    v
IntakeOrchestrator
    |
    |-- Read blob metadata and content
    |-- Validate metadata
    |-- Compute checksum
    |-- Route blob via copy + delete
    |-- Persist intake record to Table Storage (DocumentIntake)
    |
    |---- if invalid ----> Blob Storage (quarantine-documents)
    |
    |---- if valid ----> Blob Storage (validated-documents)
    |                     |
    |                     v
    |                Queue Storage (document-processing)
    |
    v
DocumentProcessingFunction (Queue Trigger)
    |
    | Delegate to DocumentProcessingService
    v
Table Storage (DocumentProcessing)
```
