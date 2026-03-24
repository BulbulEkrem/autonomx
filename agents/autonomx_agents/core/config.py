"""Configuration models for agent runtime — Pydantic-based, YAML-loadable."""

from __future__ import annotations

import os
from pathlib import Path
from typing import Any

import yaml
from pydantic import BaseModel, Field


class LlmParameters(BaseModel):
    """LLM generation parameters."""

    temperature: float = 0.7
    max_tokens: int = 4096
    top_p: float = 1.0
    stop: list[str] = Field(default_factory=list)


class AgentConfig(BaseModel):
    """Configuration for a single agent — loaded from agents.yaml."""

    name: str
    type: str
    model: str
    provider: str = "ollama"
    description: str = ""
    parameters: LlmParameters = Field(default_factory=LlmParameters)
    system_prompt: str = ""
    tools: list[str] = Field(default_factory=list)


class CoderWorkerTemplate(BaseModel):
    """Template for coder worker pool."""

    count: int = 1
    model: str
    provider: str = "ollama"


class CoderPoolSettings(BaseModel):
    """Coder pool behavior settings."""

    max_retries: int = 3
    task_pick_strategy: str = "smart"  # smart | fifo | priority
    file_lock_mode: str = "hard"  # hard | soft


class CoderPoolConfig(BaseModel):
    """Coder worker pool configuration."""

    workers: list[CoderWorkerTemplate] = Field(default_factory=list)
    settings: CoderPoolSettings = Field(default_factory=CoderPoolSettings)


class LlmProviderConfig(BaseModel):
    """LLM provider connection configuration."""

    name: str
    base_url: str
    api_key: str = ""


class AgentRuntimeConfig(BaseModel):
    """Top-level agent runtime configuration."""

    grpc_port: int = 50051
    database_url: str = ""
    log_level: str = "INFO"
    llm_providers: list[LlmProviderConfig] = Field(default_factory=list)
    agents: dict[str, AgentConfig] = Field(default_factory=dict)
    coder_pool: CoderPoolConfig = Field(default_factory=CoderPoolConfig)


def load_agents_config(config_path: str | Path) -> dict[str, AgentConfig]:
    """Load agent definitions from agents.yaml."""
    path = Path(config_path)
    if not path.exists():
        return {}

    with open(path) as f:
        data = yaml.safe_load(f) or {}

    agents: dict[str, AgentConfig] = {}
    for name, agent_data in data.get("agents", {}).items():
        agent_data["name"] = name
        agents[name] = AgentConfig(**agent_data)

    return agents


def load_coder_pool_config(config_path: str | Path) -> CoderPoolConfig:
    """Load coder pool configuration from agents.yaml."""
    path = Path(config_path)
    if not path.exists():
        return CoderPoolConfig()

    with open(path) as f:
        data = yaml.safe_load(f) or {}

    pool_data = data.get("coder_pool", {})
    return CoderPoolConfig(**pool_data)


def load_runtime_config(
    agents_config_path: str | Path | None = None,
    llm_config_path: str | Path | None = None,
) -> AgentRuntimeConfig:
    """Load full runtime configuration from YAML files + environment variables."""
    config_data: dict[str, Any] = {}

    # Load LLM config
    if llm_config_path and Path(llm_config_path).exists():
        with open(llm_config_path) as f:
            llm_data = yaml.safe_load(f) or {}
        runtime = llm_data.get("agent_runtime", {})
        config_data["grpc_port"] = runtime.get("grpc_port", 50051)
        config_data["log_level"] = runtime.get("log_level", "INFO")
        config_data["llm_providers"] = [
            LlmProviderConfig(**p) for p in runtime.get("llm_providers", [])
        ]

    # Load agents config
    if agents_config_path:
        config_data["agents"] = load_agents_config(agents_config_path)
        config_data["coder_pool"] = load_coder_pool_config(agents_config_path)

    # Environment overrides
    config_data["grpc_port"] = int(os.getenv("GRPC_PORT", str(config_data.get("grpc_port", 50051))))
    config_data["database_url"] = os.getenv("DATABASE_URL", config_data.get("database_url", ""))

    return AgentRuntimeConfig(**config_data)
