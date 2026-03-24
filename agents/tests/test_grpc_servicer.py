"""Tests for the gRPC AgentServicer and event publisher."""

import json

import pytest

from autonomx_agents.core.base_agent import AgentContext, AgentResult, TaskInfo
from autonomx_agents.core.config import AgentConfig
from autonomx_agents.grpc_services.agent_servicer import (
    AGENT_TYPE_MAP,
    AGENT_TYPE_REVERSE,
    AgentServicer,
    _ExecutionTracker,
    _build_agent_context,
    _build_execute_response,
    _build_error_response,
    _build_stream_event,
)
from autonomx_agents.grpc_services.generated import (
    agent_service_pb2,
    common_pb2,
)
from autonomx_agents.grpc_services.event_publisher import (
    PostgresEventPublisher,
    _sanitize_channel,
    _escape,
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
        assert AGENT_TYPE_REVERSE["coder"] == common_pb2.AGENT_TYPE_CODER
        assert AGENT_TYPE_REVERSE["reviewer"] == common_pb2.AGENT_TYPE_REVIEWER

    def test_maps_use_proto_enums(self):
        assert AGENT_TYPE_MAP[common_pb2.AGENT_TYPE_PRODUCT_OWNER] == "product_owner"
        assert AGENT_TYPE_MAP[common_pb2.AGENT_TYPE_PLANNER] == "planner"


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


class TestBuildHelpersWithProto:
    """Test helper functions using real proto messages."""

    def _make_request(self, **overrides):
        """Create a real ExecuteAgentRequest proto message."""
        request = agent_service_pb2.ExecuteAgentRequest(
            execution_id=overrides.get("execution_id", "exec-123"),
            project_id=overrides.get("project_id", "proj-456"),
            agent_type=overrides.get("agent_type", common_pb2.AGENT_TYPE_CODER),
            context=overrides.get("context", '{"key": "value"}'),
        )
        # Add config
        if overrides.get("with_config", True):
            request.config.CopyFrom(common_pb2.AgentConfig(
                agent_id="agent-1",
                agent_type=common_pb2.AGENT_TYPE_CODER,
                model="ollama/test-model",
                provider="ollama",
            ))
            request.config.parameters["max_iterations"] = "5"

        # Add task
        if overrides.get("with_task", True):
            request.task.CopyFrom(common_pb2.TaskInfo(
                task_id="task-1",
                title="Test task",
                description="Test description",
            ))

        # Add metadata
        if "metadata" in overrides:
            for k, v in overrides["metadata"].items():
                request.metadata[k] = v
        else:
            request.metadata["env"] = "test"

        return request

    def test_build_agent_context(self):
        request = self._make_request()
        ctx = _build_agent_context(request)
        assert ctx.execution_id == "exec-123"
        assert ctx.project_id == "proj-456"
        assert ctx.task is not None
        assert ctx.task.task_id == "task-1"
        assert ctx.metadata["previous_context"] == {"key": "value"}
        assert ctx.metadata["env"] == "test"

    def test_build_agent_context_invalid_json(self):
        request = self._make_request(context="not-json")
        ctx = _build_agent_context(request)
        assert ctx.metadata["raw_context"] == "not-json"

    def test_build_agent_context_no_task(self):
        request = self._make_request(with_task=False)
        ctx = _build_agent_context(request)
        assert ctx.task is None

    def test_build_execute_response(self):
        result = AgentResult(
            success=True,
            result={"output": "hello"},
            tokens_used=100,
            duration_seconds=1.5,
            iterations=2,
        )
        resp = _build_execute_response("exec-1", result, "ollama/test")
        assert isinstance(resp, agent_service_pb2.ExecuteAgentResponse)
        assert resp.execution_id == "exec-1"
        assert resp.success is True
        assert json.loads(resp.result) == {"output": "hello"}
        assert resp.metrics.total_tokens == 100
        assert resp.metrics.duration_seconds == 1.5
        assert resp.metrics.model_used == "ollama/test"

    def test_build_error_response(self):
        resp = _build_error_response("exec-2", "something broke")
        assert isinstance(resp, agent_service_pb2.ExecuteAgentResponse)
        assert resp.success is False
        assert resp.error == "something broke"
        assert resp.metrics.total_tokens == 0

    def test_build_stream_event(self):
        event = _build_stream_event(
            "exec-3",
            agent_service_pb2.STREAM_EVENT_TYPE_LOG,
            {"msg": "hi"},
        )
        assert isinstance(event, agent_service_pb2.AgentStreamEvent)
        assert event.execution_id == "exec-3"
        assert event.event_type == agent_service_pb2.STREAM_EVENT_TYPE_LOG
        data = json.loads(event.data)
        assert data["msg"] == "hi"
        assert event.timestamp  # ISO format

    def test_build_stream_event_completed(self):
        event = _build_stream_event(
            "exec-4",
            agent_service_pb2.STREAM_EVENT_TYPE_COMPLETED,
            {"success": True, "tokens_used": 50},
        )
        assert event.event_type == agent_service_pb2.STREAM_EVENT_TYPE_COMPLETED


class TestAgentServicer:
    def test_instantiation(self):
        servicer = AgentServicer()
        assert servicer is not None

    def test_instantiation_with_publisher(self):
        async def mock_publisher(channel, payload):
            pass
        servicer = AgentServicer(event_publisher=mock_publisher)
        assert servicer._event_publisher is not None

    def test_has_all_rpc_methods(self):
        servicer = AgentServicer()
        assert hasattr(servicer, "ExecuteAgent")
        assert hasattr(servicer, "ExecuteAgentStream")
        assert hasattr(servicer, "GetAgentStatus")
        assert hasattr(servicer, "CancelAgent")

    def test_inherits_from_generated_servicer(self):
        from autonomx_agents.grpc_services.generated import agent_service_pb2_grpc
        assert issubclass(AgentServicer, agent_service_pb2_grpc.AgentServiceServicer)


class TestProtoMessages:
    """Test that proto message creation works correctly."""

    def test_execute_request(self):
        req = agent_service_pb2.ExecuteAgentRequest(
            execution_id="test-1",
            project_id="proj-1",
            agent_type=common_pb2.AGENT_TYPE_PLANNER,
        )
        assert req.execution_id == "test-1"
        assert req.agent_type == common_pb2.AGENT_TYPE_PLANNER

    def test_execute_response(self):
        resp = agent_service_pb2.ExecuteAgentResponse(
            execution_id="test-1",
            success=True,
            result='{"key": "val"}',
        )
        assert resp.success is True

    def test_agent_metrics(self):
        metrics = agent_service_pb2.AgentMetrics(
            total_tokens=500,
            prompt_tokens=200,
            completion_tokens=300,
            duration_seconds=2.5,
            iterations=3,
            model_used="ollama/test",
        )
        assert metrics.total_tokens == 500
        assert metrics.model_used == "ollama/test"

    def test_stream_event(self):
        evt = agent_service_pb2.AgentStreamEvent(
            execution_id="test-1",
            event_type=agent_service_pb2.STREAM_EVENT_TYPE_PROGRESS,
            data='{"progress": 0.5}',
            timestamp="2024-01-01T00:00:00Z",
        )
        assert evt.event_type == agent_service_pb2.STREAM_EVENT_TYPE_PROGRESS

    def test_cancel_response(self):
        resp = agent_service_pb2.CancelAgentResponse(success=True, message="OK")
        assert resp.success is True

    def test_status_response(self):
        resp = agent_service_pb2.GetAgentStatusResponse(
            execution_id="test-1",
            agent_type=common_pb2.AGENT_TYPE_CODER,
            status="running",
            progress=0.5,
        )
        assert resp.progress == 0.5
        assert resp.agent_type == common_pb2.AGENT_TYPE_CODER


class TestEventPublisher:
    def test_sanitize_channel(self):
        assert _sanitize_channel("agent_events") == "agent_events"
        assert _sanitize_channel("agent-events!") == "agentevents"
        assert _sanitize_channel("Agent_Events_123") == "agent_events_123"

    def test_escape(self):
        assert _escape("hello") == "hello"
        assert _escape("it's a test") == "it''s a test"

    def test_publisher_no_db_url(self):
        pub = PostgresEventPublisher(database_url="")
        assert pub.is_available is False

    def test_publisher_with_db_url_no_psycopg(self):
        """Publisher availability depends on psycopg being installed."""
        pub = PostgresEventPublisher(database_url="postgresql://localhost/test")
        # is_available depends on whether psycopg is installed
        assert isinstance(pub.is_available, bool)
