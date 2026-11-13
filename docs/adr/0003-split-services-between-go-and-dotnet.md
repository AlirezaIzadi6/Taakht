# 0003. Split services between Go and .NET by workload

- Status: Accepted
- Date: 2026-11-12
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

Two people work on the project, one in Go and one in .NET, and learning both stacks in a distributed setting is a goal. Service boundaries are ownership boundaries ([ADR 0001](0001-build-the-system-as-microservices.md)), so the language rule has to be simple and have a stated reason.

## Decision

We will use Go for services whose main work is concurrency and throughput, and .NET for services whose main work is rich domain state. Assignment: Go for Ad, Matching and Communication; .NET for Deal (negotiation and settlement) and Reputation.

## Options considered

1. **Split by workload (chosen)** - Matching and Ad are the services expected to take the most load; Communication (chat and notification fan-out) is the third. Deal and Reputation are dominated by state machines, events and domain rules. Ownership follows the language, one person per stack. Cost: two toolchains, two CI stacks, and common infrastructure code (interceptors, clients) written twice.
2. **Everything in .NET** - one stack, simplest to run and review, but gives up the second language the team wants to learn.
3. **Everything in Go** - one stack, but the same trade-off in the other direction.
4. **Choose per service by preference, with no stated rule** - maximum freedom, but the reasoning would be invisible to a reader.

## Consequences

- Positive: both languages are used in a real distributed setting; the two people work in parallel, each in their own stack.
- Negative: duplicated tooling and infrastructure libraries; two sets of dependencies and security updates; cross-language debugging.
- Known gaps: the split rests on the learning goal and on a workload rule of thumb. No benchmark or prototype shows that either language is a better fit for any service, and the correctness of the exclusive lock does not depend on the language (it rests on database constraints). Communication's real-time channel is not designed yet.
- Revisit when a service's workload changes class, ownership changes, or running two stacks costs more than it teaches.

## References

- [ADR 0001: Build the system as microservices](0001-build-the-system-as-microservices.md)
- [Evidence: API contract in Protobuf](../evidence/api-contract-protobuf.md) (shows only that a Go and a .NET service can be built from one proto behind one gateway)
