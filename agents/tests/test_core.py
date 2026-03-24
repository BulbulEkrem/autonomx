"""Tests for the core agent framework."""

import pytest

from autonomx_agents.core import (
    AgentConfig,
    AgentContext,
    AgentMessage,
    AgentResult,
    BaseAgent,
    EventType,
    TaskInfo,
    agent_register,
    create_agent,
    discover_agents,
    get_agent_class,
    list_agents,
    load_agents_config,
)


class TestAgentContext:
    def test_creation_minimal(self):
        ctx = AgentContext(execution_id="test-1", project_id="proj-1")
        assert ctx.execution_id == "test-1"
        assert ctx.task is None
        assert ctx.files == {}
        assert ctx.history == []
        assert ctx.feedback == ""

    def test_creation_with_task(self):
        task = TaskInfo(task_id="t-1", title="Implement login")
        ctx = AgentContext(
            execution_id="test-2",
            project_id="proj-1",
            task=task,
            feedback="Fix the validation",
        )
        assert ctx.task is not None
        assert ctx.task.title == "Implement login"
        assert ctx.feedback == "Fix the validation"

    def test_creation_with_files(self):
        ctx = AgentContext(
            execution_id="test-3",
            project_id="proj-1",
            files={"src/main.py": "print('hello')"},
        )
        assert "src/main.py" in ctx.files


class TestAgentResult:
    def test_success(self):
        result = AgentResult(success=True, result={"output": "hello"})
        assert result.success is True
        assert result.error is None
        assert result.tokens_used == 0

    def test_failure(self):
        result = AgentResult(success=False, error="LLM timeout")
        assert result.success is False
        assert result.error == "LLM timeout"


class TestAgentConfig:
    def test_defaults(self):
        config = AgentConfig(name="test", type="planner", model="ollama/test")
        assert config.provider == "ollama"
        assert config.parameters.temperature == 0.7
        assert config.parameters.max_tokens == 4096

    def test_custom_params(self):
        config = AgentConfig(
            name="test",
            type="planner",
            model="ollama/test",
            parameters={"temperature": 0.3, "max_tokens": 8192},
        )
        assert config.parameters.temperature == 0.3


class TestRegistry:
    def test_register_and_list(self):
        @agent_register("test_registry_agent")
        class TestRegistryAgent(BaseAgent):
            async def execute(self, context: AgentContext) -> AgentResult:
                return AgentResult(success=True)

        assert "test_registry_agent" in list_agents()
        assert get_agent_class("test_registry_agent") is TestRegistryAgent

    def test_create_agent(self):
        @agent_register("test_create_agent")
        class TestCreateAgent(BaseAgent):
            async def execute(self, context: AgentContext) -> AgentResult:
                return AgentResult(success=True)

        config = AgentConfig(name="my-agent", type="test_create_agent", model="ollama/test")
        agent = create_agent(config)
        assert isinstance(agent, TestCreateAgent)
        assert agent.agent_id == "my-agent"
        assert agent.model == "ollama/test"

    def test_create_agent_unknown_type(self):
        config = AgentConfig(name="nope", type="nonexistent", model="ollama/test")
        with pytest.raises(ValueError, match="Unknown agent type"):
            create_agent(config)

    def test_discover_agents(self):
        count = discover_agents()
        assert isinstance(count, int)


class TestBaseAgentRun:
    @pytest.mark.asyncio
    async def test_run_success(self):
        @agent_register("test_run_agent")
        class TestRunAgent(BaseAgent):
            async def execute(self, context: AgentContext) -> AgentResult:
                return AgentResult(success=True, result={"data": "ok"}, tokens_used=100)

        config = AgentConfig(name="runner", type="test_run_agent", model="ollama/test")
        agent = create_agent(config)
        ctx = AgentContext(execution_id="exec-1", project_id="proj-1")
        result = await agent.run(ctx)

        assert result.success is True
        assert result.duration_seconds > 0
        assert result.tokens_used == 100

    @pytest.mark.asyncio
    async def test_run_handles_exception(self):
        @agent_register("test_error_agent")
        class TestErrorAgent(BaseAgent):
            async def execute(self, context: AgentContext) -> AgentResult:
                raise RuntimeError("boom")

        config = AgentConfig(name="error-agent", type="test_error_agent", model="ollama/test")
        agent = create_agent(config)
        ctx = AgentContext(execution_id="exec-2", project_id="proj-1")
        result = await agent.run(ctx)

        assert result.success is False
        assert "boom" in (result.error or "")
        assert result.duration_seconds > 0


class TestAgentMessage:
    def test_creation(self):
        msg = AgentMessage(
            from_agent="planner",
            to_agent="architect",
            event_type=EventType.TASK_COMPLETED,
            payload={"task_id": "t-1"},
        )
        assert msg.from_agent == "planner"
        assert msg.timestamp is not None

    def test_to_dict(self):
        msg = AgentMessage(
            from_agent="coder",
            to_agent="tester",
            event_type=EventType.TEST_REQUEST,
        )
        d = msg.to_dict()
        assert d["from_agent"] == "coder"
        assert "timestamp" in d


class TestLoadAgentsConfig:
    def test_load_from_yaml(self, tmp_path):
        config_file = tmp_path / "agents.yaml"
        config_file.write_text(
            """
agents:
  planner:
    type: planner
    model: "ollama/deepseek-r1:14b"
    provider: ollama
    description: "Plans tasks"
    parameters:
      temperature: 0.5
      max_tokens: 4096
"""
        )
        agents = load_agents_config(config_file)
        assert "planner" in agents
        assert agents["planner"].model == "ollama/deepseek-r1:14b"
        assert agents["planner"].parameters.temperature == 0.5

    def test_load_nonexistent(self):
        agents = load_agents_config("/nonexistent/path.yaml")
        assert agents == {}
