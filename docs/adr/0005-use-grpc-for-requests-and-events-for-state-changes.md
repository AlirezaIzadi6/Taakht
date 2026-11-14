# 0005. Use gRPC for requests and events for state changes between services

- Status: Accepted
- Date: 2026-11-13
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

Services ([ADR 0001](0001-build-the-system-as-microservices.md)) talk to each other in two different ways: one service asks another something and waits for the answer, or a service announces that something happened. gRPC was settled early on for internal synchronous calls, and the asynchronous messages turned out to be almost all domain events with several consumers rather than commands (about 15 events against one command at the time, according to the [decisions log](../architecture/architecture-decisions-log.md)). This ADR records the rule; the broker, the event format and the delivery guarantees are separate decisions.

## Decision

We will use gRPC for synchronous requests, where the caller needs the answer to continue, and events through a message broker for state changes that other services react to. An asynchronous command through the broker is allowed only where the sender must not wait; there is none today (the Ad lock claim is a synchronous gRPC call). This ADR gives the rule, not the list of calls: which calls are synchronous is decided per feature.

## Options considered

1. **gRPC for requests, events for state changes (chosen)** - each mechanism is used for what it fits: a request gets an answer, and an event can have several consumers without the producer knowing them (an Ad event goes to Matching, Deal and Communication). Cost: eventual consistency between services, and two mechanisms to learn and run.
2. **Everything asynchronous through a broker, requests included** - one mechanism, but a request and its answer over a log need correlation ids and reply topics, and a user-facing query would wait on that round trip. Reasoning, not tried.
3. **Everything synchronous over gRPC** - simplest to follow, but a producer would have to know and call every consumer, a consumer being down would break the producer's call, and there would be no replayable feed to rebuild a derived index. Reasoning, not tried.

## Consequences

- Positive: services stay decoupled in time for state changes; new consumers of an event need no change in the producer.
- Negative: data copied between services (for example Matching's copy of trust scores) is only eventually consistent ([open-items.md](../open-items.md), item 11); a team member has to decide for each call whether the caller really needs the answer.

## References

- [Decisions log](../architecture/architecture-decisions-log.md), the "Communication and authentication" notes of the 1405/07/02 meeting, and "Message broker: Kafka"
- [Architecture overview](../architecture/architecture-overview.md), "Communication between microservices"
