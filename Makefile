SHELL := bash
.DEFAULT_GOAL := help

# Pin these once the team settles on versions.
LEFTHOOK_VERSION ?= latest
GOLANGCI_LINT_VERSION ?= latest
BUF_VERSION ?= latest

# Every .NET service has its own solution; every Go service has its own module.
# spikes/ holds throwaway experiments behind the evidence docs; they are not linted or tested.
DOTNET_SOLUTIONS := $(shell find . \( -name '*.sln' -o -name '*.slnx' \) -not -path '*/node_modules/*' -not -path './spikes/*')
# gen/go only holds code generated from api/, so it is not linted or tested.
GO_MODULES := $(shell find . -name go.mod -not -path '*/node_modules/*' -not -path './spikes/*' -not -path './gen/*' -exec dirname {} \;)

.PHONY: help setup prereqs tools hooks dev-keys generate gen-users gen-users-check gateway up up-server down reset fmt lint test

help: ## Show available targets
	@grep -E '^[a-z-]+:.*## ' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*## "} {printf "  %-10s %s\n", $$1, $$2}'

setup: prereqs tools hooks dev-keys ## One-time developer environment setup

prereqs: ## Verify git, .NET SDK, Go and Docker are installed (reports only, never installs)
	@missing=0; \
	for tool in git dotnet go docker; do \
		if command -v $$tool >/dev/null 2>&1; then echo "ok       $$tool"; \
		else echo "MISSING  $$tool (see docs/get-started.md, Prerequisites)"; missing=1; fi; \
	done; \
	exit $$missing
	@dotnet --version >/dev/null || { echo "The .NET SDK required by global.json is not installed"; exit 1; }

tools: ## Install lefthook, golangci-lint and buf if they are not already on PATH
	@command -v lefthook >/dev/null 2>&1 || { echo "Installing lefthook..."; go install github.com/evilmartians/lefthook@$(LEFTHOOK_VERSION); }
	@command -v golangci-lint >/dev/null 2>&1 || { echo "Installing golangci-lint..."; go install github.com/golangci/golangci-lint/v2/cmd/golangci-lint@$(GOLANGCI_LINT_VERSION); }
	@command -v buf >/dev/null 2>&1 || { echo "Installing buf..."; go install github.com/bufbuild/buf/cmd/buf@$(BUF_VERSION); }
	@for tool in lefthook golangci-lint buf; do \
		command -v $$tool >/dev/null 2>&1 || { echo "$$tool was installed but is not on PATH. Add $$(go env GOPATH)/bin to PATH and re-run."; exit 1; }; \
	done

hooks: ## Install git hooks defined in lefthook.yml
	lefthook install

dev-keys: ## Generate the RS256 dev key pair and JWKS in .dev-keys/ (git-ignored)
	@dotnet run --project tools/dev-keys -- --out .dev-keys

generate: ## Generate Go code from api/ with buf (not committed)
	buf generate

gen-users: ## Regenerate the auth mock's user file (10 named + 2000 generated)
	dotnet run --project tools/seed-users -- --out "$(CURDIR)/src/mocks/auth/data/users.json"

gen-users-check: ## Verify the committed user file matches the generator
	dotnet run --project tools/seed-users -- --check --out "$(CURDIR)/src/mocks/auth/data/users.json"

gateway: ## Rebuild the Envoy gateway image after a proto or gateway config change
	docker compose --project-directory deploy/compose build gateway

up: dev-keys ## Start the local stack with Docker Compose (Postgres, Kafka, auth and the gateway; topics are created first)
	docker compose --project-directory deploy/compose up -d --wait

up-server: dev-keys ## Start the stack as on the shared server: the base file only; only the gateway publishes a port
	docker compose --project-directory deploy/compose -f deploy/compose/compose.yaml up -d --wait

down: ## Stop the Docker Compose stack (data volumes are kept)
	docker compose --project-directory deploy/compose down

reset: ## Stop the stack and delete its data volumes (Postgres and Kafka start empty, init scripts run again)
	docker compose --project-directory deploy/compose down --volumes

fmt: ## Auto-fix formatting and style
	@for s in $(DOTNET_SOLUTIONS); do dotnet format "$$s" || exit 1; done
	@for m in $(GO_MODULES); do (cd $$m && golangci-lint fmt ./...) || exit 1; done

lint: generate gen-users-check ## Check everything without fixing (same as CI)
	@for s in $(DOTNET_SOLUTIONS); do \
		dotnet format "$$s" --verify-no-changes && dotnet build "$$s" -c Release || exit 1; \
	done
	@for m in $(GO_MODULES); do (cd $$m && golangci-lint run ./...) || exit 1; done

test: generate ## Run all tests
	@for s in $(DOTNET_SOLUTIONS); do dotnet test "$$s" || exit 1; done
	@for m in $(GO_MODULES); do (cd $$m && go test -race ./...) || exit 1; done
