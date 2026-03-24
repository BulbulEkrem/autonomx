"""Model Manager Agent — assigns LLM models to workers and tracks performance."""

from __future__ import annotations

import json
import logging

from autonomx_agents.core.base_agent import AgentContext, AgentResult, BaseAgent
from autonomx_agents.core.registry import agent_register
from autonomx_agents.llm import LLMGateway

logger = logging.getLogger(__name__)

SYSTEM_PROMPT = """\
You are the Model Manager agent in an autonomous software development system.
Your role is to:
1. Assign the best LLM model to each coder worker based on task complexity
2. Track model performance (speed, quality, cost)
3. Escalate to stronger models when a worker fails
4. Optimize model usage across the system

Input: Task list with complexity ratings and available models
Output format (JSON):
{
  "assignments": [
    {
      "task_id": "TASK-001",
      "worker_id": "string",
      "model": "provider/model-name",
      "reason": "string"
    }
  ],
  "escalation_plan": {
    "TASK-ID": ["model1", "model2", "model3"]
  },
  "notes": "string"
}
"""


@agent_register("model_manager")
class ModelManagerAgent(BaseAgent):
    """Assigns LLM models to workers and manages escalation paths."""

    async def execute(self, context: AgentContext) -> AgentResult:
        gateway = LLMGateway()

        previous = context.metadata.get("previous_context", {})
        context_text = json.dumps(previous, indent=2) if isinstance(previous, dict) else str(previous)

        messages = [
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": f"Assign models to workers for these tasks:\n\n{context_text}"},
        ]

        try:
            parsed, response = await gateway.completion_json(
                model=self.model,
                messages=messages,
                temperature=self.config.parameters.temperature,
                max_tokens=self.config.parameters.max_tokens,
            )

            return AgentResult(
                success=True,
                result=parsed,
                tokens_used=response.total_tokens,
            )
        except Exception as e:
            self.logger.error("Model Manager execution failed: %s", e)
            return AgentResult(success=False, error=str(e))
