"""Model Manager Agent — intelligent model assignment and escalation.

Enhanced for M8: real task analysis, performance-based decisions,
escalation ladder, and model switching on failure.
"""

from __future__ import annotations

import json
import logging
from typing import Any

from autonomx_agents.core.base_agent import AgentContext, AgentResult, BaseAgent
from autonomx_agents.core.registry import agent_register
from autonomx_agents.llm import LLMGateway

logger = logging.getLogger(__name__)

SYSTEM_PROMPT = """\
You are the Model Manager agent in an autonomous software development system.
Your job is to make optimal model-to-worker assignments for coding tasks.

You will receive:
- Task details (type, complexity, language, domain, files_to_touch)
- Available workers and their current models
- Performance history (success rates, avg iterations, avg tokens per model)
- Model registry with capabilities

Decision Process:
1. Analyze task type (coding, testing, reviewing, planning)
2. Detect programming language (Python, C#, JavaScript, etc.)
3. Assess complexity (S=simple, M=medium, L=large, XL=extra-large)
4. Identify domain (auth, data, UI, DevOps, API, infrastructure)
5. Check worker performance history for this task type and language
6. Match the best worker + model combination

For task_assignment decisions, output:
{
  "decision_type": "task_assignment",
  "assigned_to": "worker-id",
  "model": "provider/model-name",
  "reasoning": "Why this worker and model are best for this task",
  "fallback_model": "provider/fallback-model",
  "fallback_agent": "worker-id",
  "task_analysis": {
    "type": "coding|testing|reviewing|planning",
    "language": "detected language",
    "complexity": "S|M|L|XL",
    "domain": "auth|data|UI|DevOps|API|infrastructure"
  }
}

For model_switch decisions (when a worker fails repeatedly), output:
{
  "decision_type": "model_switch",
  "agent": "worker-id",
  "old_model": "provider/old-model",
  "new_model": "provider/new-model",
  "reason": "Why switching",
  "escalation_step": 1-4,
  "permanent": false,
  "revert_after": "task_completion"
}

Escalation ladder (on repeated failures):
1. Same worker, upgrade model one tier
2. Same worker, best available local model
3. Different worker with better track record
4. External API model (last resort, most expensive)

Always prefer local models over API models for cost efficiency.
Always prefer models with proven track record for similar tasks.
"""


@agent_register("model_manager")
class ModelManagerAgent(BaseAgent):
    """Assigns LLM models to workers and manages escalation paths."""

    async def execute(self, context: AgentContext) -> AgentResult:
        gateway = LLMGateway()

        input_data = self._build_input(context)
        decision_type = input_data.get("decision_type", "task_assignment")

        prompt = self._build_prompt(decision_type, input_data)

        messages = [
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": prompt},
        ]

        try:
            parsed, response = await gateway.completion_json(
                model=self.model,
                messages=messages,
                temperature=self.config.parameters.temperature,
                max_tokens=self.config.parameters.max_tokens,
            )

            # Validate the decision
            parsed = self._validate_decision(parsed, decision_type)

            self.logger.info(
                "Model Manager decision: type=%s, result=%s",
                parsed.get("decision_type"),
                json.dumps(parsed, indent=2)[:200],
            )

            return AgentResult(
                success=True,
                result=parsed,
                tokens_used=response.total_tokens,
            )
        except Exception as e:
            self.logger.error("Model Manager execution failed: %s", e)

            # Return a safe fallback assignment
            fallback = self._generate_fallback(input_data)
            return AgentResult(
                success=True,
                result=fallback,
                tokens_used=0,
            )

    def _build_input(self, context: AgentContext) -> dict[str, Any]:
        """Extract structured input from agent context."""
        meta = context.metadata or {}
        previous = meta.get("previous_context", {})

        if isinstance(previous, str):
            try:
                previous = json.loads(previous)
            except (json.JSONDecodeError, TypeError):
                previous = {"raw": previous}

        return {
            "decision_type": meta.get("decision_type", "task_assignment"),
            "task": self._extract_task_info(context, previous),
            "workers": meta.get("workers", []),
            "performance_history": meta.get("performance_history", {}),
            "model_registry": meta.get("model_registry", {}),
            "failure_info": meta.get("failure_info", {}),
        }

    def _extract_task_info(
        self, context: AgentContext, previous: dict[str, Any]
    ) -> dict[str, Any]:
        """Extract task details from context."""
        task_info: dict[str, Any] = {}

        if context.task:
            task_info = {
                "task_id": context.task.task_id,
                "title": context.task.title,
                "description": context.task.description,
                "priority": context.task.priority,
            }

        # Merge with any previous context data
        if isinstance(previous, dict):
            for key in ("type", "complexity", "language", "domain", "files_to_touch"):
                if key in previous:
                    task_info[key] = previous[key]

        return task_info

    def _build_prompt(self, decision_type: str, input_data: dict[str, Any]) -> str:
        """Build the appropriate prompt based on decision type."""
        if decision_type == "model_switch":
            return self._build_switch_prompt(input_data)
        return self._build_assignment_prompt(input_data)

    def _build_assignment_prompt(self, input_data: dict[str, Any]) -> str:
        """Build prompt for task assignment decisions."""
        task = input_data.get("task", {})
        workers = input_data.get("workers", [])
        history = input_data.get("performance_history", {})
        registry = input_data.get("model_registry", {})

        parts = ["Assign the best worker and model for this task:\n"]

        parts.append(f"## Task\n{json.dumps(task, indent=2)}\n")

        if workers:
            parts.append(f"## Available Workers\n{json.dumps(workers, indent=2)}\n")

        if history:
            parts.append(
                f"## Performance History\n{json.dumps(history, indent=2)}\n"
            )

        if registry:
            parts.append(
                f"## Model Registry\n{json.dumps(registry, indent=2)}\n"
            )

        parts.append(
            "Return a task_assignment decision with the best worker+model match."
        )

        return "\n".join(parts)

    def _build_switch_prompt(self, input_data: dict[str, Any]) -> str:
        """Build prompt for model switch decisions."""
        failure = input_data.get("failure_info", {})
        workers = input_data.get("workers", [])
        history = input_data.get("performance_history", {})

        parts = ["A worker has failed repeatedly. Decide on model escalation:\n"]

        parts.append(f"## Failure Info\n{json.dumps(failure, indent=2)}\n")

        if workers:
            parts.append(f"## Available Workers\n{json.dumps(workers, indent=2)}\n")

        if history:
            parts.append(
                f"## Performance History\n{json.dumps(history, indent=2)}\n"
            )

        parts.append(
            "Return a model_switch decision following the escalation ladder."
        )

        return "\n".join(parts)

    def _validate_decision(
        self, parsed: dict[str, Any], expected_type: str
    ) -> dict[str, Any]:
        """Validate and normalize the decision output."""
        if "decision_type" not in parsed:
            parsed["decision_type"] = expected_type

        if parsed["decision_type"] == "task_assignment":
            parsed.setdefault("assigned_to", "worker-a")
            parsed.setdefault("model", self.model)
            parsed.setdefault("reasoning", "Default assignment")
            parsed.setdefault("fallback_model", None)
            parsed.setdefault("fallback_agent", None)

        elif parsed["decision_type"] == "model_switch":
            parsed.setdefault("agent", "worker-a")
            parsed.setdefault("old_model", self.model)
            parsed.setdefault("new_model", self.model)
            parsed.setdefault("reason", "Escalation due to failures")
            parsed.setdefault("escalation_step", 1)
            parsed.setdefault("permanent", False)
            parsed.setdefault("revert_after", "task_completion")

        return parsed

    def _generate_fallback(self, input_data: dict[str, Any]) -> dict[str, Any]:
        """Generate a safe fallback decision when LLM call fails."""
        workers = input_data.get("workers", [])
        decision_type = input_data.get("decision_type", "task_assignment")

        if decision_type == "model_switch":
            failure = input_data.get("failure_info", {})
            return {
                "decision_type": "model_switch",
                "agent": failure.get("worker_id", "worker-a"),
                "old_model": failure.get("current_model", self.model),
                "new_model": "ollama/qwen2.5-coder:32b",
                "reason": "Fallback: upgrading to largest available local model",
                "escalation_step": 2,
                "permanent": False,
                "revert_after": "task_completion",
            }

        # Default task assignment fallback
        first_worker = workers[0] if workers else {"id": "worker-a"}
        worker_id = first_worker.get("id", first_worker.get("name", "worker-a"))

        return {
            "decision_type": "task_assignment",
            "assigned_to": worker_id,
            "model": self.model,
            "reasoning": "Fallback: LLM unavailable, using default assignment",
            "fallback_model": "ollama/qwen2.5-coder:32b",
            "fallback_agent": None,
        }
