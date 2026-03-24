"""Tests for the gRPC AgentServicer."""

import json

import pytest

from autonomx_agents.core.base_agent import AgentContext, AgentResult, TaskInfo
from autonomx_agents.core.config import AgentConfig
from autonomx_agents.core.registry import agent_register, list_agents
from autonomx_agents.grpc_services.agent_servicer import (
    AGENT_TYPE_MAP,
    AGENT_TYPE_REVERSE,
    STREAM_EVENT_COMPLETED,
    STREAM_EVENT_LOG,
    AgentServicer,
    _ExecutionTracker,
    _build_agent_context,
    _build_execute_response,
    _build_error_response,
    _build_stream_event,
)


class TestAgentTypeMap:
    def test_all_agent_types_mapped(self):
        expected = {
            "product_owner", "planner", "architect",
            "model_manager", "coder", "tester", "reviewer",
        }
        mapped = {v for k, v in AGENT_TYPE_MAP.items() if k > 0}
        assert mapped == expected

    def test_reverse_map(self):
        assert AGENT_TYPE_REVERSE["coder"] == 5
        assert AGENT_TYPE_REVERSE["reviewer"] == 7


class TestExecutionTracker:
    def test_lifecycle(self):
        tracker = _ExecutionTracker()
        tracker.start("ex1", 5)

        entry = tracker.get("ex1")
        assert entry is not None
        assert entry["status"] == "running"
        assert entry["progress"] == 0.0

        tracker.update_progress("ex1", 0.5)
        assert tracker.get("ex1")["progress"] == 0.5

        tracker.complete("ex1", True)
        assert tracker.get("ex1")["status"] == "completed"
        assert tracker.get("ex1")["progress"] == 1.0

        tracker.cleanup("ex1")
        assert tracker.get("ex1") is None

    def test_cancel(self):
        tracker = _ExecutionTracker()
        tracker.start("ex2", 3)
        assert tracker.cancel("ex2") is True

        entry = tracker.get("ex2")
        assert entry["status"] == "cancelled"

    def test_cancel_nonexistent(self):
        tracker = _ExecutionTracker()
        assert tracker.cancel("nope") is False

    def test_progress_clamps(self):
        tracker = _ExecutionTracker()
        tracker.start("ex3", 1)
        tracker.update_progress("ex3", 1.5)
        assert tracker.get("ex3")["progress"] == 1.0


class _MockConfig:
    def __init__(self):
        self.model = "ollama/test-model"
        self.provider = "ollama"
        self.parameters = {"max_iterations": "5"}


class _MockTask:
    def __init__(self):
        self.task_id = "task-1"
        self.title = "Test task"
        self.description = "Test description"
        self.status = 1
        self.priority = 1
        self.dependencies = []
        self.files_touched = []
        self.assigned_worker = ""


class _MockRequest:
    def __init__(self):
        self.execution_id = "exec-123"
        self.project_id = "proj-456"
        self.agent_type = 5  # CODER
        self.config = _MockConfig()
        self.task = _MockTask()
        self.context = '{"key": "value"}'
        self.metadata = {"env": "test"}


class TestBuildHelpers:
    def test_build_agent_context(self):
        request = _MockRequest()
        ctx = _build_agent_context(request)
        assert ctx.execution_id == "exec-123"
        assert ctx.project_id == "proj-456"
        assert ctx.task is not None
        assert ctx.task.task_id == "task-1"
        assert ctx.metadata["previous_context"] == {"key": "value"}

    def test_build_agent_context_invalid_json(self):
        request = _MockRequest()
        request.context = "not-json"
        ctx = _build_agent_context(request)
        assert ctx.metadata["raw_context"] == "not-json"

    def test_build_execute_response(self):
        result = AgentResult(
            success=True,
            result={"output": "hello"},
            tokens_used=100,
            duration_seconds=1.5,
            iterations=2,
        )
        resp = _build_execute_response("exec-1", result)
        assert resp.execution_id == "exec-1"
        assert resp.success is True
        assert json.loads(resp.result) == {"output": "hello"}
        assert resp.metrics.total_tokens == 100

    def test_build_error_response(self):
        resp = _build_error_response("exec-2", "something broke")
        assert resp.success is False
        assert resp.error == "something broke"

    def test_build_stream_event(self):
        event = _build_stream_event("exec-3", STREAM_EVENT_LOG, {"msg": "hi"})
        assert event.execution_id == "exec-3"
        assert event.event_type == STREAM_EVENT_LOG
        data = json.loads(event.data)
        assert data["msg"] == "hi"
        assert event.timestamp  # ISO format


class TestAgentServicer:
    def test_instantiation(self):
        servicer = AgentServicer()
        assert servicer is not None

    def test_has_all_rpc_methods(self):
        servicer = AgentServicer()
        assert hasattr(servicer, "ExecuteAgent")
        assert hasattr(servicer, "ExecuteAgentStream")
        assert hasattr(servicer, "GetAgentStatus")
        assert hasattr(servicer, "CancelAgent")
