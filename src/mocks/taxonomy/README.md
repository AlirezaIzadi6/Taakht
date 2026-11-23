# Taxonomy mock

Mock implementation of the `TaxonomyProvider` for phase 1. It provides categories, cities, and neighbourhoods from static data files; there is no live connection to Divar's production services.

## Contract

- gRPC: `taakht.taxonomy.v1.TaxonomyService`, defined in `api/taakht/taxonomy/v1/taxonomy_service.proto`.
- RPCs:
  - `ListCategories(ListCategoriesRequest) returns (ListCategoriesResponse)` (edge: `GET /v1/taxonomy/categories`)
  - `ListCities(ListCitiesRequest) returns (ListCitiesResponse)` (edge: `GET /v1/taxonomy/cities`)
  - `ListNeighbourhoods(ListNeighbourhoodsRequest) returns (ListNeighbourhoodsResponse)` (edge: `GET /v1/taxonomy/cities/{city_id}/neighbourhoods` and `GET /v1/taxonomy/neighbourhoods`)
- Responses return complete collections in a single answer without pagination.

## Ports and configuration

- gRPC `9201`, health HTTP `8201`.
- `GRPC_ADDR`, `HTTP_ADDR`, `DATA_DIR` (defaults to `data/` next to the binary).

## Seed data

### Categories

The category tree lives in [`data/categories.json`](data/categories.json). It defines 30 categories across 3 levels:
- Slugs match Divar's standard taxonomy (`home-kitchen`, `personal`, `leisure-hobbies`, `tools-materials-equipment`).
- Scoped to physical, low-value items whose condition is verifiable via photos (furniture, cookware, tableware, bedding, clothing, bags, baby gear, books, stationery, fitness equipment, board games, and hand tools).
- High-risk items (gold/jewelry, watches, vehicles, real estate, and digital goods) are deliberately excluded.

### Cities and neighbourhoods

Cities and neighbourhoods are defined in `data/cities.json` and `data/neighbourhoods.json` (added in #63).
