# 0007. Use Envoy as the edge gateway for REST to gRPC transcoding

- Status: Accepted
- Date: 2026-11-12
- Deciders: @AlirezaIzadi6, @ArmanIzadi99
- Bounded context(s): cross-cutting

## Context

External clients speak REST/JSON ([ADR 0006](0006-use-rest-json-at-the-edge-without-graphql.md)); services speak gRPC in Go and .NET ([ADR 0003](0003-split-services-between-go-and-dotnet.md)). The README places a gateway at the edge for load balancing, rate limiting and JWT validation. We must choose what translates REST to gRPC.

What matters: the REST mapping comes from the Protobuf contract (`google.api.http`, [ADR 0005](0005-define-api-contract-in-protobuf.md)) and not from a second configuration; it works for both languages; JWT validation and rate limiting can live in the same place; and replacing it later stays cheap (see [design principles](../architecture/design-principles.md)).

## Decision

We will run Envoy as the single public entry point and use its `grpc_json_transcoder` filter, driven by a descriptor built from the shared `.proto` files. Only RPCs with an HTTP annotation are public (`auto_mapping: false`). The configuration lives in `gateway/envoy.yaml`. Where JWTs are validated is decided separately.

## Options considered

1. **Envoy `grpc_json_transcoder` (chosen)** - one translation point for every service language. JWT validation (`jwt_authn`) and rate limiting (`local_ratelimit`) are configuration, not code we write. Cost: a 175 MB image, YAML to maintain, and a descriptor change needs an Envoy restart.
2. **Transcoding inside each service** (ASP.NET Core gRPC JSON transcoding) - runs in-process with no extra hop, but covers .NET only, so Go services would need a different mechanism. Microsoft's docs limit it to server streaming, and the `Grpc.Swagger` OpenAPI package is deprecated with no replacement.
3. **Standalone grpc-gateway proxy (Go)** - uses the same `google.api.http` annotations, so switching later is cheap, and the image is small (27 MB in the spike). It transcodes correctly, but JWT validation, rate limiting and claim forwarding are code we write and own (73 lines in the spike). A proto change means regenerate, rebuild and redeploy.
4. **Other API gateways** - APISIX's `grpc-transcode` plugin does not use `google.api.http` annotations (the HTTP mapping is configured per route), which would create a second source of truth next to the proto. Kong's docs did not say whether it uses annotations; not evaluated further.
5. **Connect** - the docs we read list no .NET support, so it cannot serve both stacks.

## Consequences

- Positive: one place for translation and edge concerns; services stay pure gRPC; transcoding could move to grpc-gateway later because both read the same annotations.
- Negative: Envoy configuration must be learned; a descriptor change needs a restart (about 1.2 s in the spike, dropping in-flight requests), and hot reload through xDS was not tried; moving away from Envoy would also mean reimplementing its JWT and rate-limit configuration.
- Follow-ups: build `descriptor.binpb` and `openapi.yaml` from the same commit in CI (generated, not committed); the gateway's deployment topology is open ([open-items.md](../open-items.md), item 23).

## References

- [Evidence: edge gateway](../evidence/edge-gateway.md) (transcoding, error mapping, JWT and rate limiting worked in a spike with one hello-world service; `match_incoming_request_route: true` is mandatory; grpc-gateway compared in the same spike)
- Envoy gRPC-JSON transcoder: https://www.envoyproxy.io/docs/envoy/latest/configuration/http/http_filters/grpc_json_transcoder_filter
- ASP.NET Core gRPC JSON transcoding: https://learn.microsoft.com/en-us/aspnet/core/grpc/json-transcoding
- grpc-gateway: https://github.com/grpc-ecosystem/grpc-gateway
