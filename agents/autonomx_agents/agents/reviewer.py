"""Reviewer Agent — reviews code quality, security, and approves/rejects."""

from __future__ import annotations

import json
import logging

from autonomx_agents.core.base_agent import AgentContext, AgentResult, BaseAgent
from autonomx_agents.core.registry import agent_register
from autonomx_agents.llm import LLMGateway

logger = logging.getLogger(__name__)

SYSTEM_PROMPT = """\
You are the Reviewer agent in an autonomous software development system.
Your role is to:
1. Review code for quality, correctness, and best practices
2. Check for security vulnerabilities (OWASP Top 10)
3. Verify code meets the task requirements and acceptance criteria
4. Either APPROVE or request REVISION with specific feedback

Input: Code files, test results, and task requirements
Output format (JSON):
{
  "decision": "APPROVE | REVISION",
  "overall_score": 8,
  "categories": {
    "correctness": {"score": 9, "notes": "string"},
    "code_quality": {"score": 8, "notes": "string"},
    "security": {"score": 7, "notes": "string"},
    "testing": {"score": 8, "notes": "string"},
    "architecture": {"score": 8, "notes": "string"}
  },
  "issues": [
    {
      "severity": "critical | major | minor | suggestion",
      "file": "string",
      "line": 0,
      "description": "string",
      "suggestion": "string"
    }
  ],
  "feedback": "string (detailed feedback for revision, if needed)",
  "notes": "string"
}

Be thorough but fair. Only request REVISION for critical or major issues.
Minor issues and suggestions can be noted but should not block approval.
"""


@agent_register("reviewer")
class ReviewerAgent(BaseAgent):
    """Reviews code and either approves or requests revision."""

    async def execute(self, context: AgentContext) -> AgentResult:
        gateway = LLMGateway()

        prompt = self._build_review_prompt(context)

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

            decision = parsed.get("decision", "REVISION")
            self.logger.info(
                "Review decision: %s (score: %s)",
                decision,
                parsed.get("overall_score", "N/A"),
            )

            return AgentResult(
                success=True,
                result=parsed,
                tokens_used=response.total_tokens,
            )
        except Exception as e:
            self.logger.error("Reviewer execution failed: %s", e)
            return AgentResult(success=False, error=str(e))

    def _build_review_prompt(self, context: AgentContext) -> str:
        """Build review prompt from code and test context."""
        parts = []

        if context.task:
            parts.append(f"Task: {context.task.title}")
            parts.append(f"Acceptance Criteria: {context.task.description}")

        # Code files to review
        if context.files:
            parts.append("\nCode files to review:")
            for path, content in context.files.items():
                parts.append(f"\n--- {path} ---\n{content}")

        # Previous context (coder + tester output)
        previous = context.metadata.get("previous_context")
        if previous and isinstance(previous, dict):
            if "files" in previous:
                parts.append("\nProduced files:")
                for f in previous["files"]:
                    parts.append(f"\n--- {f.get('path', 'unknown')} ---\n{f.get('content', '')}")
            if "test_files" in previous:
                parts.append("\nTest files:")
                for f in previous["test_files"]:
                    parts.append(f"\n--- {f.get('path', 'unknown')} ---\n{f.get('content', '')}")

        return "\n".join(parts)
