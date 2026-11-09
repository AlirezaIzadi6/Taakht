# Spikes

Throwaway experiments behind the documents in [docs/evidence](../docs/evidence). They exist so a claim like "spike-verified" can be repeated. They are not product code: `make lint` and `make test` and CI skip this folder, and the code is not kept up to date with the services.

| Folder | Backs | What it does |
|---|---|---|
| [gateway](gateway) | [edge-gateway](../docs/evidence/edge-gateway.md), [api-contract-protobuf](../docs/evidence/api-contract-protobuf.md) | Envoy in front of one gRPC service (Go, and a .NET 9 variant): REST to gRPC transcoding from `google.api.http`, JWT validation, rate limiting, error mapping |

Not kept: the grpc-gateway variant (a Go proxy with hand-written JWT and rate-limit middleware) that the edge-gateway evidence compares against Envoy.
