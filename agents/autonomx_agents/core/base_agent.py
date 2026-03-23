"""Base agent class — all agents inherit from this."""

from __future__ import annotations

import logging
from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from typing import Any

logger = logging.getLogger(__name__)


@dataclass
class AgentContext:
    """Context passed to an agent execution."""

    execution_id: str
    project_id: str
    task_id: str | None = None
    context_data: dict[str, Any] = field(default_factory=dict)
    metadata: dict[str, str] = field(default_factory=dict)


@dataclass
class AgentResult:
    """Result returned by an agent execution."""

    success: bool
    result: dict[str, Any] = field(default_factory=dict)
    error: str | None = None
    metrics: dict[str, Any] = field(default_factory=dict)


class BaseAgent(ABC):
    """Abstract base class for all AutoNomX agents."""

    def __init__(self, agent_id: str, model: str, provider: str = "ollama") -> None:
        self.agent_id = agent_id
        self.model = model
        self.provider = provider
        self.logger = logging.getLogger(f"agent.{agent_id}")

    @abstractmethod
    async def execute(self, context: AgentContext) -> AgentResult:
        """Execute the agent's task."""
        ...

    def __repr__(self) -> str:
        return f"{self.__class__.__name__}(id={self.agent_id}, model={self.model})"
