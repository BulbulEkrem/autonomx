"""Tests for built-in agent implementations."""

import pytest

from autonomx_agents.core.base_agent import AgentContext, AgentResult, TaskInfo
from autonomx_agents.core.config import AgentConfig
from autonomx_agents.core.registry import (
    _registry,
    create_agent,
    discover_agents,
    get_agent_class,
    list_agents,
)


@pytest.fixture(autouse=True)
def _discover():
    """Ensure agents are discovered before tests."""
    discover_agents()


class TestAgentDiscovery:
    def test_all_agents_discovered(self):
        agents = list_agents()
        expected = {
            "product_owner",
            "planner",
            "architect",
            "model_manager",
            "coder",
            "tester",
            "reviewer",
        }
        assert expected.issubset(set(agents)), f"Missing: {expected - set(agents)}"

    def test_get_agent_class(self):
        for name in ["product_owner", "planner", "architect", "coder", "tester", "reviewer", "model_manager"]:
            cls = get_agent_class(name)
            assert cls is not None, f"Agent class not found: {name}"

    def test_unknown_agent_class(self):
        assert get_agent_class("nonexistent_agent_xyz") is None


class TestAgentCreation:
    @pytest.fixture
    def make_config(self):
        def _make(agent_type: str, model: str = "ollama/test:latest") -> AgentConfig:
            return AgentConfig(
                name=f"test_{agent_type}",
                type=agent_type,
                model=model,
                provider="ollama",
            )
        return _make

    def test_create_product_owner(self, make_config):
        agent = create_agent(make_config("product_owner"))
        assert agent.agent_id == "test_product_owner"
        assert agent.model == "ollama/test:latest"

    def test_create_planner(self, make_config):
        agent = create_agent(make_config("planner"))
        assert agent.agent_id == "test_planner"

    def test_create_architect(self, make_config):
        agent = create_agent(make_config("architect"))
        assert agent.agent_id == "test_architect"

    def test_create_model_manager(self, make_config):
        agent = create_agent(make_config("model_manager"))
        assert agent.agent_id == "test_model_manager"

    def test_create_coder(self, make_config):
        agent = create_agent(make_config("coder"))
        assert agent.agent_id == "test_coder"

    def test_create_tester(self, make_config):
        agent = create_agent(make_config("tester"))
        assert agent.agent_id == "test_tester"

    def test_create_reviewer(self, make_config):
        agent = create_agent(make_config("reviewer"))
        assert agent.agent_id == "test_reviewer"

    def test_create_unknown_raises(self, make_config):
        with pytest.raises(ValueError, match="Unknown agent type"):
            create_agent(make_config("nonexistent_xyz"))


class TestAgentConfig:
    def test_max_iterations_default(self):
        config = AgentConfig(name="test", type="coder", model="ollama/test")
        assert config.max_iterations == 10

    def test_max_iterations_custom(self):
        config = AgentConfig(name="test", type="coder", model="ollama/test", max_iterations=5)
        assert config.max_iterations == 5
