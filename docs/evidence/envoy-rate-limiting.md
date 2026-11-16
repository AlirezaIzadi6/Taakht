# Evidence: per-client rate limiting with Envoy's local rate limit

Date: 2026-11-16. Backs the [rate limiting conventions](../guidelines/rate-limiting.md) and refines the claim in [ADR 0008](../adr/0008-use-envoy-for-rest-grpc-transcoding.md) that rate limiting is configuration. A quick test with the Envoy 1.34.14 image on one laptop, using `direct_response` routes and `curl`.

## What was tested

Three setups, each with a limit of 3 requests per minute:

| Setup | Result |
|---|---|
| One token bucket per route (what the earlier spike used) | The bucket is shared by all clients: client `a` used the three tokens, then client `b` got `429` on its first request. |
| One bucket per value of the header `x-user-id` (descriptors with a key and no value, `max_dynamic_descriptors: 1000`, and the descriptor built by a `rate_limits` entry inside the filter config) | Works: `u1` got `429` after three requests while `u2` still got `200`. A request without the header was not limited by this bucket. |
| One bucket per client address, from the `remote_address` descriptor | The mechanism is the same (one bucket per key). Which address Envoy used could not be shown here: all test requests came from the same host address, and forcing a client address through `X-Forwarded-For` did not give separate buckets in two attempts (with the HTTP connection manager's `xff_num_trusted_hops` and with the XFF original-IP detection extension). |

## Findings

- A per-client limit needs descriptors; a plain route bucket limits everyone together, so one abuser can use up the quota of all.
- Per-key buckets work with `local_ratelimit` alone, with no shared store, so no Redis. The limits are per Envoy instance.
- A request that lacks the descriptor key is not limited by that descriptor, so an anonymous request needs an address-based descriptor.
- The `rate_limits` field on the route is ignored by the filter's per-route configuration; Envoy 1.34 logs a warning that the embedded `rate_limits` field inside the filter config is the supported way, and that is what worked.
- Several descriptors in one request (one for the address, one for the user) were not tested together.

## Not verified

The client address behind another proxy (the X-Forwarded-For handling above); the address when Envoy is the public entry with `use_remote_address: true` (the intended deployment, and the one that needs no header); several descriptors at once; memory under many distinct keys beyond the configured limit; behaviour with the real JWT filter in front.
