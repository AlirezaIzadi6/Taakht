# 0004. Use a single monorepo for all services

- Status: Accepted
- Date: 2026-11-12
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

The system is a set of services ([ADR 0001](0001-build-the-system-as-microservices.md)) written in Go and .NET ([language split](0003-split-services-between-go-and-dotnet.md)), run by a team of two. The repository already assumes a single repo: the Makefile discovers every `*.sln` and `go.mod`, and CI uses path filters, per-solution and per-module jobs and a single required check (`ci-ok`) so unrelated stacks are skipped.

## Decision

We will keep all services, the shared contracts (`api/`), the gateway configuration and the documentation in one repository. Each service is its own .NET solution or Go module, built and tested independently. Services share contracts and one platform library per language in `libs/`, which holds infrastructure code only (for example the outbox writer and relay, the idempotent consumer, gRPC interceptors, migrations); no domain code is shared. The rule exists to keep the services' domain code independent, not to stop them reusing infrastructure.

## Options considered

1. **Single monorepo (chosen)** - one change can update a contract and every affected service in one pull request; one CI, one set of conventions and tooling; least coordination for two people. Cost: nothing in the repo structure enforces service independence, so it takes discipline; a broken commit is visible to everyone; CI time can grow with the number of services.
2. **One repository per service** - strong boundaries and independent permissions, but every contract change becomes several coordinated pull requests, and CI, tooling and shared configuration are duplicated. No benefit at the current team size.
3. **Monorepo for services with a separate repository for contracts** - decouples contract versions from code, but turns each contract change into two pull requests plus a version bump.
4. **Monorepo with a build orchestration tool (Nx, Bazel)** - faster incremental builds at scale, but heavy to adopt and not needed while the native tooling and path filters suffice.

Options 2 to 4 were argued, not tried.

## Consequences

- Positive: atomic cross-service changes; one CI definition; simple onboarding.
- Negative: service independence depends on rules we must keep, and nothing enforces them; no per-service access control; a change to a platform library reaches every service of that language, so CI runs all of them.
- Rules: services depend on each other only through contracts in `api/`, never by importing another service's code, and import only the platform library of their own language; the platform libraries hold no domain code; each service has its own solution or module and test project; CI keeps path filters and `ci-ok` as the single required check.
- Known gaps: no service exists yet, so solution and module discovery and the per-service jobs have never run (path filters, `ci-ok` and the skip logic have run on documentation-only pull requests). The rules are not enforced by any tool and rely on review. The Taakht MVP ran four services on one Go and one .NET platform library (`libs/goplatform`, `libs/dotnet/Taakht.Platform`) without trouble, but it was small and is not part of this repository. Generated contract code is not committed (see the [API conventions](../guidelines/api-conventions.md)).
- Revisit when more than one team owns the code, services need very different access control, CI time for a typical change becomes unacceptable, or a platform library starts to carry domain logic or slows down independent changes.

## References

- README, "Repository layout"; `Makefile`; `.github/workflows/ci.yml`
