"""Basic tests for the core agent framework."""

from autonomx_agents.core import BaseAgent, AgentContext, AgentResult, agent_register, list_agents


class TestBaseAgent:
    def test_agent_context_creation(self):
        ctx = AgentContext(execution_id="test-1", project_id="proj-1")
        assert ctx.execution_id == "test-1"
        assert ctx.task_id is None

    def test_agent_result_success(self):
        result = AgentResult(success=True, result={"output": "hello"})
        assert result.success is True
        assert result.error is None


class TestRegistry:
    def test_register_and_list(self):
        @agent_register("test_agent")
        class TestAgent(BaseAgent):
            async def execute(self, context: AgentContext) -> AgentResult:
                return AgentResult(success=True)

        assert "test_agent" in list_agents()
