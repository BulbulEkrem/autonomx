"""Message types for inter-agent communication."""

from __future__ import annotations

from datetime import datetime, timezone
from enum import StrEnum
from typing import Any

from pydantic import BaseModel, Field


class EventType(StrEnum):
    """Standard event types for agent communication."""

    TASK_ASSIGNED = "task_assigned"
    TASK_COMPLETED = "task_completed"
    TASK_FAILED = "task_failed"
    CODE_REVIEW_REQUEST = "code_review_request"
    CODE_REVIEW_RESULT = "code_review_result"
    TEST_REQUEST = "test_request"
    TEST_RESULT = "test_result"
    PIPELINE_STEP_COMPLETED = "pipeline_step_completed"
    AGENT_STATUS_CHANGE = "agent_status_change"
    FILE_LOCK_ACQUIRED = "file_lock_acquired"
    FILE_LOCK_RELEASED = "file_lock_released"


class AgentMessage(BaseModel):
    """A message between agents or from orchestrator to agent."""

    from_agent: str
    to_agent: str
    event_type: str
    payload: dict[str, Any] = Field(default_factory=dict)
    project_id: str | None = None
    task_id: str | None = None
    timestamp: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))

    def to_dict(self) -> dict[str, Any]:
        return self.model_dump(mode="json")
