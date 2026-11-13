# 0009. Authenticate service-to-service calls: isolated network now, mTLS in production

- Status: Accepted
- Date: 2026-11-13
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

Services trust the user identity that Envoy forwards ([ADR 0008](0008-validate-jwts-at-the-edge.md)). That trust is only sound if nothing but Envoy and the other services can reach them. The README lists service-to-service authentication as TBD with API keys proposed. Phase 1 is a single-node demo with a mock `AuthProvider`; production must run with replicas and real isolation.

From the gRPC and ASP.NET Core documentation (not tested):

- gRPC treats TLS as the foundation for service-to-service traffic, with client certificates for mutual authentication.
- In .NET, `CallCredentials` (bearer tokens, and therefore API keys) are only applied on a TLS-secured channel unless explicitly overridden.
- So token or API-key schemes need TLS anyway; mTLS adds only a client certificate on top of that.

## Decision

In phase 1 we will not enforce service identity between services. The internal network is isolated (only Envoy publishes a port) and the accepted risk is documented in the README limitations. Each service creates its gRPC channels and interceptors in one place, so authentication can be added there later. The production target is mutual TLS between services; the mechanism for issuing and rotating certificates (scripted, a service mesh, or SPIFFE/SPIRE) is decided together with the deployment topology ([open-items.md](../open-items.md), item 23).

## Options considered

1. **Trust the isolated network (phase 1, chosen)** - no cost now and no wasted work. Risk: a compromised container or an exposed port allows forged identity.
2. **mTLS from day one with scripted certificates** - strongest and demonstrable, but every Go and .NET service needs certificate plumbing before any domain logic exists, which the [design principles](../architecture/design-principles.md) do not justify yet.
3. **API keys over TLS (the README's proposal)** - needs TLS like mTLS does, plus a shared secret to rotate and a coarse identity. Not simpler than mTLS and weaker.
4. **Internal token issuer (client-credentials style)** - allows fine-grained claims, but adds a security-critical component to build and run.

## Consequences

- Positive: no early plumbing; a clear upgrade path; the risk is explicit.
- Negative: phase 1 offers no defence if the internal network is breached, and the demo must not be exposed beyond the gateway. Services share no infrastructure code ([ADR 0004](0004-use-a-monorepo.md)), so adding mTLS later means changing each service's channel setup, not one library.
- Follow-ups: define the channel and interceptor setup for Go and .NET in a convention doc; decide the certificate mechanism with the deployment topology and then write the mTLS ADR (this one would be superseded); decide Kafka client authentication separately.

## References

- Reasoning from documentation; nothing was tried in a spike.
- gRPC authentication guide; ASP.NET Core gRPC authentication and authorization; SPIFFE overview
