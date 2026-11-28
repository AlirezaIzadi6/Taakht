package main

import (
	"context"
	"net"
	"os"
	"path/filepath"
	"testing"

	taxonomyv1 "github.com/AlirezaIzadi6/Taakht/gen/go/taakht/taxonomy/v1"
	"google.golang.org/grpc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/credentials/insecure"
	"google.golang.org/grpc/status"
	"google.golang.org/grpc/test/bufconn"
)

const bufSize = 1024 * 1024

func writeFixtureData(t *testing.T) string {
	t.Helper()

	dir := t.TempDir()
	files := map[string]string{
		"categories.json": `[
			{"id": "cat-root", "parent_id": "", "name_fa": "ریشه", "name_en": "Root"},
			{"id": "cat-child", "parent_id": "cat-root", "name_fa": "فرزند", "name_en": "Child"}
		]`,
		"cities.json": `[
			{"id": "city-a", "name_fa": "شهر الف", "name_en": "City A"},
			{"id": "city-b", "name_fa": "شهر ب", "name_en": "City B"}
		]`,
		"neighbourhoods.json": `[
			{
				"id": "nb-a-active",
				"city_id": "city-a",
				"name_fa": "محله فعال",
				"name_en": "Active NB",
				"center": {"latitude": 35.7, "longitude": 51.4},
				"active": true
			},
			{
				"id": "nb-a-inactive",
				"city_id": "city-a",
				"name_fa": "محله غیرفعال",
				"name_en": "Inactive NB",
				"center": {"latitude": 35.6, "longitude": 51.3},
				"active": false
			},
			{
				"id": "nb-b-active",
				"city_id": "city-b",
				"name_fa": "محله ب",
				"name_en": "NB B",
				"center": {"latitude": 29.6, "longitude": 52.5},
				"active": true
			}
		]`,
	}

	for name, content := range files {
		if err := os.WriteFile(filepath.Join(dir, name), []byte(content), 0o600); err != nil {
			t.Fatalf("write fixture %s: %v", name, err)
		}
	}

	return dir
}

func startTestClient(t *testing.T, dataDir string) taxonomyv1.TaxonomyServiceClient {
	t.Helper()

	store, err := LoadStore(dataDir)
	if err != nil {
		t.Fatalf("LoadStore failed: %v", err)
	}

	lis := bufconn.Listen(bufSize)
	srv := grpc.NewServer()
	taxonomyv1.RegisterTaxonomyServiceServer(srv, NewServer(store))

	go func() {
		_ = srv.Serve(lis)
	}()
	t.Cleanup(func() {
		srv.Stop()
		_ = lis.Close()
	})

	conn, err := grpc.NewClient(
		"passthrough:///bufnet",
		grpc.WithContextDialer(func(context.Context, string) (net.Conn, error) {
			return lis.Dial()
		}),
		grpc.WithTransportCredentials(insecure.NewCredentials()),
	)
	if err != nil {
		t.Fatalf("grpc.NewClient failed: %v", err)
	}
	t.Cleanup(func() { _ = conn.Close() })

	return taxonomyv1.NewTaxonomyServiceClient(conn)
}

func TestListCategories(t *testing.T) {
	client := startTestClient(t, writeFixtureData(t))

	resp, err := client.ListCategories(context.Background(), &taxonomyv1.ListCategoriesRequest{})
	if err != nil {
		t.Fatalf("ListCategories failed: %v", err)
	}
	if len(resp.GetCategories()) != 2 {
		t.Fatalf("expected 2 categories, got %d", len(resp.GetCategories()))
	}

	got := map[string]string{}
	for _, c := range resp.GetCategories() {
		got[c.GetId()] = c.GetParentId()
	}
	if got["cat-root"] != "" || got["cat-child"] != "cat-root" {
		t.Fatalf("unexpected categories map: %v", got)
	}
}

func TestListCities(t *testing.T) {
	client := startTestClient(t, writeFixtureData(t))

	resp, err := client.ListCities(context.Background(), &taxonomyv1.ListCitiesRequest{})
	if err != nil {
		t.Fatalf("ListCities failed: %v", err)
	}
	if len(resp.GetCities()) != 2 {
		t.Fatalf("expected 2 cities, got %d", len(resp.GetCities()))
	}

	ids := map[string]bool{}
	for _, c := range resp.GetCities() {
		ids[c.GetId()] = true
	}
	if !ids["city-a"] || !ids["city-b"] {
		t.Fatalf("expected city-a and city-b, got %v", ids)
	}
}

func TestListNeighbourhoods(t *testing.T) {
	client := startTestClient(t, writeFixtureData(t))

	t.Run("active only for valid city", func(t *testing.T) {
		resp, err := client.ListNeighbourhoods(context.Background(), &taxonomyv1.ListNeighbourhoodsRequest{
			CityId: "city-a",
		})
		if err != nil {
			t.Fatalf("ListNeighbourhoods failed: %v", err)
		}
		if len(resp.GetNeighbourhoods()) != 1 {
			t.Fatalf("expected 1 active neighbourhood, got %d", len(resp.GetNeighbourhoods()))
		}
		nb := resp.GetNeighbourhoods()[0]
		if nb.GetId() != "nb-a-active" || !nb.GetActive() || nb.GetCenter() == nil {
			t.Fatalf("unexpected neighbourhood: %+v", nb)
		}
	})

	t.Run("empty list for unknown city", func(t *testing.T) {
		resp, err := client.ListNeighbourhoods(context.Background(), &taxonomyv1.ListNeighbourhoodsRequest{
			CityId: "unknown-city",
		})
		if err != nil {
			t.Fatalf("expected no error for unknown city, got: %v", err)
		}
		if len(resp.GetNeighbourhoods()) != 0 {
			t.Fatalf("expected 0 neighbourhoods, got %d", len(resp.GetNeighbourhoods()))
		}
	})

	t.Run("invalid argument when city_id is empty", func(t *testing.T) {
		_, err := client.ListNeighbourhoods(context.Background(), &taxonomyv1.ListNeighbourhoodsRequest{
			CityId: "   ",
		})
		if status.Code(err) != codes.InvalidArgument {
			t.Fatalf("expected InvalidArgument, got %v", status.Code(err))
		}
	})
}

func TestLoadStore_RealData(t *testing.T) {
	store, err := LoadStore("data")
	if err != nil {
		t.Fatalf("LoadStore(data) failed: %v", err)
	}
	if len(store.Categories()) == 0 {
		t.Fatal("expected non-empty categories in data/categories.json")
	}
	if len(store.Cities()) == 0 {
		t.Fatal("expected non-empty cities in data/cities.json")
	}

	for _, city := range store.Cities() {
		for _, nb := range store.Neighbourhoods(city.GetId()) {
			if nb.GetCityId() != city.GetId() {
				t.Fatalf("neighbourhood %q has city_id %q, want %q", nb.GetId(), nb.GetCityId(), city.GetId())
			}
			if !nb.GetActive() {
				t.Fatalf("expected only active neighbourhoods, got inactive %q", nb.GetId())
			}
			if nb.GetCenter() == nil {
				t.Fatalf("expected coordinates for %q", nb.GetId())
			}
		}
	}
}
