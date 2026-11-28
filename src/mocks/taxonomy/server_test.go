package main

import (
	"context"
	"net"
	"testing"

	taxonomyv1 "github.com/AlirezaIzadi6/Taakht/gen/go/taakht/taxonomy/v1"
	"google.golang.org/grpc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/credentials/insecure"
	"google.golang.org/grpc/status"
	"google.golang.org/grpc/test/bufconn"
)

const bufSize = 1024 * 1024

func startTestClient(t *testing.T) taxonomyv1.TaxonomyServiceClient {
	t.Helper()

	store, err := LoadStore("data")
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
	client := startTestClient(t)

	resp, err := client.ListCategories(context.Background(), &taxonomyv1.ListCategoriesRequest{})
	if err != nil {
		t.Fatalf("ListCategories failed: %v", err)
	}
	if len(resp.GetCategories()) != 30 {
		t.Fatalf("expected 30 categories, got %d", len(resp.GetCategories()))
	}

	var foundRoot bool
	for _, c := range resp.GetCategories() {
		if c.GetId() == "home-kitchen" && c.GetParentId() == "" {
			foundRoot = true
			break
		}
	}
	if !foundRoot {
		t.Fatal("expected root category home-kitchen")
	}
}

func TestListCities(t *testing.T) {
	client := startTestClient(t)

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
	if !ids["tehran"] || !ids["shiraz"] {
		t.Fatalf("expected tehran and shiraz, got %v", ids)
	}
}

func TestListNeighbourhoods(t *testing.T) {
	client := startTestClient(t)

	t.Run("active only for valid city", func(t *testing.T) {
		resp, err := client.ListNeighbourhoods(context.Background(), &taxonomyv1.ListNeighbourhoodsRequest{
			CityId: "tehran",
		})
		if err != nil {
			t.Fatalf("ListNeighbourhoods failed: %v", err)
		}
		if len(resp.GetNeighbourhoods()) != 28 {
			t.Fatalf("expected 28 active tehran neighbourhoods, got %d", len(resp.GetNeighbourhoods()))
		}
		for _, n := range resp.GetNeighbourhoods() {
			if n.GetCityId() != "tehran" {
				t.Fatalf("unexpected city_id %q", n.GetCityId())
			}
			if !n.GetActive() {
				t.Fatalf("expected only active neighbourhoods, got inactive %q", n.GetId())
			}
			if n.GetCenter() == nil {
				t.Fatalf("expected coordinates for %q", n.GetId())
			}
		}
	})

	t.Run("empty list for unknown city", func(t *testing.T) {
		resp, err := client.ListNeighbourhoods(context.Background(), &taxonomyv1.ListNeighbourhoodsRequest{
			CityId: "isfahan",
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
