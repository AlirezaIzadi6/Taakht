# Evidence: the observability stack with the MVP's real logs and metrics

Date: 2026-11-15. Backs [ADR 0020](../adr/0020-use-opentelemetry-for-observability.md) and the [deployment guideline](../guidelines/deployment.md). A test on one laptop; it is not a production measurement.

## Setup

Loki 3.5.0 (single binary, filesystem storage, 72 h retention), Tempo 2.8.1, OpenTelemetry Collector contrib 0.130.0, Prometheus 3.5.0 and Grafana 12.1.0, each in a container with a 1 GiB hard limit (none was killed). The Collector tailed the MVP's four JSON log files (read-only) with the `filelog` receiver and sent them to Loki; Prometheus scraped the four `/metrics` ports of the MVP services. Loki indexed only two labels, `service` and `level`; every other JSON field was kept as structured metadata. Alertmanager was not run.

## What worked

- Logs from all four services reached Loki with the labels `service` and `level`; all fields (`request_id`, `user_id`, `method`, `code`, `duration_ms`) were queryable.
- One `request_id` was followed across all four services, asynchronous consumers included, with one query: all 32 lines of a request came back in 0.02 s, matching the files exactly. This is the design goal of the MVP's request id.
- Prometheus showed the MVP metrics; Grafana queried Loki, Prometheus and Tempo. No dashboard was built.
- 300 synthetic traces went through the Collector to Tempo and were searchable. The MVP itself emits no traces.

## Memory (average over each phase, MiB)

| Component | Idle | 500 log lines/s | 2,000 log lines/s |
|---|---|---|---|
| Loki | 54 | 152 | 342 (peak 456, overall peak 499) |
| Collector | 73 | 100 | 100 |
| Prometheus | 23 | 31 | 32 |
| Tempo | 29 | 38 | 34 |
| Grafana | 91 | 85 | 85 |
| Sum | about 270 | about 405 | about 595 |

Four runs of the demo barely changed these. No drops or ingestion errors. `docker stats` leaves out page cache, so Loki's real footprint is a little higher. The Loki disk was 93 MB after the 500 lines/s run and 437 MB after the 2,000 lines/s run, mostly the write-ahead log. The MVP itself produced about 2,300 log lines in the whole session, far below either synthetic load.

## Query latency

| Query | About 150k lines in the hour | About 750k lines in the hour |
|---|---|---|
| one `request_id` with a line filter | about 0.8 to 1.0 s | about 4.2 s |
| errors in the last 15 minutes (label) | 0.2 s | 0.35 s |
| one service for an hour (label) | 0.3 s | 0.35 s |

Searching a `request_id` as a line filter grows with the data in the window; label queries stay fast.

## `request_id` as a label or not

| Variant (53,640 lines) | Loki memory | Result |
|---|---|---|
| in the line or as structured metadata | 117 MiB | all accepted; lookup 0.08 to 0.11 s |
| as a label, default limits | 125 MiB | rejected at 5,000 active streams (HTTP 429); only 4,980 lines accepted |
| as a label, limit raised to 500,000 | 475 MiB | all accepted; lookup 0.01 s; every other query returns one stream per line |

So `request_id` is not a label. Structured metadata gave 0.013 s at the small size without a stream explosion; it was not measured separately at the larger size.

## Alert

The rule `taakht_outbox_oldest_unpublished_age_seconds > 60` for 2 minutes, with Kafka stopped: above 60 s at 66 s, pending at 91 s, firing at 208 s, cleared about 35 s after Kafka returned. A 90 s outage cannot fire a rule that needs 60 s of age plus 2 minutes. Verified in Prometheus only, so delivery of a notification is untested.

## Problems found in the MVP

- On a fresh Kafka the swap service is not ready until the topic `negotiation.events` exists.
- While not ready, every `/readyz` poll is logged at error level (about 300 lines in 2 minutes), which would pollute an error-rate alert.
- The .NET services print non-JSON lines from the Kafka client library (510 of them); they reach Loki without `service` and `level`, so the Collector needs a fallback.
- The services bind their health port to loopback, so a Prometheus in a container cannot scrape them unless `HEALTH_BIND=0.0.0.0` is set.
- The .NET and Go services name some fields differently (`RequestId` next to `request_id`).

## Not done

Alertmanager and notification delivery; a dashboard; real traces (so Tempo's memory under real trace volume is unknown); Loki's retention and compaction behaviour over days; a Linux server (the log files were on a Windows filesystem).

## Caveats

One laptop with other load; synthetic logs with one uniform shape and about 270 bytes a line; real logs with exceptions will be bigger.
