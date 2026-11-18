# API conventions (draft)

Conventions that follow from the Protobuf API contract decision. Not an ADR; cheap to change.

## Versioning

- The major version is part of both the REST path and the proto package: `/v1/users/{id}` and `taakht.user.v1`.
- Compatible changes (new optional fields, new RPCs) stay in `v1`. A breaking change requires a new `v2` package and path, with both versions served during migration.
- `buf breaking` runs against `main` in CI and blocks accidental breaking changes.

## Layout

- One directory and one proto package per service and version: `api/taakht/<service>/v1/`, package `taakht.<service>.v1`.
- The API and the events of a service share the package but live in separate files: `<service>_service.proto` and `<service>_events.proto` (for example `ad_service.proto` and `ad_events.proto`). Event messages are never reused in the API.

## Errors

- Services return standard gRPC status codes with `google.rpc.Status` details.
- Envoy maps them to HTTP status codes and a JSON `google.rpc.Status` body (`convert_grpc_status`).
- Request validation errors use `google.rpc.BadRequest` details with field names.

## REST mapping

- Only RPCs with a `google.api.http` annotation are public; Envoy auto-mapping stays disabled.
- Each service owns one path prefix at the edge.

## Caller identity

- The user id reaches services as the `x-user-id` metadata key, set by Envoy from the verified JWT (`sub` claim). Services read it only through the identity interceptor of the platform library of their language (ADR 0004) and pass it unchanged on outgoing calls.
- Verified in a spike (2026-10-06): a client-sent `x-user-id` is overwritten or dropped on routes covered by a `jwt_authn` rule.
- Development tokens are signed with RS256 (decided 2026-11-18), as a real identity provider would. Envoy gets only the public key as JWKS; a small dev tool holds the private key and issues tokens for the seeded users. The spike used an HS256 secret, so the RS256 setup on Envoy is not tried yet. Both keys are throwaway dev keys.

## Generated code

- Code generated from `api/` (Go stubs, .NET gRPC classes, the gateway descriptor, the OpenAPI document) is not committed. It is produced at build time and ignored by git.
- Go: `buf generate` writes to `gen/go`, a Go module of its own (module file committed, generated files ignored) that services find with a `replace` directive like the platform library. It is run by a `make generate` target that `make lint` and `make test` depend on, and by CI before the Go jobs. .NET: the `Grpc.Tools` package generates the classes during the build.
- Reason: the `.proto` files stay the only source of truth, and generated diffs never need review or cause merge conflicts. Cost: building needs `buf` installed (`make tools` installs it) and its plugins.
