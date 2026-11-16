# Evidence

Records of the experiments and document reviews behind ADRs. An ADR keeps only the conclusion; the details live here so the ADR stays short and the claim can still be checked.

Each file states the date, the setup, the results, and what was **not** verified. The experiment code is throwaway. The gateway experiment is kept under [`spikes/`](../../spikes) so it can be repeated; the key configuration and commands are also included inline. Test credentials used in experiments were throwaway values and are not recorded.

| File | Backs ADR |
|---|---|
| [api-contract-protobuf.md](api-contract-protobuf.md) | API contract in Protobuf |
| [edge-gateway.md](edge-gateway.md) | Envoy transcoding, JWT at the edge |
| [kafka-events.md](kafka-events.md) | Kafka events in Protobuf |
| [microservices-vs-monolith.md](microservices-vs-monolith.md) | Build the system as microservices |
| [elasticsearch-matching-load-test.md](elasticsearch-matching-load-test.md) | Elasticsearch for the Matching index |
| [mvp-stack-memory.md](mvp-stack-memory.md) | Deployment guideline (memory of the MVP stack) |
| [observability-stack.md](observability-stack.md) | Observability stack (ADR 0020) and the deployment guideline |
| [envoy-rate-limiting.md](envoy-rate-limiting.md) | Rate limiting conventions (Envoy's local rate limit) |
