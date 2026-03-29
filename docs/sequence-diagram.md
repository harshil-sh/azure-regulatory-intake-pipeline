# Sequence Diagram

## Document Intake Flow

```text
User / Script
    |
    | Upload document
    v
Azurite Blob Storage (raw-documents)
    |
    | Trigger event (simulated)
    v
Event Publisher (Console App)
    |
    | POST Event Grid payload
    v
BlobUploadWebhookFunction (HTTP Trigger)
    |
    | Parse event
    v
IntakeOrchestrator
    |
    |-- Validate metadata
    |-- Compute checksum
    |-- Persist intake record
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
    | Process document
    v
Table Storage (DocumentProcessing)