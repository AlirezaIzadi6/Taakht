# Rate limiting at the edge (draft)

Conventions for the limits that Envoy applies at the edge. Not an ADR: the mechanism (Envoy's local rate limit, no shared store) follows [ADR 0008](../adr/0008-use-envoy-for-rest-grpc-transcoding.md), and the numbers are configuration. The test behind this is in the [evidence](../evidence/envoy-rate-limiting.md).

## Two layers

1. **By client address, on every route:** a coarse shield. Anonymous browsing is only limited this way. With Envoy as the public entry (`use_remote_address: true`) the address is the real peer; behind another proxy the client address has to be taken from `X-Forwarded-For`, which is not verified yet.
2. **By user id, on the actions that can be abused by a logged-in user:** creating and editing an Ad, opening a negotiation, sending a chat message, reporting abuse. The id is `x-user-id`, which Envoy sets from the verified JWT ([ADR 0009](../adr/0009-validate-jwts-at-the-edge.md)), so the user limit sits after the JWT filter.

## Rules

- Use one bucket per key (descriptors without a value and a cap on the number of keys), not one bucket per route: a route bucket is shared by all clients.
- The limits are per Envoy instance. With one Envoy that is the real limit; with several, each has its own count.
- The numbers are set when there is data to calibrate them; none is proposed here.
- Rejected requests answer `429`.
