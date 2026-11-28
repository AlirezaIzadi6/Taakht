package main

import (
	"context"
	"strings"

	taxonomyv1 "github.com/AlirezaIzadi6/Taakht/gen/go/taakht/taxonomy/v1"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
)

// Server implements taakht.taxonomy.v1.TaxonomyServiceServer backed by a Store.
type Server struct {
	taxonomyv1.UnimplementedTaxonomyServiceServer
	store *Store
}

// NewServer constructs a TaxonomyServiceServer backed by store.
func NewServer(store *Store) *Server {
	return &Server{store: store}
}

// ListCategories returns the complete category tree.
func (s *Server) ListCategories(_ context.Context, _ *taxonomyv1.ListCategoriesRequest) (*taxonomyv1.ListCategoriesResponse, error) {
	return &taxonomyv1.ListCategoriesResponse{
		Categories: s.store.Categories(),
	}, nil
}

// ListCities returns all supported cities.
func (s *Server) ListCities(_ context.Context, _ *taxonomyv1.ListCitiesRequest) (*taxonomyv1.ListCitiesResponse, error) {
	return &taxonomyv1.ListCitiesResponse{
		Cities: s.store.Cities(),
	}, nil
}

// ListNeighbourhoods returns active neighbourhoods for the requested city.
func (s *Server) ListNeighbourhoods(_ context.Context, req *taxonomyv1.ListNeighbourhoodsRequest) (*taxonomyv1.ListNeighbourhoodsResponse, error) {
	cityID := strings.TrimSpace(req.GetCityId())
	if cityID == "" {
		return nil, status.Error(codes.InvalidArgument, "city_id is required")
	}

	return &taxonomyv1.ListNeighbourhoodsResponse{
		Neighbourhoods: s.store.Neighbourhoods(cityID),
	}, nil
}
