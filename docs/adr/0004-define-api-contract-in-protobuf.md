# 0004. Define the API contract in Protobuf

- Status: Accepted
- Date: 2026-11-12
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

Services are written in Go and .NET ([ADR 0002](0002-split-services-between-go-and-dotnet.md)), live in one repository ([ADR 0003](0003-use-a-monorepo.md)) and talk to each other over gRPC. Clients reach the system through a REST/JSON edge. We need one place that defines every API so that:

- Go and .NET services cannot drift apart on message shapes.
- The REST surface and its documentation cannot drift from what the services actually accept.
- Breaking changes are caught before they ship.

## Decision

We will define every service API in `.proto` files in the shared `api/` directory and treat them as the single source of truth. REST routes are declared in the same files with `google.api.http` annotations. Server code, the gateway's route descriptor and the OpenAPI document are generated from these files, and `buf lint` and `buf breaking` run in CI.

## Options considered

1. **Protobuf as the source of truth (chosen)** - one contract for both languages; REST mapping, docs and code are generated from it; `buf breaking` gives automatic compatibility checks. Cost: generated OpenAPI is less hand-tunable.
2. **Code-first per language** (Huma/swaggo in Go, Swashbuckle or built-in OpenAPI in .NET) - familiar tooling, but the services speak gRPC, so there is no REST handler to generate a spec from, and two languages would produce two independently drifting descriptions.
3. **A separate repository or the Buf Schema Registry for the protos** - decouples the contract from code, but every change becomes two pull requests with version coordination, and the registry adds an external dependency. Not needed while everything lives in one repository.
4. **OpenAPI-first, REST as the contract** - good for external consumers, but gRPC between services would still need its own contract, giving two sources of truth.

Options 2 to 4 were argued, not tried.

## Consequences

- Positive: one reviewed contract; generated, always-current docs; compatibility enforced mechanically in CI.
- Negative: REST-specific details (file upload, custom status codes) are limited by what the gateway can express.
- Follow-ups: create `api/` with `buf.yaml` and `buf.gen.yaml`; add `api/` to the CI path filters; compile the Google API protos (`annotations.proto`, `http.proto`) explicitly in the .NET projects (see evidence).

## References

- [Evidence: API contract in Protobuf](../evidence/api-contract-protobuf.md) (one proto produced a Go service, a .NET service and the gateway descriptor; `buf breaking` rejected incompatible changes; tried only with a hello-world service)
- [Evidence: edge gateway](../evidence/edge-gateway.md) (OpenAPI generated from the annotations)
- [API conventions](../guidelines/api-conventions.md) (versioning, errors, caller identity, generated code; a draft guideline, not an ADR)
