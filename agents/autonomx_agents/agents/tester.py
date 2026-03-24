"""Tester Agent — writes tests and executes them."""

from __future__ import annotations

import json
import logging

from autonomx_agents.core.base_agent import AgentContext, AgentResult, BaseAgent
from autonomx_agents.core.registry import agent_register
from autonomx_agents.llm import LLMGateway

logger = logging.getLogger(__name__)

SYSTEM_PROMPT = """\
You are the Tester agent in an autonomous software development system.
Your role is to:
1. Write comprehensive tests for the code produced by coder agents
2. Specify test execution commands
3. Validate code against acceptance criteria
4. Report test results and coverage

Input: Code files and acceptance criteria from the task
Output format (JSON):
{
  "test_files": [
    {
      "path": "string",
      "content": "string (full test file content)",
      "description": "string"
    }
  ],
  "execution": {
    "command": "string (e.g., pytest tests/ -v)",
    "working_directory": "string",
    "timeout_seconds": 120
  },
  "coverage_targets": {
    "path": "string — expected coverage percentage"
  },
  "notes": "string"
}

Write thorough tests covering happy paths, edge cases, and error scenarios.
"""


@agent_register("tester")
class TesterAgent(BaseAgent):
    """Writes tests for code produced by coder agents."""

    async def execute(self, context: AgentContext) -> AgentResult:
        gateway = LLMGateway()

        # Build test prompt from code files and task
        prompt = self._build_test_prompt(context)

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

            return AgentResult(
                success=True,
                result=parsed,
                tokens_used=response.total_tokens,
            )
        except Exception as e:
            self.logger.error("Tester execution failed: %s", e)
            return AgentResult(success=False, error=str(e))

    def _build_test_prompt(self, context: AgentContext) -> str:
        """Build the testing prompt from code context."""
        parts = []

        if context.task:
            parts.append(f"Task: {context.task.title}")
            parts.append(f"Description: {context.task.description}")

        # Include code files to test
        if context.files:
            parts.append("\nCode files to test:")
            for path, content in context.files.items():
                parts.append(f"\n--- {path} ---\n{content}")

        # Include coder output from previous context
        previous = context.metadata.get("previous_context")
        if previous and isinstance(previous, dict) and "files" in previous:
            parts.append("\nCoder output files:")
            for f in previous["files"]:
                parts.append(f"\n--- {f.get('path', 'unknown')} ---\n{f.get('content', '')}")

        return "\n".join(parts)
