# Testing

This document defines the testing approach for Taakht across all services and shared libraries.

## 1. Principles and Workflow

- **Test-Driven Development (TDD):** Development across all projects starts test-first (tests are written before implementation).
- **Single Commit:** Tests and implementation are delivered together in the same commit.
- **Continuous Evaluation:** TDD is subject to ongoing review. If it slows delivery or creates unwarranted friction for a specific service, it may be relaxed or dropped for that service on a case-by-case basis.
- **Phase 1 Focus:** Current development focuses exclusively on **Unit Tests**. Integration, end-to-end, and load/stress tests will be introduced in subsequent phases.

## 2. Test Scope and Layering

| Layer / Component | Unit Test Requirement | Notes |
|---|---|---|
| **Domain** | **Mandatory** | Pure business logic, aggregates, entities, value objects, invariants, and state machines. Zero I/O dependencies. |
| **Application** | **Mandatory** | Command/Query handlers, use cases, and workflows. External ports (repositories, publishers) are mocked/stubbed. |
| **Shared Libraries (`libs/`)** | **Mandatory** | Platform libraries, shared helpers, and common utilities. |
| **Infrastructure** | Special cases only | Database queries, message brokers, and sweepers are tested via integration tests in future phases. |
| **API & Gateway** | Special cases only | Thin controllers and gRPC handlers are deferred to integration/E2E testing. |

## 3. Contracts and Serialization

- **Single Source of Truth:** API and domain event contracts are defined in `api/` using Protobuf.
- **Schema Compatibility:** Backward compatibility is enforced in CI via `buf lint` and `buf breaking`.
- **No Serialization Testing:** Standard compiler-generated Protobuf code (`Google.Protobuf`, `protoc-gen-go`) is trusted; unit tests do not test serialization or deserialization of standard proto messages.

## 4. Future Test Phases

- **Integration Tests:** Verifying repositories and event publishing/consumption against real databases and brokers using Docker/Testcontainers (PostgreSQL, Kafka).
- **End-to-End (E2E) Tests:** Validating end-to-end user flows over the REST edge gateway (Envoy).
- **Concurrency & Load Tests:** Concurrency race tests for the atomic Ad lock and Swap Saga compensations, with performance metrics recorded.
