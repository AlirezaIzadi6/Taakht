# Auth mock

Mock implementation of the `AuthProvider` for phase 1. It issues RS256 JWTs for the seeded users; there is no real identity provider, registration, session or revocation.

## Contract

- gRPC: `taakht.auth.v1.AuthService/Login`, defined in `api/taakht/auth/v1/auth_service.proto`.
- Edge: Envoy exposes it as `POST /v1/auth/login` with `{"username": "...", "password": "..."}` (#59); the response carries `access_token` and `expires_at`. The password field is accepted but ignored in phase 1.
- Token claims: `sub` (user id), `role` (`user` or `admin`), `iss`, `aud`, `iat`, `nbf`, `exp`; one-hour lifetime (`TOKEN_TTL`); header `kid=dev-1`.
- An unknown username returns `UNAUTHENTICATED`; a missing username returns `INVALID_ARGUMENT` with `google.rpc.BadRequest` field details.

## Ports and configuration

- gRPC `9204`, health HTTP `8204`.
- `GRPC_ADDR`, `HTTP_ADDR`, `JWT_PRIVATE_KEY_PATH`, `JWT_KEY_ID`, `JWT_ISSUER`, `JWT_AUDIENCE`, `TOKEN_TTL`.
- `USERS_FILE_PATH` points at the seeded user file; the default is `data/users.json` next to the application.

## Seeded users

The file [`data/users.json`](data/users.json) holds 2010 users: the ten named users and the generated `u0001`–`u2000`. It is loaded at startup and committed. `ali`, `sara`, `reza`, `mina`, `hossein`, `zahra`, `amir`, `neda`, `kian` have role `user`; `admin` has role `admin`; the generated users are all `user`. The model stores no password: the `password` field is accepted and ignored, so any password, including an empty one, works for a seeded username.

The file is generated from `tools/seed-users`; do not hand-edit it. `make gen-users` regenerates it and `make gen-users-check` verifies it is current.

## Run and test

```bash
make dev-keys
make up
# or, without Docker:
dotnet run --project src/mocks/auth/src/AuthMock.Api
dotnet test src/mocks/auth/AuthMock.slnx
```
