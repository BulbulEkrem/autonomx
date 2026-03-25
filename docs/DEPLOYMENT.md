# AutoNomX Deployment Guide

## Table of Contents

- [Development Environment Setup](#development-environment-setup)
- [Docker Setup (Recommended)](#docker-setup-recommended)
- [Ollama Model Setup](#ollama-model-setup)
- [LM Studio Configuration](#lm-studio-configuration)
- [PostgreSQL Setup](#postgresql-setup)
- [Environment Variables](#environment-variables)
- [Running the System](#running-the-system)
- [Troubleshooting](#troubleshooting)

---

## Development Environment Setup

### Prerequisites

| Tool | Version | Installation |
|------|---------|--------------|
| .NET SDK | 8.0+ | [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download) |
| Python | 3.11+ | [python.org](https://python.org) |
| Docker | 24+ | [docker.com](https://docker.com) |
| Docker Compose | v2+ | Included with Docker Desktop |
| Git | 2.40+ | [git-scm.com](https://git-scm.com) |
| Ollama | Latest | [ollama.com](https://ollama.com) |

### Linux (Ubuntu/Debian)

```bash
# .NET SDK
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0

# Python 3.11+
sudo apt update
sudo apt install python3.11 python3.11-venv python3-pip

# Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER

# Ollama
curl -fsSL https://ollama.com/install.sh | sh

# Clone and setup
git clone https://github.com/BulbulEkrem/autonomx.git
cd autonomx
cp .env.example .env
make setup
```

### macOS

```bash
# Homebrew (if not installed)
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"

# .NET SDK
brew install dotnet-sdk

# Python 3.11+
brew install python@3.11

# Docker Desktop
brew install --cask docker

# Ollama
brew install ollama

# Clone and setup
git clone https://github.com/BulbulEkrem/autonomx.git
cd autonomx
cp .env.example .env
make setup
```

### Windows

```powershell
# Using winget (Windows Package Manager)
winget install Microsoft.DotNet.SDK.8
winget install Python.Python.3.11
winget install Docker.DockerDesktop
winget install Ollama.Ollama

# Clone and setup
git clone https://github.com/BulbulEkrem/autonomx.git
cd autonomx
copy .env.example .env

# Build
dotnet build src/AutoNomX.sln
pip install -e ./agents
```

---

## Docker Setup (Recommended)

The easiest way to run AutoNomX is with Docker Compose.

### Full Stack

```bash
# Start everything: PostgreSQL, API, Agent Runtime, Ollama
docker-compose up -d

# Check status
docker-compose ps

# View logs
docker-compose logs -f
```

### Development Mode

For development, run only infrastructure and build .NET/Python locally:

```bash
# Start only PostgreSQL and Ollama
docker-compose up -d postgres ollama

# Build and run .NET locally
dotnet build src/AutoNomX.sln
dotnet run --project src/cli/AutoNomX.Cli -- new "My project" --dry-run

# Install and run Python agents locally
pip install -e ./agents
python -m autonomx_agents.server
```

### Docker Compose Services

| Service | Port | Description |
|---------|------|-------------|
| `postgres` | 5432 | PostgreSQL 16 database |
| `api` | 5000 | .NET Core Web API + SignalR |
| `agent-runtime` | 50051 | Python gRPC agent server |
| `ollama` | 11434 | Local LLM inference |

### Building Docker Images

```bash
# Build all images
make build-docker

# Or individually
docker build -f docker/Dockerfile.api -t autonomx-api .
docker build -f docker/Dockerfile.agents -t autonomx-agents .
```

---

## Ollama Model Setup

AutoNomX uses Ollama for local LLM inference by default. You need to pull the required models before running.

### Required Models

```bash
# Core reasoning model (Product Owner, Planner, Model Manager)
ollama pull deepseek-r1:14b

# Coding model (Architect, Tester, entry-level coders)
ollama pull qwen2.5-coder:14b

# High-performance coding model (top-tier coders)
ollama pull qwen2.5-coder:32b

# Optional: Additional coding models
ollama pull deepseek-coder-v2:16b
ollama pull codellama:13b
```

### Verify Models

```bash
ollama list
```

### GPU Requirements

| Model | VRAM (Minimum) | VRAM (Recommended) |
|-------|----------------|---------------------|
| deepseek-r1:14b | 10 GB | 12 GB |
| qwen2.5-coder:14b | 10 GB | 12 GB |
| qwen2.5-coder:32b | 20 GB | 24 GB |
| deepseek-coder-v2:16b | 12 GB | 14 GB |
| codellama:13b | 8 GB | 10 GB |

If you don't have enough VRAM, use smaller quantized versions (e.g., `qwen2.5-coder:14b-q4_K_M`) or switch to API models (OpenAI/Anthropic).

### Ollama Configuration

Ollama runs on `http://localhost:11434` by default. To change:

```bash
# Set custom host
export OLLAMA_HOST=0.0.0.0:11434

# Increase context window (default 2048)
export OLLAMA_NUM_CTX=8192

# Enable GPU layers
export OLLAMA_NUM_GPU=999
```

---

## LM Studio Configuration

LM Studio is an alternative local LLM provider with a GUI.

### Setup

1. Download and install [LM Studio](https://lmstudio.ai)
2. Download the desired models through the LM Studio UI
3. Start the local server (Settings > Local Server > Start)
4. Default endpoint: `http://localhost:1234/v1`

### Configure AutoNomX

Update `config/llm.yaml`:

```yaml
providers:
  lm_studio:
    name: lm_studio
    base_url: http://localhost:1234/v1
    api_key: "not-needed"  # LM Studio doesn't require auth
    priority: 1
```

Update agent models in `config/agents.yaml` to use LM Studio format:

```yaml
agents:
  planner:
    type: planner
    model: "lm_studio/your-model-name"
    provider: lm_studio
```

---

## PostgreSQL Setup

### Via Docker (Recommended)

```bash
docker-compose up -d postgres
```

This creates a PostgreSQL 16 instance with the configuration from `.env`.

### Manual Installation

```bash
# Ubuntu/Debian
sudo apt install postgresql-16

# macOS
brew install postgresql@16

# Create database
createdb autonomx
```

### Connection String

Set in `.env`:

```
POSTGRES_USER=autonomx
POSTGRES_PASSWORD=autonomx_dev
POSTGRES_DB=autonomx
POSTGRES_PORT=5432
```

The .NET application reads the connection string from `appsettings.json` or constructs it from environment variables:

```
Host=localhost;Port=5432;Database=autonomx;Username=autonomx;Password=autonomx_dev
```

### Database Migrations

Migrations are managed by Entity Framework Core:

```bash
# Apply migrations
dotnet ef database update --project src/core/AutoNomX.Infrastructure --startup-project src/api/AutoNomX.Api

# Create a new migration
dotnet ef migrations add MigrationName --project src/core/AutoNomX.Infrastructure --startup-project src/api/AutoNomX.Api
```

---

## Environment Variables

Copy the example file and customize:

```bash
cp .env.example .env
```

### Complete Reference

| Variable | Default | Description |
|----------|---------|-------------|
| `POSTGRES_USER` | `autonomx` | PostgreSQL username |
| `POSTGRES_PASSWORD` | `autonomx_dev` | PostgreSQL password |
| `POSTGRES_DB` | `autonomx` | PostgreSQL database name |
| `POSTGRES_PORT` | `5432` | PostgreSQL port |
| `API_PORT` | `5000` | .NET API port |
| `ASPNETCORE_ENVIRONMENT` | `Development` | .NET environment |
| `GRPC_PORT` | `50051` | Python agent gRPC port |
| `OLLAMA_PORT` | `11434` | Ollama API port |
| `LLM_GATEWAY_URL` | `http://ollama:11434` | LLM gateway base URL |
| `OPENAI_API_KEY` | _(empty)_ | OpenAI API key (optional) |
| `ANTHROPIC_API_KEY` | _(empty)_ | Anthropic API key (optional) |
| `DATABASE_URL` | _(constructed)_ | Full connection string override |
| `LOG_LEVEL` | `Information` | Logging level |
| `WORKER_POOL_SIZE` | `2` | Default worker pool size |
| `WORKSPACE_PATH` | `./workspace` | Runtime project workspace |

---

## Running the System

### First Run

```bash
# 1. Setup environment
cp .env.example .env
make setup

# 2. Start infrastructure
docker-compose up -d postgres ollama

# 3. Pull LLM models
ollama pull deepseek-r1:14b
ollama pull qwen2.5-coder:14b
ollama pull qwen2.5-coder:32b

# 4. Build
make build
pip install -e ./agents

# 5. Generate gRPC code (if needed)
make proto

# 6. Test
make test

# 7. Start agent runtime
python -m autonomx_agents.server &

# 8. Run your first project
dotnet run --project src/cli/AutoNomX.Cli -- new "Build a calculator" --dry-run
```

### Daily Development

```bash
# Start infrastructure
make run-dev

# Build and test
make build && make test

# Run CLI
dotnet run --project src/cli/AutoNomX.Cli -- <command>
```

---

## Troubleshooting

### Common Issues

#### "Connection refused" to PostgreSQL

```bash
# Check if PostgreSQL is running
docker-compose ps postgres

# Check logs
docker-compose logs postgres

# Verify port
netstat -tlnp | grep 5432
```

**Fix:** Ensure `POSTGRES_PORT` in `.env` matches your PostgreSQL instance.

#### "Connection refused" to Ollama

```bash
# Check if Ollama is running
ollama list

# If using Docker
docker-compose logs ollama

# Check port
curl http://localhost:11434/api/tags
```

**Fix:** Start Ollama with `ollama serve` or `docker-compose up -d ollama`.

#### "Model not found" in Ollama

```bash
# List available models
ollama list

# Pull missing model
ollama pull qwen2.5-coder:32b
```

#### gRPC connection failed

```bash
# Check agent runtime
python -m autonomx_agents.server  # Should show "Server started on port 50051"

# Verify port is open
netstat -tlnp | grep 50051
```

**Fix:** Ensure `GRPC_PORT` matches in both `.env` and `config/llm.yaml`.

#### "Out of memory" with large models

**Fix:** Use smaller models or quantized versions:
```bash
ollama pull qwen2.5-coder:14b-q4_K_M  # Smaller quantization
```

Or switch to API models in `config/agents.yaml`:
```yaml
agents:
  coder:
    model: "gpt-4o"
    provider: openai
```

#### Proto generation fails

```bash
# Install protoc
# Linux:
sudo apt install protobuf-compiler
# macOS:
brew install protobuf

# Install Python gRPC tools
pip install grpcio-tools

# Regenerate
make proto
```

#### .NET build errors after pulling

```bash
# Clean and rebuild
make clean
dotnet restore src/AutoNomX.sln
make build
```

#### Docker permission denied (Linux)

```bash
sudo usermod -aG docker $USER
newgrp docker  # or log out and back in
```

### Getting Help

- Check the [Architecture docs](ARCHITECTURE.md) for design decisions
- See [Plugin Guide](PLUGIN-GUIDE.md) for extending the system
- Review agent logs: `autonomx logs <project-id> --agent coder`
- File issues: [github.com/BulbulEkrem/autonomx/issues](https://github.com/BulbulEkrem/autonomx/issues)
