# Demo Script

## Goal

Demonstrate a complete event-driven regulatory document intake workflow locally.

## Step 1: Start Azurite

From the repository root:

```bash
docker compose -f infra/docker-compose.yml up -d azurite
```

Azurite should expose:

- Blob on `127.0.0.1:10000`
- Queue on `127.0.0.1:10001`
- Table on `127.0.0.1:10002`

## Step 2: Create local configuration files

```bash
cp src/RegulatoryIntake.Functions/local.settings.json.example src/RegulatoryIntake.Functions/local.settings.json
cp src/RegulatoryIntake.EventPublisher/appsettings.json.example src/RegulatoryIntake.EventPublisher/appsettings.json
```

The default examples are already Azurite-compatible and use:

- Blob containers: `raw-documents`, `validated-documents`, `quarantine-documents`
- Queue: `document-processing`
- Tables: `DocumentIntake`, `DocumentProcessing`

## Step 3: Start the Functions app

In one terminal:

```bash
cd src/RegulatoryIntake.Functions
func start
```

The intake endpoint is:

`POST http://localhost:7071/api/intake/events/blob-created`

If local auth is enabled in your Functions host, set `Publisher:Event:FunctionCode` in `src/RegulatoryIntake.EventPublisher/appsettings.json`.

## Step 4: Publish a sample document

In a second terminal from the repository root:

```bash
dotnet run --project src/RegulatoryIntake.EventPublisher
```

The publisher will:

- Upload `sample-data/sanctions.json` into `raw-documents`
- Apply required metadata: `correlationId`, `documentType`, `sourceSystem`
- Post a `Microsoft.Storage.BlobCreated` Event Grid-style payload to the intake Function

## Step 5: What to show during the demo

Call out the implemented flow:

1. The HTTP-triggered Function accepts the event payload and stays thin.
2. `IntakeOrchestrator` reads blob metadata and content, validates metadata, computes a SHA-256 checksum, and routes the blob.
3. Invalid documents are copied to `quarantine-documents` and removed from `raw-documents`.
4. Valid documents are copied to `validated-documents`, removed from `raw-documents`, and published to the `document-processing` queue.
5. A queue-triggered Function delegates downstream work to `DocumentProcessingService`, which writes a completed row to `DocumentProcessing`.

## Step 6: Verify the outcome

After the publisher finishes, verify:

- The sample blob is no longer in `raw-documents`
- The blob exists in `validated-documents`
- A row exists in the `DocumentIntake` table with status `Validated`
- A row exists in the `DocumentProcessing` table with status `Completed`

To demonstrate the quarantine path, remove one of the required metadata fields in `src/RegulatoryIntake.EventPublisher/appsettings.json` and run the publisher again. The blob should end up in `quarantine-documents`, no queue message should be published, and the `DocumentIntake` row should contain validation errors.
