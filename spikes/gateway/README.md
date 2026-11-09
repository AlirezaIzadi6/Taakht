# Gateway spike

Envoy v1.34 as the public edge in front of a one-method gRPC service (`HelloService.GetGreeting`, `GET /v1/greetings/{name}`). The results are in [docs/evidence/edge-gateway.md](../../docs/evidence/edge-gateway.md).

## Run it

Go backend:

```bash
docker compose up --build
```

.NET 9 backend (same gateway and contract):

```bash
docker compose -f docker-compose.dotnet.yml up --build
```

Envoy listens on `localhost:18080`. Try the REST call and look at the backend log for `SEEN x-user-id`:

```bash
curl -i http://localhost:18080/v1/greetings/world
```

`token.py` prints an HS256 test token (throwaway secret `spike-test-secret-spike-test-0123`, issuer `spike-issuer`, subject `user-42`) for the JWT checks:

```bash
python token.py
curl -i -H "Authorization: Bearer $(python token.py)" http://localhost:18080/v1/greetings/world
```

## Layout

| Path | Purpose |
|---|---|
| `api/proto` | The contract with `google.api.http` annotations |
| `third_party/google/api` | Vendored googleapis protos needed by the annotations |
| `gateway/envoy.yaml`, `gateway/descriptor.binpb` | Envoy config and the compiled proto descriptor |
| `backend` | Go gRPC service (generated code in `backend/gen`) |
| `dotnet-backend` | The same service in .NET 9 |
| `buf.yaml`, `buf.gen.yaml`, `buf.lock`, `baseline.binpb` | buf lint, code generation and the breaking-change baseline |

The product's own Envoy config is [gateway/envoy.yaml](../../gateway/envoy.yaml) at the repo root; this folder's copy is the experiment's.
