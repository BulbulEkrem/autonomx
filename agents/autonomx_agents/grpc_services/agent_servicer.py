"""gRPC AgentService implementation.

This servicer handles incoming gRPC calls from the .NET orchestrator
and dispatches them to the appropriate Python agent via the registry.
"""

from __future__ import annotations

import asyncio
import json
import logging
import time
from datetime import datetime, timezone
from typing import Any

import grpc

from autonomx_agents.core.base_agent import AgentContext, AgentResult, TaskInfo
from autonomx_agents.core.config import AgentConfig
from autonomx_agents.core.registry import create_agent, list_agents

logger = logging.getLogger(__name__)

# Agent type enum mapping (from common.proto AgentType)
AGENT_TYPE_MAP: dict[int, str] = {
    0: "unspecified",
    1: "product_owner",
    2: "planner",
    3: "architect",
    4: "model_manager",
    5: "coder",
    6: "tester",
    7: "reviewer",
}

# Reverse mapping
AGENT_TYPE_REVERSE: dict[str, int] = {v: k for k, v in AGENT_TYPE_MAP.items()}

# Stream event types (from agent_service.proto StreamEventType)
STREAM_EVENT_LOG = 1
STREAM_EVENT_PROGRESS = 2
STREAM_EVENT_OUTPUT = 3
STREAM_EVENT_ERROR = 4
STREAM_EVENT_COMPLETED = 5


class _ExecutionTracker:
    """Tracks running agent executions for status queries and cancellation."""

    def __init__(self) -> None:
        self._executions: dict[str, dict[str, Any]] = {}

    def start(self, execution_id: str, agent_type: int) -> None:
        self._executions[execution_id] = {
            "agent_type": agent_type,
            "status": "running",
            "progress": 0.0,
            "task": None,
            "started_at": time.monotonic(),
        }

    def update_progress(self, execution_id: str, progress: float) -> None:
        if execution_id in self._executions:
            self._executions[execution_id]["progress"] = min(progress, 1.0)

    def complete(self, execution_id: str, success: bool) -> None:
        if execution_id in self._executions:
            self._executions[execution_id]["status"] = "completed" if success else "failed"
            self._executions[execution_id]["progress"] = 1.0

    def cancel(self, execution_id: str) -> bool:
        if execution_id in self._executions:
            entry = self._executions[execution_id]
            if entry["status"] == "running":
                entry["status"] = "cancelled"
                task = entry.get("task")
                if task and not task.done():
                    task.cancel()
                return True
        return False

    def set_task(self, execution_id: str, task: asyncio.Task) -> None:
        if execution_id in self._executions:
            self._executions[execution_id]["task"] = task

    def get(self, execution_id: str) -> dict[str, Any] | None:
        return self._executions.get(execution_id)

    def cleanup(self, execution_id: str) -> None:
        self._executions.pop(execution_id, None)


# Global tracker
_tracker = _ExecutionTracker()


class AgentServicer:
    """gRPC AgentService implementation.

    This class implements the AgentService RPC interface defined in
    agent_service.proto. It receives execution requests from the .NET
    orchestrator and routes them to registered Python agents.

    Note: Method signatures use generic types until proto stubs are generated.
    After running `make proto`, these should match the generated servicer base class.
    """

    async def ExecuteAgent(
        self,
        request: Any,
        context: grpc.aio.ServicerContext,
    ) -> Any:
        """Execute an agent (unary RPC).

        Maps to: rpc ExecuteAgent(ExecuteAgentRequest) returns (ExecuteAgentResponse)
        """
        execution_id = request.execution_id
        agent_type_int = request.agent_type
        agent_type_name = AGENT_TYPE_MAP.get(agent_type_int, "unspecified")

        logger.info(
            "ExecuteAgent: id=%s, type=%s (%d), project=%s",
            execution_id,
            agent_type_name,
            agent_type_int,
            request.project_id,
        )

        _tracker.start(execution_id, agent_type_int)

        try:
            # Build agent config from request
            agent_config = _build_agent_config(request, agent_type_name)
            agent = create_agent(agent_config)

            # Build execution context
            agent_context = _build_agent_context(request)

            # Run the agent
            task = asyncio.current_task()
            if task:
                _tracker.set_task(execution_id, task)

            result = await agent.run(agent_context)
            _tracker.complete(execution_id, result.success)

            return _build_execute_response(execution_id, result)

        except Exception as e:
            logger.exception("ExecuteAgent failed: %s", execution_id)
            _tracker.complete(execution_id, False)
            return _build_error_response(execution_id, str(e))

        finally:
            _tracker.cleanup(execution_id)

    async def ExecuteAgentStream(
        self,
        request: Any,
        context: grpc.aio.ServicerContext,
    ):
        """Execute an agent with streaming output (server streaming RPC).

        Maps to: rpc ExecuteAgentStream(ExecuteAgentRequest) returns (stream AgentStreamEvent)
        """
        execution_id = request.execution_id
        agent_type_int = request.agent_type
        agent_type_name = AGENT_TYPE_MAP.get(agent_type_int, "unspecified")

        logger.info(
            "ExecuteAgentStream: id=%s, type=%s",
            execution_id,
            agent_type_name,
        )

        _tracker.start(execution_id, agent_type_int)

        try:
            # Send initial log event
            yield _build_stream_event(
                execution_id,
                STREAM_EVENT_LOG,
                {"message": f"Starting agent: {agent_type_name}"},
            )

            # Build and run agent
            agent_config = _build_agent_config(request, agent_type_name)
            agent = create_agent(agent_config)
            agent_context = _build_agent_context(request)

            # Progress event
            yield _build_stream_event(
                execution_id,
                STREAM_EVENT_PROGRESS,
                {"progress": 0.1, "message": "Agent initialized"},
            )

            result = await agent.run(agent_context)
            _tracker.complete(execution_id, result.success)

            if result.success:
                # Output event with result
                yield _build_stream_event(
                    execution_id,
                    STREAM_EVENT_OUTPUT,
                    {"result": result.result},
                )
            else:
                yield _build_stream_event(
                    execution_id,
                    STREAM_EVENT_ERROR,
                    {"error": result.error or "Unknown error"},
                )

            # Completed event
            yield _build_stream_event(
                execution_id,
                STREAM_EVENT_COMPLETED,
                {
                    "success": result.success,
                    "tokens_used": result.tokens_used,
                    "duration_seconds": result.duration_seconds,
                },
            )

        except asyncio.CancelledError:
            logger.info("ExecuteAgentStream cancelled: %s", execution_id)
            _tracker.complete(execution_id, False)
            yield _build_stream_event(
                execution_id,
                STREAM_EVENT_ERROR,
                {"error": "Execution cancelled"},
            )
        except Exception as e:
            logger.exception("ExecuteAgentStream failed: %s", execution_id)
            _tracker.complete(execution_id, False)
            yield _build_stream_event(
                execution_id,
                STREAM_EVENT_ERROR,
                {"error": str(e)},
            )
        finally:
            _tracker.cleanup(execution_id)

    async def GetAgentStatus(
        self,
        request: Any,
        context: grpc.aio.ServicerContext,
    ) -> Any:
        """Get status of a running agent execution.

        Maps to: rpc GetAgentStatus(GetAgentStatusRequest) returns (GetAgentStatusResponse)
        """
        execution_id = request.execution_id
        entry = _tracker.get(execution_id)

        if entry is None:
            await context.abort(
                grpc.StatusCode.NOT_FOUND,
                f"Execution not found: {execution_id}",
            )

        # Return a dict-like response (actual proto message after make proto)
        return _StatusResponse(
            execution_id=execution_id,
            agent_type=entry["agent_type"],
            status=entry["status"],
            progress=entry["progress"],
        )

    async def CancelAgent(
        self,
        request: Any,
        context: grpc.aio.ServicerContext,
    ) -> Any:
        """Cancel a running agent execution.

        Maps to: rpc CancelAgent(CancelAgentRequest) returns (CancelAgentResponse)
        """
        execution_id = request.execution_id
        reason = request.reason

        logger.info("CancelAgent: %s, reason=%s", execution_id, reason)

        success = _tracker.cancel(execution_id)
        message = "Cancelled" if success else f"Could not cancel: {execution_id}"

        return _CancelResponse(success=success, message=message)


# ── Helper classes for responses (until proto stubs are generated) ──


class _StatusResponse:
    """Placeholder response for GetAgentStatus."""

    def __init__(self, execution_id: str, agent_type: int, status: str, progress: float):
        self.execution_id = execution_id
        self.agent_type = agent_type
        self.status = status
        self.progress = progress


class _CancelResponse:
    """Placeholder response for CancelAgent."""

    def __init__(self, success: bool, message: str):
        self.success = success
        self.message = message


class _ExecuteResponse:
    """Placeholder response for ExecuteAgent."""

    def __init__(self, **kwargs: Any):
        for k, v in kwargs.items():
            setattr(self, k, v)


class _StreamEvent:
    """Placeholder for AgentStreamEvent."""

    def __init__(self, **kwargs: Any):
        for k, v in kwargs.items():
            setattr(self, k, v)


class _AgentMetrics:
    """Placeholder for AgentMetrics."""

    def __init__(self, **kwargs: Any):
        for k, v in kwargs.items():
            setattr(self, k, v)


# ── Helper functions ────────────────────────────────────────────


def _build_agent_config(request: Any, agent_type_name: str) -> AgentConfig:
    """Build AgentConfig from gRPC request."""
    config = request.config if hasattr(request, "config") and request.config else None

    return AgentConfig(
        name=f"{agent_type_name}_{request.execution_id[:8]}",
        type=agent_type_name,
        model=config.model if config else "ollama/qwen2.5-coder:14b",
        provider=config.provider if config else "ollama",
        max_iterations=int(
            config.parameters.get("max_iterations", "10")
            if config and config.parameters
            else "10"
        ),
    )


def _build_agent_context(request: Any) -> AgentContext:
    """Build AgentContext from gRPC request."""
    task = request.task if hasattr(request, "task") and request.task else None

    task_info = None
    if task:
        task_info = TaskInfo(
            task_id=task.task_id,
            title=task.title,
            description=task.description,
        )

    # Parse context JSON
    metadata: dict[str, Any] = dict(request.metadata) if request.metadata else {}
    if request.context:
        try:
            ctx_data = json.loads(request.context)
            metadata["previous_context"] = ctx_data
        except json.JSONDecodeError:
            metadata["raw_context"] = request.context

    return AgentContext(
        execution_id=request.execution_id,
        project_id=request.project_id,
        task=task_info,
        metadata=metadata,
    )


def _build_execute_response(execution_id: str, result: AgentResult) -> _ExecuteResponse:
    """Build ExecuteAgentResponse from AgentResult."""
    return _ExecuteResponse(
        execution_id=execution_id,
        success=result.success,
        result=json.dumps(result.result),
        error=result.error or "",
        metrics=_AgentMetrics(
            total_tokens=result.tokens_used,
            prompt_tokens=0,
            completion_tokens=0,
            duration_seconds=result.duration_seconds,
            iterations=result.iterations,
            model_used="",
        ),
    )


def _build_error_response(execution_id: str, error: str) -> _ExecuteResponse:
    """Build error ExecuteAgentResponse."""
    return _ExecuteResponse(
        execution_id=execution_id,
        success=False,
        result="",
        error=error,
        metrics=_AgentMetrics(
            total_tokens=0,
            prompt_tokens=0,
            completion_tokens=0,
            duration_seconds=0.0,
            iterations=0,
            model_used="",
        ),
    )


def _build_stream_event(
    execution_id: str,
    event_type: int,
    data: dict[str, Any],
) -> _StreamEvent:
    """Build AgentStreamEvent."""
    return _StreamEvent(
        execution_id=execution_id,
        event_type=event_type,
        data=json.dumps(data),
        timestamp=datetime.now(timezone.utc).isoformat(),
    )
