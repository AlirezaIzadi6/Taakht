# Taakht

![Go](https://img.shields.io/badge/Go-1.27-00ADD8?style=flat&logo=go)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=flat&logo=dotnet)
![DDD](https://img.shields.io/badge/Design-DDD-success?style=flat)

**A goods-swap layer for a classifieds platform (designed around Divar), built as a microservices portfolio project.**

> **Status: design complete, scaffolding in progress (Phase 1).** The domain model and bounded contexts are documented and the key decisions are recorded as draft ADRs; repo tooling (CI, Makefile, edge gateway config) is in place. Service implementation has not started. Sections marked `TBD` depend on decisions that are still open. See [Status and known limitations](#status-and-known-limitations).

## The idea

Many people hold small, low-value items they no longer use. Selling them for cash is unattractive (high friction, low price, no trust mechanism), and informal swap groups have no matching or safety net.

Taakht lets users declare what they have and what they want, matches them with each other (also while they are away from the app), and walks both sides through negotiation and delivery. Money never goes through the platform: any price difference is settled directly between the two parties.

Honest framing: this is a **calculated bet for a Growth/Experiments team**, not a claim about the future of the platform. It deliberately targets low-value goods and takes no cut of the exchange (the only revenue idea is paid matching priority), so direct revenue is capped; the case rests on secondary metrics (engagement and reactivation of idle inventory).

## How it works

1. **Browse** anonymously by category and neighborhood. Browsing is a stateless query; nothing is stored and requests cannot be sent.
2. **Publish an Ad** to become discoverable. An Ad has an "I have" side (a category, which can be very general, and the neighbourhood of the item are required) and an "I want" side (all optional: categories, neighbourhoods of the city, a maximum distance; empty means anything). It is persistent, versioned, and stays active in the matching engine after restarts. It can be hidden at any time; it is never deleted. Once a deal is agreed on it, the Ad is locked and leaves the index, and it is closed for good (still not deleted) when nothing can cancel the agreement any more. Matching is event-driven and two-way, and each user sees the matches ranked by how well the other Ad fits what they asked for. The user is notified when matches appear (one notification groups the matches from one publish or edit).
3. **Request and negotiate.** Chat is independent of negotiation: you can open one with the owner of any Ad you see. A negotiation request opens a **Negotiation** in the **Deal** service right away, with a 3-pillar **Proposal** snapshot (`Ad1_Version + Ad2_Version + Terms`). Each Ad can have at most 10 open negotiations at a time (inbound and outbound combined; configurable), and each pair of Ads allows only 3 requests until one is accepted. An unanswered request expires after 3 days; an accepted negotiation expires after 3 days without activity or 30 days in total. Blocking a user cuts chat, matching and new requests between the two and withdraws their open negotiations.
4. **Pin down the terms.** Goods and terms form a single package. Any edit to an Ad or to the logistics terms produces a new proposal snapshot that keeps the unchanged pillars and carries no approval; both sides, including the editor, must approve again explicitly. Each party gives a single, explicit approval on the active proposal.
5. **Lock.** When both sides approve the active Proposal, an agreement is reached (`AgreementReached`), which is still tentative. Deal asks the Ad service, with a synchronous call (`ClaimLock`), to claim both Ads in one atomic transaction, which succeeds only if both are still available at the agreed versions. When the claim succeeds (`ExclusiveLockAcquired`) the Ads become `Locked`, competing negotiations on them are cancelled with notifications, and the exchange moves to the **Swap** phase; from here the agreement is binding. If another negotiation won (`AlreadyLocked`), the losing negotiation is cancelled; if an Ad was edited meanwhile (`VersionChanged`), the proposal is rebuilt on the new versions and both sides approve again.
6. **Complete the swap.** The platform never touches money: any price difference is paid directly between the two parties, outside the system. The parties pick one of two delivery methods per leg (a **locker**, or **in person / courier** arranged by themselves); each party pays the locker fee for their own leg directly to the locker partner within a 1-hour deadline, otherwise the Swap is cancelled and a penalty is applied to their reputation. There is no withdrawal after the lock. After a completed swap each party is asked whether they were satisfied; users can also report abuse, and the internal trust score is only used to rank matches.

## Architecture at a glance

A microservices system designed for the scale it would need to operate at (barter liquidity only exists on a large platform), run as a single-node demo. The distributed-systems problems (partial failure, eventual consistency, ordering, idempotency) are real in this demo, because they come from process and data boundaries, not from the number of machines. Scale itself is claimed only where it is measured.

Services are classified by how deep the implementation goes:

| Service | Responsibility | Class | Implementation |
|---|---|---|---|
| **Ad** | Owns Ads: creation, versioned edits, publish/hide, atomic locking, consumption by a Swap (Go 1.27) | Core | Full |
| **Matching** | Neighbourhood/category/reputation-aware candidate discovery (Criteria against the Ad index); two-sided sync and async matching (Go 1.27) | Core | Full |
| **Deal** | Modular monolith (.NET 10) combining **Negotiation** (multi-candidate negotiations, 3-pillar proposal snapshots, approvals, state machine) and **Swap** (exclusive lock coordination, locker fee payment relay, blind handoff, immutable audit vault) | Core | Full |
| **Communication** | Downstream event listener and notification translator (calling external notification provider); trade chat for demo (minimal) (Go 1.27) | Supporting | Minimal |
| **Reputation** | Event-sourced internal trust score (recomputable when the formula changes), review question after a swap, abuse-report review queue, blocking, penalties; Matching keeps a local copy of the score (.NET 10) | Supporting | Minimal |
| **Provider adapters** | Everything outside the swap domain (see below) | Generic | Mock |

### What is real and what is mocked

Every dependency outside the swap domain sits behind a small interface with a mock implementation. Swapping in a real implementation later is a wiring change, not a change to the core services.

| Interface | Phase 1 (this project) |
|---|---|
| `AuthProvider` | Issues a JWT for seeded users, no real password check |
| `TaxonomyProvider` | Divar's category taxonomy and city/neighbourhood division, from a small fixed dataset (Taakht has no link to Divar ads) |
| `KYCProvider` | Always passes |
| `DeliveryProvider` | Simulated locker statuses (the only delivery method integrated with a partner) |
| `ReportProvider` | Records an abuse report (any user can be reported, not only a trade counterpart), no review workflow |
| `NotificationProvider` | Dispatches notifications to external channels (Divar OHS) |

There is deliberately no wallet, payment or escrow interface: money never flows through the platform.

Rule of thumb: mock an external dependency when it cannot realistically be built (licenses, partners) or when building it shows nothing interesting. Build for real anything that is part of the swap domain itself, such as the matching engine and its search index.

## Key design decisions

- **Persistent, class-level, versioned Ad** instead of a toggle on an existing Divar ad.
- **Modular Monolith for Deal in .NET**, keeping negotiation and swap in isolated modules with internal transactional outbox events to eliminate unnecessary network latency.
- **An atomic, synchronous Ad lock in the Ad service, and a Saga with compensating transactions for the swap lifecycle** (lock, locker fees, completion or cancellation).
- **Three-pillar Proposal snapshot model** (`Ad1_Version + Ad2_Version + Terms`) with single approval per side, ensuring legal safety and clean state machines.
- **Matching versus search:** matching is automatic, two-way and ranked per viewer (category fit, then distance, then trust); search is a plain filter over the index with no distance.
- **Blocking** is a trust-and-safety concern with effects on chat, matching and negotiation, without letting anyone escape a binding agreement.
- **Event-sourced Reputation** with lazy hydration. The trust score is internal and only ranks matches; abuse reports are reviewed Stack Overflow-style (instant effect only for reporters with enough standing, everyone else needs an admin).
- **Ports and Adapters** for all peripheral services.
- **No money in the system.** No escrow, wallet, deposit or commission on the exchange; price differences are settled between the parties. This removes the PSP/escrow licensing problem at the cost of any payment guarantee.
- **Two delivery methods only:** locker (integrated) and in-person/courier (arranged by the parties).
- **Eligibility Config**: a global whitelist of categories and pilot neighborhoods that also drives what users can pick.

## Tech stack

- **Languages:** Go 1.27 (Ad, Matching, Communication), C# / .NET 10 (Deal modular monolith, Reputation)
- **Async communication:** Apache Kafka (domain events published through a transactional outbox)
- **Sync communication:** gRPC between services, REST/JSON at the edge behind an API Gateway (Envoy: load balancing, rate limiting, JWT validation); no GraphQL
- **Storage:** PostgreSQL as the durable store, Elasticsearch for search and match scoring, no shared cache for now (static reference data such as the category tree and the neighbourhood distances stays in service memory), dedicated Event Store for Reputation
- **Observability:** Logstash (rest of the stack TBD)
- **Testing:** unit, integration and end-to-end tests; scripted load and stress tests kept in the repo, with results recorded

`TBD:` real-time channel specifics, the rest of the observability stack and load-testing tooling. Deployment topology and scaling approach are open as well. There is no service-to-service authentication in phase 1.

## Quick start

### Developer setup

The repository is a monorepo of .NET 10 and Go 1.27 services. You need git, the .NET SDK and Go installed (`make setup` only checks for them, it never installs them).

```bash
git clone <repo-url>
cd Taakht
make setup
```

`make setup` verifies the prerequisites, installs `lefthook` and `golangci-lint` if missing, and registers the git hooks. Everyday commands:

| Command | What it does |
|---|---|
| `make fmt` | Auto-fixes formatting and style for .NET and Go |
| `make lint` | Checks everything without fixing (exactly what CI runs) |
| `make test` | Runs all .NET and Go tests (Go with `-race`) |

On Windows, run `make` from Git Bash. Prerequisites, hooks, CI and troubleshooting are in [Get started](docs/get-started.md).

### Running the demo

The demo is **API-driven**: no product UI (at most a minimal UI for testing), everything is exercised through API calls against seeded data.

`TBD (planned, nothing exists yet):` one command to start the whole system and one to seed fake users, categories and Ads.

Then follow a scripted walkthrough of API calls: log in as a seeded user, publish an Ad, watch a match notification, send competing Swap Requests, negotiate Proposals, approve, lock, settle.

## Repository layout

```text
Taakht/
├── docs/            design documents (see Documentation below)
├── api/             Protobuf contracts for the services and events (planned)
├── libs/            one platform library per language: infrastructure code only (planned)
├── src/             one solution (.NET) or module (Go) per service
├── gateway/         Envoy edge gateway config (REST/JSON to gRPC)
├── spikes/          throwaway experiments behind the evidence docs
├── Makefile         setup, fmt, lint, test
├── lefthook.yml     git hooks (pre-commit fixes, Conventional Commits check)
└── .github/         CI
```

Each .NET service has its own solution file and each Go service its own module. The Makefile and CI discover them automatically, so adding a service needs no configuration change.

## Contributing

- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org), e.g. `feat(reputation): add score projection`. History is linear: we rebase, we do not merge. Keep a branch up to date by rebasing it onto `main`, and merge PRs with "Rebase and merge", so every commit lands on `main` as written. See [development process](docs/process/development-process.md#git-workflow).
- Git hooks fix formatting on staged files; anything they cannot fix mechanically (nullable warnings, unchecked errors, missing `CancellationToken`/`context.Context`) must be fixed by hand.
- CI runs `ci-ok` as the single required check: commit-message and history check, format/lint, build and tests per solution or module, with unrelated stacks skipped.
- Every architecture decision is recorded as an English ADR with a single decision each, see [`docs/adr/`](docs/adr/README.md).
- All documentation and code identifiers are in English; only user-facing messages are localized to Persian.

## Status and known limitations

Known and intentional for the current phase:

- The platform does not hold or move money and does not guarantee that an agreed price difference is paid; this is between the parties.
- A higher KYC level for locker delivery is assumed and mocked.
- Courier and in-person hand-over are not tracked or insured by the platform.
- The platform does not arbitrate item-condition disputes; it only records and forwards claims.
- Informal competitors (Telegram/Instagram swap groups, global swap apps) have not been analyzed yet.
- Item value and price estimation, inter-city exchange and a few other ideas are deliberately parked (see [parked ideas](docs/product/parked-ideas.md)).
- How the lock behaves while the Ad service is unavailable was exercised only by the Taakht MVP's fault-injection tests, not yet by this design's own code.
- The system runs on a single node; there is no fault tolerance against the loss of a node or of infrastructure components.
- Services do not authenticate each other in this phase: the internal network is isolated and only the gateway publishes a port, so it must not be exposed beyond the gateway. Production target is mutual TLS between services.

### What would change in production

- Every service and infrastructure component runs with multiple replicas; the message broker and the durable stores are replicated.
- Matching is sharded (the approach is `TBD`).
- The mocked providers (KYC, locker partner) are replaced by real integrations.
- Performance numbers are only stated where a load test measured them.

## Documentation

[`docs/README.md`](docs/README.md) is the entry point. Where things live:

| Folder | Contents |
|---|---|
| [`docs/product/`](docs/product) | Business case: business summary, business decisions log, parked ideas; later PRD, risk assessment, roadmap |
| [`docs/domain/`](docs/domain) | Ubiquitous language, Event Storming output, bounded contexts, context map |
| [`docs/architecture/`](docs/architecture) | Architecture overview (current state), chronological decisions log, diagrams |
| [`docs/adr/`](docs/adr/README.md) | One architecture decision per file, with the reasoning |
| [`docs/evidence/`](docs/evidence) | Experiments and document reviews behind the ADRs, with what was not verified (the runnable code is in [`spikes/`](spikes)) |
| [`docs/guidelines/`](docs/guidelines) | Conventions that follow from decisions (topic naming, retry/DLQ, code style) |
| [`docs/api/`](docs/api) | API documentation and event contracts |
| [`docs/testing/`](docs/testing) | Test strategy, load/stress scenarios and recorded results |
| [`docs/process/`](docs/process) | How the team works |
| [`docs/open-items.md`](docs/open-items.md) | Undecided questions that feed new ADRs |

When the architecture overview and the decisions log disagree, the overview wins. Some folders are still empty while the design is being migrated into the repository.
