# 0001. Build the system as microservices

- Status: Accepted
- Date: 2026-11-11
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

The system is designed for the scale the business needs and runs as a single-node demo. The team is two people working in two languages (Go and .NET). Three drivers, kept separate on purpose:

- **Product:** matching quality and liquidity only exist on a large platform, so matching and search are expected to scale and change differently from the rest. This is an expectation, not a measurement.
- **Demonstration:** the project is a portfolio piece meant to show how concurrency and eventual consistency are handled. Those problems come from process and data boundaries, so the boundaries are part of the point.
- **Team and learning:** a service boundary is also an ownership boundary between the two people. Learning distributed systems in practice is an explicit goal.

## Decision

We will build the system as independently deployable services, each owning its data. The working boundaries are Ad, Matching, Deal and Reputation, plus a minimal Communication service. They are not final: they are settled in the service decomposition item ([open-items.md](../open-items.md), item 3) before each service is implemented.

## Options considered

1. **Microservices from the start (chosen)** - serves all three drivers; boundaries are enforced by processes, not discipline; the two people work in parallel. Cost: the microservice premium (deployment, monitoring, eventual consistency, failure handling) lands on two people, and a wrong boundary is expensive to move.
2. **Modular monolith** - the lowest-risk start and the usual advice for new systems. Rejected: it does not show the distributed problems the project is meant to show, and one deployable does not fit two people in two languages.
3. **A few coarse services, split later** - the chosen option is already coarse (five services, three of them core); going coarser would remove the Ad, Deal and Matching boundaries where the concurrency and consistency problems appear.

## Consequences

- Positive: independent deployment and scaling; firm boundaries; real concurrency and consistency problems; parallel work.
- Negative: slower start; operational overhead for two people; cross-service changes need coordinated contracts; a wrong boundary means moving code, possibly across languages.
- Known gaps: no measurement shows any service needs independent scaling, and the boundaries are not yet validated against the domain. This is the main risk.
- Revisit when services are repeatedly changed together, velocity is unacceptable, or measurements show no service needs independent scaling and the demonstration goal has changed.

## References

- [Design principles](../architecture/design-principles.md)
- [Evidence: microservices versus monolith](../evidence/microservices-vs-monolith.md) (reasoning from published guidance, not an experiment)
