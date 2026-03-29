# TASKS.md

## Project
Regulatory Intake Pipeline

## Goal
Build a senior-level GitHub portfolio project that demonstrates an event-driven document ingestion pipeline using .NET 8 isolated Azure Functions, Event Grid-style events, Blob Storage, Queue Storage, and Table Storage for a regulatory data intake workflow.

The project must be:
- Azure-oriented in design
- fully runnable locally without a paid Azure subscription
- powered by Azurite for Blob, Queue, and Table storage
- structured like a real production codebase
- suitable for step-by-step execution by Codex

## Core Constraints
- No paid Azure subscription
- Use Azurite for local storage emulation
- Keep code written as if it were targeting real Azure services
- The local-to-cloud swap should be primarily configuration, not a rewrite
- Event Grid should be represented using Event Grid-style event payloads posted to a local HTTP-triggered Azure Function
- Functions must stay thin
- Business logic must live in services
- Use strong typing and dependency injection
- Keep documentation aligned with implementation

## Working Rules for Codex
- Read these files before making changes:
  - README.md
  - docs/architecture.md
  - docs/adr-local-infra.md
  - docs/coding-standards.md
  - docs/sequence-diagram.md
  - docs/tradeoffs.md
  - docs/demo-script.md
- Complete only the requested task
- Do not start later tasks
- Do not rewrite the architecture
- Do not introduce paid Azure-only dependencies
- Keep implementation production-minded and portfolio-quality
- Run relevant verification for the current task
- Summarize plan, changes made, files updated, verification performed, and whether the task is complete

---

## Phase 1 — Repo foundation

### Task 1: Create production-grade solution structure for Azure Functions pipeline
**Objective**
Create the initial solution and project structure for the regulatory intake pipeline.

**Requirements**
- Create a clean solution structure under `src/`, `tests/`, `infra/`, `docs/`, and `sample-data/`
- Add a .NET 8 isolated Azure Functions project
- Add a console app project for local event publishing
- Add test project scaffolding
- Keep naming consistent with the regulatory intake domain

**Acceptance Criteria**
- Solution and project structure exist for Functions, EventPublisher, tests, docs, infra, and sample-data
- Project names are clean and consistent
- No feature implementation beyond foundational structure

---

### Task 2: Add Docker Compose for Azurite with Blob, Queue, and Table services
**Objective**
Add local infrastructure for Azure Storage emulation.

**Requirements**
- Add `infra/docker-compose.yml`
- Configure Azurite for blob, queue, and table services
- Use stable local ports and persistent volume mapping

**Acceptance Criteria**
- `infra/docker-compose.yml` exists
- Azurite exposes blob, queue, and table ports
- Configuration is suitable for local development and demo use

---

### Task 3: Add local settings and configuration scaffolding for storage-backed local execution
**Objective**
Prepare the Functions app for local execution against Azurite.

**Requirements**
- Add `local.settings.json.example`
- Define all required storage, container, queue, and table settings
- Keep names consistent with docs

**Acceptance Criteria**
- `local.settings.json.example` exists
- Required storage/container/table/queue settings are represented
- No real secrets are added

---

## Phase 2 — Core storage abstractions

### Task 4: Implement Blob, Queue, and Table storage service abstractions
**Objective**
Create minimal, production-minded storage services.

**Requirements**
- Add Blob storage service
- Add Queue storage publisher/service
- Add Table storage service
- Register services with DI

**Acceptance Criteria**
- Storage service classes exist and compile
- Services are registered through dependency injection
- Abstractions are minimal and testable

---

### Task 5: Implement Event Grid-style event models and intake domain models
**Objective**
Create explicit models for event ingestion and pipeline processing.

**Requirements**
- Add Event Grid envelope model
- Add blob-created event data model
- Add document intake record model
- Add queue message model

**Acceptance Criteria**
- Event Grid envelope model exists
- Blob-created event data model exists
- Intake and queue models are strongly typed and coherent

---

### Task 6: Implement metadata validation and checksum services
**Objective**
Create reusable domain services for validation and hashing.

**Requirements**
- Add metadata validation service
- Validate required metadata keys
- Add checksum/hash generation service for file content

**Acceptance Criteria**
- Metadata validator exists and validates required fields
- Checksum service computes file hashes
- Services are testable and independent of trigger-specific code

---

## Phase 3 — Intake pipeline

### Task 7: Implement HTTP-triggered Function to receive Event Grid-style blob-created events
**Objective**
Create the local event ingestion entrypoint.

**Requirements**
- Add HTTP-triggered Azure Function endpoint
- Parse Event Grid-style payloads
- Handle invalid or empty payloads cleanly
- Delegate orchestration to a service

**Acceptance Criteria**
- HTTP-triggered function endpoint exists
- Function parses Event Grid-style payloads
- Invalid payloads are handled cleanly
- Function remains thin

---

### Task 8: Implement intake orchestration service for validation, persistence, routing, and queue publishing
**Objective**
Create the core application workflow for document intake.

**Requirements**
- Read blob metadata and content
- Validate metadata
- Compute checksum
- Persist intake record to table storage
- Route invalid documents to quarantine
- Route valid documents to validated container
- Publish queue message for valid documents

**Acceptance Criteria**
- Orchestrator handles valid and invalid document flows
- Intake records are written to table storage
- Valid documents are routed for downstream processing
- Logic is centralized and testable

---

## Phase 4 — Downstream processing

### Task 9: Implement queue-triggered document processing Function
**Objective**
Process validated documents asynchronously.

**Requirements**
- Add queue-triggered Function
- Read processing message
- Write processing result to table storage
- Log processing lifecycle clearly

**Acceptance Criteria**
- Queue-triggered Function exists
- Processing result is written to table storage
- Logging is clear and aligned with the intake flow

---

### Task 10: Implement validated and quarantine blob routing behavior
**Objective**
Complete the blob movement/routing rules for the pipeline.

**Requirements**
- Copy or move invalid documents into `quarantine-documents`
- Copy or move valid documents into `validated-documents`
- Keep routing logic clear and auditable

**Acceptance Criteria**
- Invalid documents are routed to quarantine
- Valid documents are routed to validated storage
- Routing behavior is consistent with the docs

---

## Phase 5 — Event publishing and local demo

### Task 11: Create local console publisher to upload a sample document and post Event Grid-style payload
**Objective**
Enable a full local demo flow.

**Requirements**
- Upload a sample file to `raw-documents`
- Apply required metadata
- Post Event Grid-style payload to local Function endpoint
- Keep config easy to adjust

**Acceptance Criteria**
- Console publisher uploads a sample file to `raw-documents`
- Publisher posts valid Event Grid-style payload to the local Function endpoint
- Publisher configuration is local-friendly and understandable

---

### Task 12: Add sample documents and local demo support files
**Objective**
Make the repo easy to demo and review.

**Requirements**
- Add sample files under `sample-data/`
- Ensure sample files are suitable for the pipeline demo
- Keep file names domain-relevant

**Acceptance Criteria**
- Sample files exist
- Demo flow can reference sample files directly
- Naming is consistent with the regulatory intake story

---

## Phase 6 — Quality

### Task 13: Add unit tests for metadata validation and checksum services
**Objective**
Cover core domain logic with fast tests.

**Requirements**
- Add xUnit tests for metadata validator
- Add xUnit tests for checksum service
- Keep tests deterministic

**Acceptance Criteria**
- Unit tests exist for metadata validation
- Unit tests exist for checksum generation
- Tests are isolated and stable

---

### Task 14: Add integration test coverage for the local intake pipeline against Azurite-compatible flow
**Objective**
Prove that the main workflow works end to end in local mode.

**Requirements**
- Add or extend integration test project
- Cover core ingestion flow
- Document any required environment setup

**Acceptance Criteria**
- Integration test project exists or is extended
- Core local ingestion flow is covered
- Environment/setup assumptions are documented where needed

---

## Phase 7 — Portfolio polish

### Task 15: Complete README with architecture summary, local run instructions, and production migration path
**Objective**
Turn the repo into a strong portfolio artifact.

**Requirements**
- Explain the business scenario
- Explain the architecture
- Explain how to run locally with Azurite
- Explain how to migrate to real Azure

**Acceptance Criteria**
- README explains the project clearly
- README documents Azurite-based local setup
- README explains the production migration path

---

### Task 16: Finalize documentation under docs folder and align it with the implemented solution
**Objective**
Ensure documentation is accurate and recruiter-ready.

**Requirements**
- Align architecture docs with actual implementation
- Update sequence diagram if needed
- Update demo instructions if needed
- Ensure no documentation drift remains

**Acceptance Criteria**
- Docs are consistent with the codebase
- Architecture and ADR docs reflect actual implementation
- Demo instructions are usable and accurate

---

## Recommended Execution Order
1. Task 1
2. Task 2
3. Task 3
4. Task 4
5. Task 5
6. Task 6
7. Task 7
8. Task 8
9. Task 9
10. Task 10
11. Task 11
12. Task 12
13. Task 13
14. Task 14
15. Task 15
16. Task 16

---

## Definition of Done
This project is done when:
- the full local pipeline runs end to end using Azurite
- a sample document can be uploaded and processed through the intake flow
- intake and processing records are persisted
- the architecture is documented clearly
- the repo is portfolio-ready and understandable to recruiters, hiring managers, and senior engineers