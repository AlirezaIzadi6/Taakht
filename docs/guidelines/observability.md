# Observability conventions (draft)

Conventions that follow from [ADR 0020](../adr/0020-use-opentelemetry-for-observability.md). Not an ADR; cheap to change.

Service level objectives are left for later: they need a running system with real data, and any number chosen now would be a guess.

## First alerts

Candidates, built from the metrics the Taakht MVP already exposes. The thresholds are set once there is data to calibrate them.

- The oldest unpublished outbox row is too old (`taakht_outbox_oldest_unpublished_age_seconds`).
- A consumer has not joined its group, or its last processed event is stale (`taakht_consumer_joined`, `taakht_consumer_last_event_timestamp_seconds`).
- Any row in a dead-letter table.
- A swap has waited for the lock longer than the sweeper timeout.
- The database connection pool of a service is exhausted (`taakht_db_pool_connections`).

Each alert gets a short runbook page: what it means, how to look, what to do.

## Testing the alerts

Use the fault-injection scenarios of the MVP: kill the Ad service during the lock, stop Kafka for 15 s and for 70 s, stop Postgres for 10 s. The matching alert must fire, and the trace must show where the request stopped.

## Logs

- JSON lines with `ts`, `level`, `service`, `msg`, `request_id` and `user_id`, the same names in both languages. The .NET services also write their own field names (such as `RequestId`) next to them.
- Loki labels are `service` and `level` only. `request_id` and the other fields stay in the line or in structured metadata: as a label, `request_id` exhausted Loki's default limit of 5,000 active streams within seconds in the test ([evidence](../evidence/observability-stack.md)).
- Lines that are not JSON (the Kafka client library writes some from the .NET services) arrive without labels unless the Collector has a fallback; add one.
- A readiness check that fails while a service starts is not an error for an alert; log it at a lower level.
