package main

import (
	"context"
	"errors"
	"log/slog"
	"net"
	"net/http"
	"os"
	"os/signal"
	"path/filepath"
	"syscall"
	"time"

	taxonomyv1 "github.com/AlirezaIzadi6/Taakht/gen/go/taakht/taxonomy/v1"
	"google.golang.org/grpc"
	"google.golang.org/grpc/reflection"
)

const (
	defaultGRPCAddr = ":9201"
	defaultHTTPAddr = ":8201"
)

func main() {
	logger := slog.New(slog.NewTextHandler(os.Stdout, nil))

	grpcAddr := envOrDefault("GRPC_ADDR", defaultGRPCAddr)
	httpAddr := envOrDefault("HTTP_ADDR", defaultHTTPAddr)
	dataDir := envOrDefault("DATA_DIR", defaultDataDir())

	store, err := LoadStore(dataDir)
	if err != nil {
		logger.Error("failed to load taxonomy data", "data_dir", dataDir, "error", err)
		os.Exit(1)
	}
	logger.Info("loaded taxonomy data",
		"data_dir", dataDir,
		"categories", len(store.Categories()),
		"cities", len(store.Cities()),
	)

	var lc net.ListenConfig
	grpcLis, err := lc.Listen(context.Background(), "tcp", grpcAddr)
	if err != nil {
		logger.Error("failed to listen on gRPC address", "addr", grpcAddr, "error", err)
		os.Exit(1)
	}

	ctx, stop := signal.NotifyContext(context.Background(), syscall.SIGINT, syscall.SIGTERM)
	defer stop()

	grpcServer := grpc.NewServer()
	taxonomyv1.RegisterTaxonomyServiceServer(grpcServer, NewServer(store))
	reflection.Register(grpcServer)

	mux := http.NewServeMux()
	mux.HandleFunc("GET /healthz", func(w http.ResponseWriter, _ *http.Request) {
		w.WriteHeader(http.StatusOK)
	})
	httpServer := &http.Server{
		Addr:              httpAddr,
		Handler:           mux,
		ReadHeaderTimeout: 5 * time.Second,
	}

	errCh := make(chan error, 2)
	go func() {
		logger.Info("gRPC server listening", "addr", grpcAddr)
		errCh <- grpcServer.Serve(grpcLis)
	}()
	go func() {
		logger.Info("HTTP health server listening", "addr", httpAddr)
		if err := httpServer.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			errCh <- err
		}
	}()

	select {
	case <-ctx.Done():
		logger.Info("shutting down")
	case err := <-errCh:
		logger.Error("server exited unexpectedly", "error", err)
	}

	shutdownCtx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	grpcServer.GracefulStop()
	if err := httpServer.Shutdown(shutdownCtx); err != nil {
		logger.Error("HTTP server shutdown error", "error", err)
	}
}

func envOrDefault(key, fallback string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return fallback
}

func defaultDataDir() string {
	if exe, err := os.Executable(); err == nil {
		candidate := filepath.Join(filepath.Dir(exe), "data")
		if info, statErr := os.Stat(candidate); statErr == nil && info.IsDir() {
			return candidate
		}
	}
	return "data"
}
