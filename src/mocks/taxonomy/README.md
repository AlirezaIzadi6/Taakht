# Taxonomy mock

Mock implementation of the `TaxonomyProvider` for phase 1. It provides categories, cities, and neighbourhoods from static data files; there is no live connection to Divar's production services.

## Contract

- gRPC: `taakht.taxonomy.v1.TaxonomyService`, defined in `api/taakht/taxonomy/v1/taxonomy_service.proto`.
- RPCs:
  - `ListCategories(ListCategoriesRequest) returns (ListCategoriesResponse)` (edge: `GET /v1/taxonomy/categories`)
  - `ListCities(ListCitiesRequest) returns (ListCitiesResponse)` (edge: `GET /v1/taxonomy/cities`)
  - `ListNeighbourhoods(ListNeighbourhoodsRequest) returns (ListNeighbourhoodsResponse)` (edge: `GET /v1/taxonomy/cities/{city_id}/neighbourhoods`)
- Responses return complete collections in a single answer without pagination.
- `ListNeighbourhoods` requires `city_id` (`InvalidArgument` when empty), returns an empty list for an unknown `city_id`, and returns only `active: true` neighbourhoods.

## Ports and configuration

- gRPC `9201`, health HTTP `8201` (`GET /healthz`).
- `GRPC_ADDR` (default `:9201`), `HTTP_ADDR` (default `:8201`), `DATA_DIR` (defaults to `data/` next to the binary).

## Seed data

### Categories

The category tree lives in [`data/categories.json`](data/categories.json). It defines 30 categories across 3 levels:
- Slugs match Divar's standard taxonomy (`home-kitchen`, `personal`, `leisure-hobbies`, `tools-materials-equipment`).
- Scoped to physical, low-value items whose condition is verifiable via photos (furniture, cookware, tableware, bedding, clothing, bags, baby gear, books, stationery, fitness equipment, board games, and hand tools).
- High-risk items (gold/jewelry, watches, vehicles, real estate, and digital goods) are deliberately excluded.

### Cities and neighbourhoods

Cities and neighbourhoods are defined in [`data/cities.json`](data/cities.json) (2 cities: Tehran and Shiraz) and [`data/neighbourhoods.json`](data/neighbourhoods.json) (542 neighbourhoods: 364 in Tehran with 28 active pilot neighbourhoods, and 178 in Shiraz with 19 active pilot neighbourhoods), generated once by [`tools/seed-neighbourhoods/extract.py`](../../../tools/seed-neighbourhoods/extract.py).

- **Selection & identifiers:** Extracted from OSM `way` and `relation` elements tagged `place=neighbourhood` inside each city's administrative boundary, excluding non-neighbourhood, industrial, cemetery, and numeric-code entries. Each neighbourhood has a globally unique slug `id` prefixed by city (`thr-` / `shz-`), Persian and English names, and an `active` flag marking the dense central pilot clusters.
- **Centres:** Computed via Overpass `out center` (the bounding-box centre of each polygon, averaged when a neighbourhood is split across multiple polygons).
- **Source & licence:** Derived from OpenStreetMap (https://www.openstreetmap.org) via Overpass API, licensed under the Open Database License (ODbL 1.0) — see [`data/ATTRIBUTION.md`](data/ATTRIBUTION.md).
