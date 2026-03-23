#!/usr/bin/env bash
set -euo pipefail

echo "============================================"
echo "  AutoNomX — First-Time Setup"
echo "============================================"

# ── Check prerequisites ──────────────────────────────────────
check_cmd() {
    if ! command -v "$1" &>/dev/null; then
        echo "❌ $1 is required but not installed."
        exit 1
    fi
    echo "✓ $1 found: $($1 --version 2>&1 | head -1)"
}

echo ""
echo "Checking prerequisites..."
check_cmd docker
check_cmd dotnet
check_cmd python3
check_cmd git

# ── .env file ────────────────────────────────────────────────
if [ ! -f .env ]; then
    echo ""
    echo "Creating .env from .env.example..."
    cp .env.example .env
    echo "✓ .env created. Edit it if needed."
fi

# ── Python dependencies ──────────────────────────────────────
echo ""
echo "Installing Python dependencies..."
cd agents
if [ ! -d venv ]; then
    python3 -m venv venv
fi
source venv/bin/activate
pip install -r requirements.txt
pip install -e ".[dev]"
deactivate
cd ..

# ── .NET restore ─────────────────────────────────────────────
echo ""
echo "Restoring .NET packages..."
dotnet restore src/AutoNomX.sln

# ── Proto generation ─────────────────────────────────────────
echo ""
echo "Generating gRPC code..."
bash scripts/proto-gen.sh

# ── Docker images ────────────────────────────────────────────
echo ""
echo "Pulling Docker images..."
docker compose pull postgres ollama

echo ""
echo "============================================"
echo "  Setup complete!"
echo "  Run 'make run-dev' to start infrastructure"
echo "  Run 'make build' to build the solution"
echo "============================================"
