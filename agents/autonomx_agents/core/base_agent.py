"""Base agent class — all agents inherit from this."""

from __future__ import annotations

import logging
import time
from abc import ABC, abstractmethod
from typing import Any

from pydantic import BaseModel, Field

from autonomx_agents.core.config import AgentConfig

logger = logging.getLogger(__name__)


class TaskInfo(BaseModel):
    """Lightweight task info passed to agent context."""

    task_id: str
    title: str
    description: str = ""
    priority: str = "Should"
    dependencies: list[str] = Field(default_factory=list)
    git_branch: str = ""


class AgentContext(BaseModel):
    """Context passed to an agent execution."""

    execution_id: str
    project_id: str
    task: TaskInfo | None = None
    files: dict[str, str] = Field(default_factory=dict)  # path -> content
    history: list[dict[str, Any]] = Field(default_factory=list)  # previous messages
    feedback: str = ""  # reviewer/user feedback for retry
    metadata: dict[str, Any] = Field(default_factory=dict)


class AgentResult(BaseModel):
    """Result returned by an agent execution."""

    success: bool
    result: dict[str, Any] = Field(default_factory=dict)
    error: str | None = None
    tokens_used: int = 0
    duration_seconds: float = 0.0
    iterations: int = 1


class BaseAgent(ABC):
    """Abstract base class for all AutoNomX agents."""

    def __init__(self, config: AgentConfig) -> None:
        self.config = config
        self.agent_id = config.name
        self.model = config.model
        self.provider = config.provider
        self.logger = logging.getLogger(f"agent.{self.agent_id}")

    @abstractmethod
    async def execute(self, context: AgentContext) -> AgentResult:
        """Execute the agent's task. Subclasses must implement this."""
        ...

    async def run(self, context: AgentContext) -> AgentResult:
        """Run the agent with lifecycle hooks and metrics tracking."""
        self.logger.info(
            "Starting execution: %s (model=%s, execution_id=%s)",
            self.agent_id,
            self.model,
            context.execution_id,
        )

        start = time.monotonic()
        try:
            await self.on_before_execute(context)
            result = await self.execute(context)
            result.duration_seconds = time.monotonic() - start
            await self.on_after_execute(context, result)

            self.logger.info(
                "Execution completed: %s (success=%s, duration=%.2fs, tokens=%d)",
                self.agent_id,
                result.success,
                result.duration_seconds,
                result.tokens_used,
            )
            return result

        except Exception as e:
            duration = time.monotonic() - start
            self.logger.error("Execution failed: %s — %s", self.agent_id, e)
            return AgentResult(
                success=False,
                error=str(e),
                duration_seconds=duration,
            )

    async def on_before_execute(self, context: AgentContext) -> None:
        """Hook called before execute(). Override for setup logic."""

    async def on_after_execute(self, context: AgentContext, result: AgentResult) -> None:
        """Hook called after execute(). Override for cleanup/metrics."""

    def __repr__(self) -> str:
        return f"{self.__class__.__name__}(id={self.agent_id}, model={self.model})"
