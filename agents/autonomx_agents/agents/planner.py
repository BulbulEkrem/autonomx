"""Planner Agent — converts user stories into technical tasks."""

from __future__ import annotations

import json
import logging

from autonomx_agents.core.base_agent import AgentContext, AgentResult, BaseAgent
from autonomx_agents.core.registry import agent_register
from autonomx_agents.llm import LLMGateway

logger = logging.getLogger(__name__)

SYSTEM_PROMPT = """\
You are the Planner agent in an autonomous software development system.
Your role is to:
1. Take user stories from the Product Owner
2. Break each story into concrete technical tasks
3. Define task dependencies and ordering
4. Estimate task complexity for worker assignment

Input: User stories (JSON from Product Owner)
Output format (JSON):
{
  "tasks": [
    {
      "id": "TASK-001",
      "story_id": "US-001",
      "title": "string",
      "description": "string",
      "type": "feature | bugfix | refactor | test | config",
      "files_to_create": ["string"],
      "files_to_modify": ["string"],
      "dependencies": ["TASK-ID"],
      "priority": "Must | Should | Could",
      "estimated_complexity": "S | M | L | XL",
      "acceptance_criteria": ["string"]
    }
  ],
  "execution_order": [["TASK-001"], ["TASK-002", "TASK-003"]],
  "notes": "string"
}

The execution_order is a list of groups. Tasks in the same group can run in parallel.
"""


@agent_register("planner")
class PlannerAgent(BaseAgent):
    """Converts user stories into technical task breakdown."""

    async def execute(self, context: AgentContext) -> AgentResult:
        gateway = LLMGateway()

        # Get user stories from previous context
        stories = context.metadata.get("previous_context", {})
        stories_text = json.dumps(stories, indent=2) if isinstance(stories, dict) else str(stories)

        messages = [
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": f"Break these user stories into technical tasks:\n\n{stories_text}"},
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
            self.logger.error("Planner execution failed: %s", e)
            return AgentResult(success=False, error=str(e))
