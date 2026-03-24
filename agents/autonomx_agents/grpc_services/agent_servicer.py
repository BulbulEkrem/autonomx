"""gRPC AgentService implementation.

This servicer handles incoming gRPC calls from the .NET orchestrator
and dispatches them to the appropriate Python agent via the registry.
Uses real protobuf types generated from agent_service.proto.
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

# Import generated proto types
from autonomx_agents.grpc_services.generated import (  # noqa: E402
    agent_service_pb2,
    agent_service_pb2_grpc,
    common_pb2,
)

logger = logging.getLogger(__name__)

# Agent type enum mapping (from common.proto AgentType)
AGENT_TYPE_MAP: dict[int, str] = {
    common_pb2.AGENT_TYPE_UNSPECIFIED: "unspecified",
    common_pb2.AGENT_TYPE_PRODUCT_OWNER: "product_owner",
    common_pb2.AGENT_TYPE_PLANNER: "planner",
    common_pb2.AGENT_TYPE_ARCHITECT: "architect",
    common_pb2.AGENT_TYPE_MODEL_MANAGER: "model_manager",
    common_pb2.AGENT_TYPE_CODER: "coder",
    common_pb2.AGENT_TYPE_TESTER: "tester",
    common_pb2.AGENT_TYPE_REVIEWER: "reviewer",
}

# Reverse mapping
AGENT_TYPE_REVERSE: dict[str, int] = {v: k for k, v in AGENT_TYPE_MAP.items()}


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


class AgentServicer(agent_service_pb2_grpc.AgentServiceServicer):
    """gRPC AgentService implementation.

    Extends the generated base class from agent_service.proto.
    Receives execution requests from the .NET orchestrator and
    routes them to registered Python agents.
    """

    def __init__(self, event_publisher: Any | None = None) -> None:
        """Initialize servicer with optional event publisher for NOTIFY.

        Args:
            event_publisher: Optional callable(channel, payload) for Postgres NOTIFY.
        """
        super().__init__()
        self._event_publisher = event_publisher

    async def ExecuteAgent(
        self,
        request: agent_service_pb2.ExecuteAgentRequest,
        context: grpc.aio.ServicerContext,
    ) -> agent_service_pb2.ExecuteAgentResponse:
        """Execute an agent (unary RPC)."""
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
            agent_config = _build_agent_config(request, agent_type_name)
            agent = create_agent(agent_config)
            agent_context = _build_agent_context(request)

            task = asyncio.current_task()
            if task:
                _tracker.set_task(execution_id, task)

            result = await agent.run(agent_context)
            _tracker.complete(execution_id, result.success)

            # Publish event via Postgres NOTIFY if publisher available
            await self._publish_event(
                "agent_events",
                {
                    "type": "agent.completed",
                    "execution_id": execution_id,
                    "agent_type": agent_type_name,
                    "success": result.success,
                    "project_id": request.project_id,
                },
            )

            return _build_execute_response(execution_id, result, agent_config.model)

        except Exception as e:
            logger.exception("ExecuteAgent failed: %s", execution_id)
            _tracker.complete(execution_id, False)

            await self._publish_event(
                "agent_events",
                {
                    "type": "agent.failed",
                    "execution_id": execution_id,
                    "agent_type": agent_type_name,
                    "error": str(e),
                    "project_id": request.project_id,
                },
            )

            return _build_error_response(execution_id, str(e))

        finally:
            _tracker.cleanup(execution_id)

    async def ExecuteAgentStream(
        self,
        request: agent_service_pb2.ExecuteAgentRequest,
        context: grpc.aio.ServicerContext,
    ):
        """Execute an agent with streaming output (server streaming RPC)."""
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
                agent_service_pb2.STREAM_EVENT_TYPE_LOG,
                {"message": f"Starting agent: {agent_type_name}"},
            )

            # Build and run agent
            agent_config = _build_agent_config(request, agent_type_name)
            agent = create_agent(agent_config)
            agent_context = _build_agent_context(request)

            # Progress event
            yield _build_stream_event(
                execution_id,
                agent_service_pb2.STREAM_EVENT_TYPE_PROGRESS,
                {"progress": 0.1, "message": "Agent initialized"},
            )
            _tracker.update_progress(execution_id, 0.1)

            result = await agent.run(agent_context)
            _tracker.complete(execution_id, result.success)

            if result.success:
                yield _build_stream_event(
                    execution_id,
                    agent_service_pb2.STREAM_EVENT_TYPE_OUTPUT,
                    {"result": result.result},
                )
            else:
                yield _build_stream_event(
                    execution_id,
                    agent_service_pb2.STREAM_EVENT_TYPE_ERROR,
                    {"error": result.error or "Unknown error"},
                )

            # Completed event
            yield _build_stream_event(
                execution_id,
                agent_service_pb2.STREAM_EVENT_TYPE_COMPLETED,
                {
                    "success": result.success,
                    "tokens_used": result.tokens_used,
                    "duration_seconds": result.duration_seconds,
                },
            )

            await self._publish_event(
                "agent_events",
                {
                    "type": "agent.completed",
                    "execution_id": execution_id,
                    "agent_type": agent_type_name,
                    "success": result.success,
                    "project_id": request.project_id,
                },
            )

        except asyncio.CancelledError:
            logger.info("ExecuteAgentStream cancelled: %s", execution_id)
            _tracker.complete(execution_id, False)
            yield _build_stream_event(
                execution_id,
                agent_service_pb2.STREAM_EVENT_TYPE_ERROR,
                {"error": "Execution cancelled"},
            )
        except Exception as e:
            logger.exception("ExecuteAgentStream failed: %s", execution_id)
            _tracker.complete(execution_id, False)
            yield _build_stream_event(
                execution_id,
                agent_service_pb2.STREAM_EVENT_TYPE_ERROR,
                {"error": str(e)},
            )
        finally:
            _tracker.cleanup(execution_id)

    async def GetAgentStatus(
        self,
        request: agent_service_pb2.GetAgentStatusRequest,
        context: grpc.aio.ServicerContext,
    ) -> agent_service_pb2.GetAgentStatusResponse:
        """Get status of a running agent execution."""
        execution_id = request.execution_id
        entry = _tracker.get(execution_id)

        if entry is None:
            await context.abort(
                grpc.StatusCode.NOT_FOUND,
                f"Execution not found: {execution_id}",
            )

        return agent_service_pb2.GetAgentStatusResponse(
            execution_id=execution_id,
            agent_type=entry["agent_type"],
            status=entry["status"],
            progress=entry["progress"],
        )

    async def CancelAgent(
        self,
        request: agent_service_pb2.CancelAgentRequest,
        context: grpc.aio.ServicerContext,
    ) -> agent_service_pb2.CancelAgentResponse:
        """Cancel a running agent execution."""
        execution_id = request.execution_id
        reason = request.reason

        logger.info("CancelAgent: %s, reason=%s", execution_id, reason)

        success = _tracker.cancel(execution_id)
        message = "Cancelled" if success else f"Could not cancel: {execution_id}"

        return agent_service_pb2.CancelAgentResponse(success=success, message=message)

    async def _publish_event(self, channel: str, payload: dict[str, Any]) -> None:
        """Publish event via Postgres NOTIFY if publisher is available."""
        if self._event_publisher is None:
            return
        try:
            await self._event_publisher(channel, json.dumps(payload))
        except Exception as e:
            logger.warning("Failed to publish event: %s", e)


# ── Helper functions ────────────────────────────────────────────


def _build_agent_config(request: Any, agent_type_name: str) -> AgentConfig:
    """Build AgentConfig from gRPC request."""
    config = request.config if request.HasField("config") else None

    return AgentConfig(
        name=f"{agent_type_name}_{request.execution_id[:8]}",
        type=agent_type_name,
        model=config.model if config and config.model else "ollama/qwen2.5-coder:14b",
        provider=config.provider if config and config.provider else "ollama",
        max_iterations=int(
            config.parameters.get("max_iterations", "10")
            if config and config.parameters
            else "10"
        ),
    )


def _build_agent_context(request: Any) -> AgentContext:
    """Build AgentContext from gRPC request."""
    task_info = None
    if request.HasField("task"):
        task = request.task
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


def _build_execute_response(
    execution_id: str, result: AgentResult, model: str = "",
) -> agent_service_pb2.ExecuteAgentResponse:
    """Build ExecuteAgentResponse from AgentResult."""
    return agent_service_pb2.ExecuteAgentResponse(
        execution_id=execution_id,
        success=result.success,
        result=json.dumps(result.result) if result.result else "",
        error=result.error or "",
        metrics=agent_service_pb2.AgentMetrics(
            total_tokens=result.tokens_used,
            prompt_tokens=0,
            completion_tokens=0,
            duration_seconds=result.duration_seconds,
            iterations=result.iterations,
            model_used=model,
        ),
    )


def _build_error_response(
    execution_id: str, error: str,
) -> agent_service_pb2.ExecuteAgentResponse:
    """Build error ExecuteAgentResponse."""
    return agent_service_pb2.ExecuteAgentResponse(
        execution_id=execution_id,
        success=False,
        result="",
        error=error,
        metrics=agent_service_pb2.AgentMetrics(
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
) -> agent_service_pb2.AgentStreamEvent:
    """Build AgentStreamEvent."""
    return agent_service_pb2.AgentStreamEvent(
        execution_id=execution_id,
        event_type=event_type,
        data=json.dumps(data),
        timestamp=datetime.now(timezone.utc).isoformat(),
    )
