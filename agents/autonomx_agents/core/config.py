"""Configuration loader for agent runtime."""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from pathlib import Path

import yaml


@dataclass
class LlmProviderConfig:
    """LLM provider configuration."""

    name: str
    base_url: str
    api_key: str = ""


@dataclass
class AgentRuntimeConfig:
    """Agent runtime configuration."""

    grpc_port: int = 50051
    database_url: str = ""
    llm_providers: list[LlmProviderConfig] = field(default_factory=list)
    log_level: str = "INFO"


def load_config(config_path: str | Path | None = None) -> AgentRuntimeConfig:
    """Load configuration from YAML file and environment variables."""
    config = AgentRuntimeConfig()

    if config_path and Path(config_path).exists():
        with open(config_path) as f:
            data = yaml.safe_load(f) or {}

        runtime = data.get("agent_runtime", {})
        config.grpc_port = runtime.get("grpc_port", config.grpc_port)
        config.log_level = runtime.get("log_level", config.log_level)

        for provider in runtime.get("llm_providers", []):
            config.llm_providers.append(
                LlmProviderConfig(
                    name=provider["name"],
                    base_url=provider["base_url"],
                    api_key=provider.get("api_key", ""),
                )
            )

    # Environment overrides
    config.grpc_port = int(os.getenv("GRPC_PORT", str(config.grpc_port)))
    config.database_url = os.getenv("DATABASE_URL", config.database_url)

    return config
