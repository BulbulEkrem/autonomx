"""Tests for the enhanced Model Manager agent."""

import json
import pytest

from autonomx_agents.agents.model_manager import ModelManagerAgent
from autonomx_agents.core.base_agent import AgentContext, TaskInfo
from autonomx_agents.core.config import AgentConfig, LlmParameters


@pytest.fixture
def agent_config():
    return AgentConfig(
        name="model_manager",
        type="model_manager",
        model="ollama/deepseek-r1:14b",
        provider="ollama",
        description="Test model manager",
        parameters=LlmParameters(temperature=0.3, max_tokens=2048),
    )


@pytest.fixture
def agent(agent_config):
    return ModelManagerAgent(agent_config)


class TestModelManagerAgent:
    def test_build_input_task_assignment(self, agent):
        context = AgentContext(
            execution_id="test-1",
            project_id="proj-1",
            task=TaskInfo(
                task_id="T-001",
                title="Build auth module",
                description="Implement OAuth2",
                priority="Must",
            ),
            metadata={
                "decision_type": "task_assignment",
                "workers": [
                    {"id": "w1", "name": "worker-a", "model": "qwen:32b"},
                ],
                "performance_history": {"worker-a": {"success_rate": 0.85}},
            },
        )

        input_data = agent._build_input(context)

        assert input_data["decision_type"] == "task_assignment"
        assert input_data["task"]["task_id"] == "T-001"
        assert input_data["task"]["title"] == "Build auth module"
        assert len(input_data["workers"]) == 1

    def test_build_input_model_switch(self, agent):
        context = AgentContext(
            execution_id="test-2",
            project_id="proj-1",
            metadata={
                "decision_type": "model_switch",
                "failure_info": {
                    "worker_id": "w1",
                    "current_model": "codellama:13b",
                    "failure_count": 3,
                },
            },
        )

        input_data = agent._build_input(context)

        assert input_data["decision_type"] == "model_switch"
        assert input_data["failure_info"]["failure_count"] == 3

    def test_validate_assignment_decision(self, agent):
        parsed = {"assigned_to": "worker-a", "model": "qwen:32b"}

        result = agent._validate_decision(parsed, "task_assignment")

        assert result["decision_type"] == "task_assignment"
        assert result["assigned_to"] == "worker-a"
        assert result["reasoning"] == "Default assignment"
        assert result["fallback_model"] is None

    def test_validate_switch_decision(self, agent):
        parsed = {
            "agent": "worker-b",
            "old_model": "codellama:13b",
            "new_model": "qwen:32b",
        }

        result = agent._validate_decision(parsed, "model_switch")

        assert result["decision_type"] == "model_switch"
        assert result["permanent"] is False
        assert result["revert_after"] == "task_completion"

    def test_generate_fallback_assignment(self, agent):
        input_data = {
            "decision_type": "task_assignment",
            "workers": [{"id": "w1", "name": "worker-a"}],
        }

        result = agent._generate_fallback(input_data)

        assert result["decision_type"] == "task_assignment"
        assert result["assigned_to"] == "w1"
        assert "Fallback" in result["reasoning"]

    def test_generate_fallback_switch(self, agent):
        input_data = {
            "decision_type": "model_switch",
            "failure_info": {
                "worker_id": "w1",
                "current_model": "codellama:13b",
            },
            "workers": [],
        }

        result = agent._generate_fallback(input_data)

        assert result["decision_type"] == "model_switch"
        assert result["new_model"] == "ollama/qwen2.5-coder:32b"
        assert result["escalation_step"] == 2

    def test_build_assignment_prompt(self, agent):
        input_data = {
            "task": {"title": "Build API", "complexity": "L"},
            "workers": [{"name": "worker-a"}],
            "performance_history": {"worker-a": {"success_rate": 0.9}},
            "model_registry": {},
        }

        prompt = agent._build_assignment_prompt(input_data)

        assert "Build API" in prompt
        assert "worker-a" in prompt
        assert "success_rate" in prompt

    def test_build_switch_prompt(self, agent):
        input_data = {
            "failure_info": {"worker_id": "w1", "failure_count": 3},
            "workers": [],
            "performance_history": {},
        }

        prompt = agent._build_switch_prompt(input_data)

        assert "failure_count" in prompt
        assert "escalation" in prompt.lower()

    def test_empty_workers_fallback(self, agent):
        input_data = {
            "decision_type": "task_assignment",
            "workers": [],
        }

        result = agent._generate_fallback(input_data)

        assert result["assigned_to"] == "worker-a"  # default
