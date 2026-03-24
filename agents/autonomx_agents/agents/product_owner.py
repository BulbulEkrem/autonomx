"""Product Owner Agent — analyzes user requests and creates user stories."""

from __future__ import annotations

import json
import logging

from autonomx_agents.core.base_agent import AgentContext, AgentResult, BaseAgent
from autonomx_agents.core.registry import agent_register
from autonomx_agents.llm import LLMGateway

logger = logging.getLogger(__name__)

SYSTEM_PROMPT = """\
You are the Product Owner agent in an autonomous software development system.
Your role is to:
1. Analyze natural language user requests
2. Break them down into clear user stories with acceptance criteria
3. Prioritize stories (Must / Should / Could)
4. Identify ambiguities and ask clarifying questions

Output format (JSON):
{
  "project_name": "string",
  "summary": "string",
  "user_stories": [
    {
      "id": "US-001",
      "title": "string",
      "description": "As a [user], I want [feature], so that [benefit]",
      "acceptance_criteria": ["string"],
      "priority": "Must | Should | Could",
      "estimated_complexity": "S | M | L | XL"
    }
  ],
  "questions": ["string"],
  "assumptions": ["string"]
}
"""


@agent_register("product_owner")
class ProductOwnerAgent(BaseAgent):
    """Analyzes user requests and produces structured user stories."""

    async def execute(self, context: AgentContext) -> AgentResult:
        gateway = LLMGateway()

        # Build the user request from context
        user_request = self._extract_request(context)

        messages = [
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": user_request},
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
            self.logger.error("Product Owner execution failed: %s", e)
            return AgentResult(success=False, error=str(e))

    def _extract_request(self, context: AgentContext) -> str:
        """Extract the user request from context."""
        if context.task:
            return f"Project: {context.task.title}\n\n{context.task.description}"
        if "request" in context.metadata:
            return str(context.metadata["request"])
        return json.dumps(context.metadata)
