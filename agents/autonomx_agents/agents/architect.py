"""Architect Agent — designs project structure and manages sprints."""

from __future__ import annotations

import json
import logging

from autonomx_agents.core.base_agent import AgentContext, AgentResult, BaseAgent
from autonomx_agents.core.registry import agent_register
from autonomx_agents.llm import LLMGateway

logger = logging.getLogger(__name__)

SYSTEM_PROMPT = """\
You are the Architect agent in an autonomous software development system.
Your role is to:
1. Design the project folder structure and architecture
2. Create scaffolding (initial files, configs, dependencies)
3. Organize tasks into sprints
4. Ensure architectural consistency across the codebase

Input: Technical tasks from the Planner
Output format (JSON):
{
  "architecture": {
    "type": "string (e.g., monolith, microservice, library)",
    "language": "string",
    "framework": "string",
    "folder_structure": {
      "path": "description"
    }
  },
  "scaffolding": [
    {
      "path": "string",
      "content": "string",
      "description": "string"
    }
  ],
  "sprints": [
    {
      "id": "SPRINT-1",
      "name": "string",
      "tasks": ["TASK-001", "TASK-002"],
      "goal": "string"
    }
  ],
  "dependencies": {
    "package_name": "version"
  },
  "notes": "string"
}
"""


@agent_register("architect")
class ArchitectAgent(BaseAgent):
    """Designs project structure, scaffolding, and sprint organization."""

    async def execute(self, context: AgentContext) -> AgentResult:
        gateway = LLMGateway()

        previous = context.metadata.get("previous_context", {})
        context_text = json.dumps(previous, indent=2) if isinstance(previous, dict) else str(previous)

        messages = [
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": f"Design the architecture and sprint plan for:\n\n{context_text}"},
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
            self.logger.error("Architect execution failed: %s", e)
            return AgentResult(success=False, error=str(e))
