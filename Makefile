.PHONY: help setup build test run proto clean lint
.DEFAULT_GOAL := help

# ── Variables ────────────────────────────────────────────────
PROTO_DIR     := protos
DOTNET_DIR    := src
PYTHON_DIR    := agents
DOCKER_COMPOSE := docker compose

# ── Help ─────────────────────────────────────────────────────
help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | \
		awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-20s\033[0m %s\n", $$1, $$2}'

# ── Setup ────────────────────────────────────────────────────
setup: ## First-time setup (install dependencies)
	@echo "==> Setting up AutoNomX..."
	@bash scripts/setup.sh

# ── Proto Generation ─────────────────────────────────────────
proto: ## Generate C# + Python code from .proto files
	@echo "==> Generating gRPC code from proto files..."
	@bash scripts/proto-gen.sh

# ── Build ────────────────────────────────────────────────────
build: ## Build .NET solution
	@echo "==> Building .NET solution..."
	dotnet build $(DOTNET_DIR)/AutoNomX.sln

build-docker: ## Build all Docker images
	@echo "==> Building Docker images..."
	$(DOCKER_COMPOSE) build

# ── Test ─────────────────────────────────────────────────────
test: test-dotnet test-python ## Run all tests

test-dotnet: ## Run .NET tests
	@echo "==> Running .NET tests..."
	dotnet test $(DOTNET_DIR)/AutoNomX.sln

test-python: ## Run Python tests
	@echo "==> Running Python tests..."
	cd $(PYTHON_DIR) && python -m pytest tests/ -v

# ── Run ──────────────────────────────────────────────────────
run: ## Start all services (docker-compose up)
	$(DOCKER_COMPOSE) up -d

run-dev: ## Start only infrastructure (postgres + ollama)
	$(DOCKER_COMPOSE) up -d postgres ollama

stop: ## Stop all services
	$(DOCKER_COMPOSE) down

logs: ## Show service logs
	$(DOCKER_COMPOSE) logs -f

# ── Lint ─────────────────────────────────────────────────────
lint: ## Run linters
	@echo "==> Linting Python..."
	cd $(PYTHON_DIR) && python -m ruff check .
	@echo "==> Linting .NET..."
	dotnet format $(DOTNET_DIR)/AutoNomX.sln --verify-no-changes

# ── Clean ────────────────────────────────────────────────────
clean: ## Clean build artifacts
	@echo "==> Cleaning..."
	find $(DOTNET_DIR) -type d \( -name bin -o -name obj \) -exec rm -rf {} + 2>/dev/null || true
	find $(PYTHON_DIR) -type d -name __pycache__ -exec rm -rf {} + 2>/dev/null || true
	rm -rf $(PYTHON_DIR)/.pytest_cache
