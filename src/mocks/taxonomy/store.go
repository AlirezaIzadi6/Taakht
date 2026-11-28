package main

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"

	taxonomyv1 "github.com/AlirezaIzadi6/Taakht/gen/go/taakht/taxonomy/v1"
)

type categoryJSON struct {
	ID       string `json:"id"`
	ParentID string `json:"parent_id"`
	NameFA   string `json:"name_fa"`
	NameEN   string `json:"name_en"`
}

type cityJSON struct {
	ID     string `json:"id"`
	NameFA string `json:"name_fa"`
	NameEN string `json:"name_en"`
}

type coordinatesJSON struct {
	Latitude  float64 `json:"latitude"`
	Longitude float64 `json:"longitude"`
}

type neighbourhoodJSON struct {
	ID     string          `json:"id"`
	CityID string          `json:"city_id"`
	NameFA string          `json:"name_fa"`
	NameEN string          `json:"name_en"`
	Center coordinatesJSON `json:"center"`
	Active bool            `json:"active"`
}

// Store holds the in-memory taxonomy dataset loaded from JSON files.
type Store struct {
	categories                 []*taxonomyv1.Category
	cities                     []*taxonomyv1.City
	activeNeighbourhoodsByCity map[string][]*taxonomyv1.Neighbourhood
}

// LoadStore reads categories.json, cities.json, and neighbourhoods.json from dataDir.
func LoadStore(dataDir string) (*Store, error) {
	rawCategories, err := readJSON[[]categoryJSON](filepath.Join(dataDir, "categories.json"))
	if err != nil {
		return nil, fmt.Errorf("load categories: %w", err)
	}

	rawCities, err := readJSON[[]cityJSON](filepath.Join(dataDir, "cities.json"))
	if err != nil {
		return nil, fmt.Errorf("load cities: %w", err)
	}

	rawNeighbourhoods, err := readJSON[[]neighbourhoodJSON](filepath.Join(dataDir, "neighbourhoods.json"))
	if err != nil {
		return nil, fmt.Errorf("load neighbourhoods: %w", err)
	}

	categories := make([]*taxonomyv1.Category, 0, len(rawCategories))
	for _, c := range rawCategories {
		categories = append(categories, &taxonomyv1.Category{
			Id:       c.ID,
			ParentId: c.ParentID,
			NameFa:   c.NameFA,
			NameEn:   c.NameEN,
		})
	}

	cities := make([]*taxonomyv1.City, 0, len(rawCities))
	for _, c := range rawCities {
		cities = append(cities, &taxonomyv1.City{
			Id:     c.ID,
			NameFa: c.NameFA,
			NameEn: c.NameEN,
		})
	}

	activeByCity := make(map[string][]*taxonomyv1.Neighbourhood)
	for _, n := range rawNeighbourhoods {
		if !n.Active {
			continue
		}
		activeByCity[n.CityID] = append(activeByCity[n.CityID], &taxonomyv1.Neighbourhood{
			Id:     n.ID,
			CityId: n.CityID,
			NameFa: n.NameFA,
			NameEn: n.NameEN,
			Center: &taxonomyv1.Coordinates{
				Latitude:  n.Center.Latitude,
				Longitude: n.Center.Longitude,
			},
			Active: n.Active,
		})
	}

	return &Store{
		categories:                 categories,
		cities:                     cities,
		activeNeighbourhoodsByCity: activeByCity,
	}, nil
}

// Categories returns all loaded categories.
func (s *Store) Categories() []*taxonomyv1.Category {
	return s.categories
}

// Cities returns all loaded cities.
func (s *Store) Cities() []*taxonomyv1.City {
	return s.cities
}

// Neighbourhoods returns active neighbourhoods for the given city ID.
func (s *Store) Neighbourhoods(cityID string) []*taxonomyv1.Neighbourhood {
	return s.activeNeighbourhoodsByCity[cityID]
}

func readJSON[T any](path string) (T, error) {
	var zero T
	data, err := os.ReadFile(path)
	if err != nil {
		return zero, err
	}
	var out T
	if err := json.Unmarshal(data, &out); err != nil {
		return zero, fmt.Errorf("parse %s: %w", path, err)
	}
	return out, nil
}
