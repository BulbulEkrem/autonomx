"""Coder Agent — writes code for assigned tasks."""

from __future__ import annotations

import json
import logging

from autonomx_agents.core.base_agent import AgentContext, AgentResult, BaseAgent
from autonomx_agents.core.registry import agent_register
from autonomx_agents.llm import LLMGateway

logger = logging.getLogger(__name__)

SYSTEM_PROMPT = """\
You are a Coder agent in an autonomous software development system.
Your role is to:
1. Write clean, production-quality code for the assigned task
2. Follow the project's coding conventions and architecture
3. Create or modify files as specified in the task
4. Include appropriate error handling and logging

Input: A specific task with file paths and requirements
Output format (JSON):
{
  "files": [
    {
      "path": "string",
      "action": "create | modify | delete",
      "content": "string (full file content for create, diff for modify)",
      "description": "string"
    }
  ],
  "summary": "string",
  "notes": "string",
  "needs_review": true
}

Write complete, working code. Do not use placeholders or TODO comments.
"""


@agent_register("coder")
class CoderAgent(BaseAgent):
    """Writes code for assigned tasks. Runs as part of the worker pool."""

    async def execute(self, context: AgentContext) -> AgentResult:
        gateway = LLMGateway()

        # Build task description
        task_desc = self._build_task_prompt(context)

        messages = [
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": task_desc},
        ]

        # If there's reviewer feedback, add it
        if context.feedback:
            messages.append({
                "role": "user",
                "content": f"Reviewer feedback to address:\n{context.feedback}",
            })

        iteration = 0
        max_iter = self.config.max_iterations

        while iteration < max_iter:
            iteration += 1
            self.logger.info("Coder iteration %d/%d", iteration, max_iter)

            try:
                parsed, response = await gateway.completion_json(
                    model=self.model,
                    messages=messages,
                    temperature=self.config.parameters.temperature,
                    max_tokens=self.config.parameters.max_tokens,
                )

                # Validate output has files
                if "files" in parsed and parsed["files"]:
                    return AgentResult(
                        success=True,
                        result=parsed,
                        tokens_used=response.total_tokens,
                        iterations=iteration,
                    )

                # No files produced — retry with guidance
                messages.append({
                    "role": "assistant",
                    "content": json.dumps(parsed),
                })
                messages.append({
                    "role": "user",
                    "content": "Your output must include a 'files' array with actual code. Please try again.",
                })

            except Exception as e:
                self.logger.error("Coder iteration %d failed: %s", iteration, e)
                if iteration >= max_iter:
                    return AgentResult(success=False, error=str(e), iterations=iteration)

        return AgentResult(
            success=False,
            error=f"Max iterations ({max_iter}) reached without producing valid output",
            iterations=iteration,
        )

    def _build_task_prompt(self, context: AgentContext) -> str:
        """Build the task prompt from context."""
        parts = []

        if context.task:
            parts.append(f"Task: {context.task.title}")
            parts.append(f"Description: {context.task.description}")

        # Include existing file contents for modification
        if context.files:
            parts.append("\nExisting files:")
            for path, content in context.files.items():
                parts.append(f"\n--- {path} ---\n{content}")

        # Include previous context
        previous = context.metadata.get("previous_context")
        if previous:
            parts.append(f"\nArchitecture context:\n{json.dumps(previous, indent=2)}")

        return "\n".join(parts)
