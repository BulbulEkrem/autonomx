#!/usr/bin/env bash
set -euo pipefail

PROTO_DIR="protos"
PYTHON_OUT="agents/autonomx_agents/grpc_services/generated"
CSHARP_OUT="src/core/AutoNomX.Infrastructure/Grpc/Generated"

echo "==> Proto generation starting..."

# ── Create output directories ────────────────────────────────
mkdir -p "$PYTHON_OUT"
mkdir -p "$CSHARP_OUT"

# ── Generate Python gRPC code ────────────────────────────────
echo "  Generating Python gRPC code..."
python3 -m grpc_tools.protoc \
    -I"$PROTO_DIR" \
    --python_out="$PYTHON_OUT" \
    --grpc_python_out="$PYTHON_OUT" \
    --pyi_out="$PYTHON_OUT" \
    "$PROTO_DIR"/*.proto

# Create __init__.py for generated package
touch "$PYTHON_OUT/__init__.py"

echo "  ✓ Python code generated in $PYTHON_OUT"

# ── C# generation is handled by Grpc.Tools in .csproj ───────
echo "  ✓ C# code will be generated during dotnet build (via Grpc.Tools)"

echo "==> Proto generation complete."
