# Tradeoffs and Design Considerations

## 1. Event Grid Simulation vs Real Event Grid

Chosen:
- HTTP-triggered function + Event Grid schema

Why:
- No local emulator available
- Keeps event contract intact

Tradeoff:
- No real subscription/filtering behavior

---

## 2. Azurite vs Real Azure Storage

Chosen:
- Azurite

Why:
- Zero cost
- Same SDK usage

Tradeoff:
- Limited performance realism
- No real security model

---

## 3. Queue-Based Processing vs Direct Invocation

Chosen:
- Queue-based

Why:
- Decoupling
- Retry capability
- Real-world architecture

Tradeoff:
- Slightly more complexity

---

## 4. Table Storage vs SQL Database

Chosen:
- Table Storage

Why:
- Azure-native
- Lightweight
- Matches ingestion/audit use case

Tradeoff:
- No relational querying
- Limited indexing

---

## 5. Blob Copy vs Move

Chosen:
- Copy (logical move)

Why:
- Azure standard pattern
- Safer for audit systems

Tradeoff:
- Temporary duplication

---

## 6. Function-Oriented Architecture

Chosen:
- Thin Functions + services

Why:
- Testability
- Maintainability
- Clean separation

Tradeoff:
- Slight upfront structure overhead

---

## 7. Local-First Development

Chosen:
- Full local execution

Why:
- Cost-free
- Faster dev loop

Tradeoff:
- Less exposure to real cloud infra

---

## Final Thought

All tradeoffs were made to balance:
- realism
- cost
- simplicity
- portfolio impact