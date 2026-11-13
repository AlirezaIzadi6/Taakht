# 0008. Validate JWTs at the Envoy edge

- Status: Accepted
- Date: 2026-11-12
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

Clients authenticate with JWTs (in phase 1 `AuthProvider` is a mock that issues a token for seeded users). The README places JWT validation in the API gateway ([ADR 0007](0007-use-envoy-for-rest-grpc-transcoding.md)). Services are written in Go and .NET. We must decide where tokens are verified and how services learn who the caller is, including when one service calls another on behalf of a user.

## Decision

We will verify JWTs (signature, expiry, issuer, audience) in Envoy with the `jwt_authn` filter and reject invalid requests before they reach a service. Envoy forwards only the claims services need (initially the user id as `x-user-id`) as gRPC metadata, using `claim_to_headers`. Services read the user identity through a gRPC interceptor (each service keeps its own copy, see [ADR 0004](0004-use-a-monorepo.md)) and pass it unchanged on outgoing calls. `jwt_authn` has no revocation, so we rely on short-lived tokens; the lifetime is set when the identity provider is chosen.

## Options considered

1. **Verify at Envoy, forward only needed claims (chosen)** - one verification point for both languages, invalid requests dropped early, services stay simple. Cost: services trust the metadata, so its trustworthiness depends on how callers reach them.
2. **Forward the whole JWT and verify it again in every service** - independent of the network and safer if a service is reached directly, but duplicates token and key handling in Go and .NET.
3. **Verify only in the services** - no edge dependency, but unauthenticated traffic reaches every service and the README's gateway responsibility is lost.

## Consequences

- Positive: consistent authentication in one place; simpler services.
- Negative: a service reachable without passing through Envoy would accept forged identity metadata (the README relies on an isolated internal network for now; how services authenticate each other is decided separately); no token revocation; authorization decisions still belong to each service because `jwt_authn` only checks issuer and audience.
- Follow-ups: keep every service route covered by a `jwt_authn` rule, because the header protection was verified only on routes matched by a rule; choose the identity provider, key rotation and token lifetime.

## References

- [Evidence: edge gateway](../evidence/edge-gateway.md) (`jwt_authn` rejected missing and expired tokens with 401; on a covered route a forged `x-user-id` was overwritten or dropped; tried only with a hello-world service)
- Envoy `jwt_authn` filter documentation
