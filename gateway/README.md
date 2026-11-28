# Envoy edge gateway

The single public entry point for the API: REST/JSON in, gRPC out, JWT validation at the edge. The product config is `gateway/envoy.yaml` (ADR 0008); the spike under `spikes/gateway` is the experiment.

## Ports

- `8080` — public edge (the only published port in `deploy/compose/compose.yaml`).
- `9901` — Envoy admin, internal only.

## Routes

| Route | Upstream | JWT | Notes |
|---|---|---|---|
| `POST /v1/auth/login` (`/v1/auth/*`) | `auth:9204` | no | Public route; issues the RS256 token. |
| `GET /v1/taxonomy/*` | `taxonomy:9201` | no | Public route; categories, cities, and active neighbourhoods. |
| `POST /v1/ads` (`/v1/ads`) | `ad:9090` | yes | The Ad service is not in Compose yet; a valid request returns 503. |
| Matching | `matching:9090` | yes | Placeholder in the config comments. |
| Deal | `deal:9090` | yes | Placeholder. |
| Reputation | `reputation:9090` | yes | Placeholder. |
| Communication | `communication:9090` | yes | Placeholder. |

`jwt_authn` rules are ordered: `/v1/auth/` and `/v1/taxonomy/` allow a missing JWT; `/` requires the `taakht` provider. Unknown paths with no token get 401; with a valid token they reach the router and get 404.

## JWT

- Issuer `taakht-auth-mock`, audience `taakht-api`, RS256, `kid=dev-1`.
- Envoy reads only the public JWKS from `/keys/jwks.json` (mounted from `.dev-keys/jwks.json`; `make up` generates it).
- `claim_to_headers`: `sub` → `x-user-id`, `role` → `x-user-role`.
- The access log prints both headers so claim forwarding can be checked without an upstream.

## Build and run

```bash
make up          # builds the image if missing and starts the stack

# after changing gateway/envoy.yaml or api/:
make gateway     # docker compose ... build gateway
```

The image builds `descriptor.binpb` from `api/` with `buf`; the descriptor and config are baked, the JWKS is a runtime mount. Rebuild after a proto or config change.

## Smoke test

If 8080 is reserved on Windows, set `GATEWAY_PORT=18080` in `deploy/compose/.env` and replace `8080` with `18080` below.

```bash
TOKEN=$(curl -sS -X POST http://localhost:8080/v1/auth/login \
  -H 'content-type: application/json' \
  -d '{"username":"ali","password":"anything"}' | jq -r .access_token)

curl -i http://localhost:8080/v1/ads                                          # 401
curl -i -H "Authorization: Bearer $TOKEN" http://localhost:8080/v1/ads       # 503 (no ad:9090), not 401
curl -i -H "Authorization: Bearer $TOKEN" -H 'x-user-id: attacker' \
  http://localhost:8080/v1/ads
docker compose --project-directory deploy/compose logs gateway | grep 'user=ali role=user'
```

The last log line must show `user=ali`, not `attacker`; that proves the claim header is set from the token. The Ad route's 503 is expected until the Ad service lands; the JWT was still accepted.

## Notes

- The descriptor is generated, not committed (ADR 0008); the Dockerfile builds it from the current `api/`.
- The JWKS and the auth mock's private key are throwaway dev keys; `make dev-keys` creates them when missing.
- Config changes require a rebuild; the descriptor is read at startup.
- Rate limiting, TLS and the server reverse proxy are later work.
