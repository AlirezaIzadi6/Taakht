# Evidence: Elasticsearch for Matching, small-heap load test

Date: 2026-11-15. Backs [ADR 0019](../adr/0019-use-elasticsearch-for-the-matching-index.md). A test on one laptop with synthetic data; it is not a production measurement.

## Setup

Elasticsearch 9.5.6 (the newest stable tag confirmed that day), one node, one shard, zero replicas, security off, bound to 127.0.0.1, container limit 2.5 GB. Heap 1 GB for all sizes, then 1.5 GB at 1,000,000 Ads. The laptop has 16 logical CPUs and a Docker VM of about 8 GB; other containers (SQL Server, redis, nginx) were running and the load tool ran on the same machine.

## Data and model

- A category tree of 385 nodes (4 levels), one city with 300 neighbourhoods and a Euclidean distance matrix kept in the load tool, as the application would keep it in memory.
- Ads with random have and want sides: 45% of wants with no category, 70% with no neighbourhood, 40% with a distance step (2, 5, 10 or 20 km), a trust score from 0 to 100.
- Seven fields, all keywords or small numbers: `have_cat`, `have_hood`, `want_cats`, `want_hoods`, `max_dist`, `trust`, `created`. An empty "want" is stored as the value `*`, so "anything" is an ordinary term.
- Category compatibility ("same, ancestor or descendant") is a `terms` filter whose lists the client expands from the in-memory tree; no ancestor paths are stored on the document.
- Scoring uses `function_score` with filter weights (100 per category-fit tier, 10 per closeness bucket, trust times 0.09). No scripts. The weights are placeholders, not tuned.
- Query 1 is "candidates for a new Ad" (two-way filter, top 20). Query 2 is the one-way search (category and neighbourhood filters, sorted by fit, trust, newest).

## Results at a 1 GB heap

Latency in ms (p50 / p99), 30 s per cell, closed-loop clients, no errors, no HTTP 429 or 503.

| Ads | Index size | Heap used after queries | Container memory peak | Query 1, 1 client | Query 1, 50 clients | Query 2, 50 clients |
|---|---|---|---|---|---|---|
| 100k | 12 MB | 363 MB | 1.96 GiB | 4.7 / 11.7 | 32 / 74 | 14 / 40 |
| 500k | 59 MB | 460 MB | 2.02 GiB | 7.8 / 34.7 | 68 / 208 | 16 / 45 |
| 1M | 97 MB | 386 MB | 2.04 GiB | 11 / 42 | 201 / 476 | 30 / 65 |

Query 1 at 1M ran at about 227 queries per second with 50 clients. Bulk indexing ran at 60k to 155k documents per second.

## Results at a 1.5 GB heap, 1M Ads

Query 1 at 50 clients: p50 155 ms (p99 445). Query 2: 21 ms (p99 58). Heap used 1009 MB after queries, container peak 2.16 GiB, young-GC time fell from 15 s to 3 s. Same stability.

## What the memory is

The container used about the heap plus 1 GB: 1.7 GiB for a 1 GB heap even at 10k Ads, 2.0 GiB at 1M, and 2.1 to 2.2 GiB with a 1.5 GB heap. The rest is direct memory, JVM metaspace and thread stacks, and the file cache. The index itself is tiny (97 MB at 1M Ads), so the heap and the container limit were what mattered.

## Findings from the documentation

- `terms` accepts 65,536 terms by default; our lists have at most a few hundred.
- `function_score` scores every matching document, which is why query 1 slows with the number of matches (at 1M Ads a query matched 3,500 on median and up to 220,000).
- `script_score` also scores every hit and was avoided; `rank_feature` needs strictly positive single values, so a trust score of 0 is a problem.
- The node query cache (10% of heap) churns with unique term lists.
- One shard is right at this size; more shards would only add overhead. A single node with no replicas loses data if the disk is lost.

## Caveats

Synthetic, uniform data; real categories and neighbourhoods are skewed, which changes how many documents a query matches. One laptop with other load, and the load tool on the same machine, so high-client numbers are pessimistic. One run per cell. No concurrent indexing and querying. No block rule, city filter or once-per-pair logic. The ceiling above 1M Ads was not found.

Source: the report of the test run; the scripts and raw results were kept outside the repository.
