# AutoNomX Plugin Development Guide

This guide walks you through creating custom agents for AutoNomX. The plugin system allows you to extend the agent team with specialized capabilities without modifying the core codebase.

## How Plugins Work

AutoNomX's agent runtime uses a **decorator-based registration system**. When the runtime starts, it:

1. Discovers all built-in agents from `agents/autonomx_agents/agents/`
2. Scans the `agents/plugins/` directory for custom plugins
3. Registers everything in a global registry via `@agent_register`
4. Instantiates agents on demand when the orchestrator requests them

## Step-by-Step: Create a Custom Agent

### Step 1: Create the Plugin Directory

```bash
mkdir -p agents/plugins/documentation_agent
touch agents/plugins/documentation_agent/__init__.py
touch agents/plugins/documentation_agent/documentation_agent.py
```

### Step 2: Extend BaseAgent

Every agent must inherit from `BaseAgent` and implement the `execute()` method.

```python
# agents/plugins/documentation_agent/documentation_agent.py

"""Documentation Agent — generates docs after code is written."""

from __future__ import annotations

import json
from typing import Any

from autonomx_agents.core.base_agent import (
    AgentContext,
    AgentResult,
    BaseAgent,
)
from autonomx_agents.core.registry import agent_register


@agent_register("documentation")
class DocumentationAgent(BaseAgent):
    """Agent that generates documentation for completed code tasks.

    This agent analyzes source files produced by coder workers and
    generates README files, API docs, and inline documentation.
    """

    SYSTEM_PROMPT = """You are a technical documentation specialist.
Given source code files, generate clear, comprehensive documentation.

Your output must be JSON with this structure:
{
    "files": {
        "docs/api.md": "# API Documentation\\n...",
        "README.md": "# Project\\n..."
    },
    "summary": "Generated X documentation files covering Y"
}

Documentation rules:
- Write clear, concise descriptions
- Include code examples for public APIs
- Document parameters, return values, and exceptions
- Use Markdown formatting
- Keep it practical — skip obvious boilerplate
"""

    async def execute(self, context: AgentContext) -> AgentResult:
        """Generate documentation for the given source files.

        Args:
            context: Agent context containing source files in context.files
                     and task info in context.task.

        Returns:
            AgentResult with generated documentation files.
        """
        # Build the prompt from context
        prompt = self._build_prompt(context)

        # Call the LLM via the gateway
        try:
            response = await self.call_llm(prompt)
            parsed = json.loads(response)

            return AgentResult(
                success=True,
                result={
                    "files": parsed.get("files", {}),
                    "summary": parsed.get("summary", "Documentation generated"),
                },
            )
        except json.JSONDecodeError:
            return AgentResult(
                success=False,
                error="Failed to parse LLM response as JSON",
                result={"raw_response": response},
            )

    def _build_prompt(self, context: AgentContext) -> str:
        """Build the documentation prompt from context files."""
        parts = []

        if context.task:
            parts.append(f"Task: {context.task.title}")
            if context.task.description:
                parts.append(f"Description: {context.task.description}")

        if context.files:
            parts.append("\n## Source Files\n")
            for path, content in context.files.items():
                parts.append(f"### {path}\n```\n{content}\n```\n")

        if context.feedback:
            parts.append(f"\nAdditional instructions: {context.feedback}")

        return "\n".join(parts)

    async def on_before_execute(self, context: AgentContext) -> None:
        """Log which files will be documented."""
        self.logger.info(
            "Documenting %d files for task: %s",
            len(context.files),
            context.task.title if context.task else "unknown",
        )

    async def on_after_execute(
        self, context: AgentContext, result: AgentResult
    ) -> None:
        """Log documentation generation results."""
        if result.success:
            file_count = len(result.result.get("files", {}))
            self.logger.info("Generated %d documentation files", file_count)

    async def call_llm(self, prompt: str) -> str:
        """Call the LLM with the system prompt and user prompt.

        Override this method if you need custom LLM interaction logic.
        """
        from autonomx_agents.llm.gateway import LLMGateway

        gateway = LLMGateway()
        response = await gateway.chat(
            model=self.config.model,
            system_prompt=self.SYSTEM_PROMPT,
            user_prompt=prompt,
            temperature=self.config.parameters.temperature,
            max_tokens=self.config.parameters.max_tokens,
        )
        return response
```

### Step 3: Register with @agent_register

The `@agent_register("documentation")` decorator (shown above) registers the agent class with the name `"documentation"`. This name is used to reference the agent in configuration and pipeline definitions.

Key rules:
- The name must be unique across all agents (built-in + plugins)
- If a name conflicts with an existing registration, a warning is logged and the new one takes precedence
- The decorator must be on the class definition (not a factory function)

### Step 4: Configure in agents.yaml

Add your agent definition to `config/agents.yaml`:

```yaml
agents:
  # ... existing agents ...

  documentation:
    type: documentation
    model: "ollama/qwen2.5-coder:14b"
    provider: ollama
    description: "Generates documentation for completed code"
    parameters:
      temperature: 0.3
      max_tokens: 4096
```

### Step 5: Use in Pipeline (Optional)

To integrate your agent into the pipeline, add it to `config/pipelines.yaml`:

```yaml
stages:
  # ... existing stages ...
  documentation:
    agent: documentation
    description: "Generate project documentation"
    run_after: review  # Run after code review passes
    optional: true     # Don't fail the pipeline if docs fail
```

Or invoke it programmatically from the orchestrator or another service.

## BaseAgent API Reference

### Constructor

```python
class BaseAgent(ABC):
    def __init__(self, config: AgentConfig) -> None:
```

Your agent receives an `AgentConfig` with:
- `config.name` — Agent instance name
- `config.type` — Agent type (matches registry name)
- `config.model` — LLM model to use (e.g., `"ollama/qwen2.5-coder:32b"`)
- `config.provider` — LLM provider (e.g., `"ollama"`, `"openai"`)
- `config.parameters` — `LlmParameters` (temperature, max_tokens, top_p, stop)
- `config.max_iterations` — Maximum retry iterations
- `config.system_prompt` — Override system prompt from config (optional)
- `config.tools` — List of tool names this agent can use

### Required Method

```python
@abstractmethod
async def execute(self, context: AgentContext) -> AgentResult:
    """Execute the agent's task. Must be implemented by subclasses."""
```

### Lifecycle Hooks

```python
async def on_before_execute(self, context: AgentContext) -> None:
    """Called before execute(). Use for setup, validation, logging."""

async def on_after_execute(self, context: AgentContext, result: AgentResult) -> None:
    """Called after execute(). Use for cleanup, metrics, post-processing."""
```

### Context Object

```python
class AgentContext(BaseModel):
    execution_id: str              # Unique execution identifier
    project_id: str                # Project this execution belongs to
    task: TaskInfo | None          # Task info (if applicable)
    files: dict[str, str]          # File path → content mapping
    history: list[dict[str, Any]]  # Previous conversation messages
    feedback: str                  # Reviewer/user feedback for retries
    metadata: dict[str, Any]       # Additional context data
```

### Result Object

```python
class AgentResult(BaseModel):
    success: bool                  # Whether execution succeeded
    result: dict[str, Any]         # Output data (JSON-serializable)
    error: str | None              # Error message if failed
    tokens_used: int               # LLM tokens consumed
    duration_seconds: float        # Execution time (auto-filled by run())
    iterations: int                # Number of LLM calls made
```

## Plugin Directory Structure

```
agents/plugins/
├── documentation_agent/
│   ├── __init__.py              # Can be empty
│   ├── documentation_agent.py   # Agent implementation
│   └── prompts/                 # Optional prompt templates
│       └── doc_template.txt
├── security_scanner/
│   ├── __init__.py
│   └── security_scanner.py
└── my_single_file_agent.py      # Single-file plugins also work
```

Both package-style (directory with `__init__.py`) and single-file plugins are supported.

## Best Practices

1. **Always return structured JSON** — The orchestrator expects `AgentResult.result` to be a dict that can be serialized to JSON and passed to subsequent agents.

2. **Use the system prompt wisely** — Define a clear `SYSTEM_PROMPT` class variable. Include the expected JSON output format so the LLM knows what structure to produce.

3. **Handle LLM failures gracefully** — LLM responses may be malformed. Always wrap JSON parsing in try/except and return a meaningful error in `AgentResult`.

4. **Leverage lifecycle hooks** — Use `on_before_execute` for validation and `on_after_execute` for metrics/logging rather than cluttering `execute()`.

5. **Keep agents focused** — Each agent should do one thing well. If you need complex behavior, compose multiple agents in the pipeline.

6. **Use type hints everywhere** — The codebase uses Python 3.11+ type hints consistently. Follow the pattern.

7. **Write tests** — Place tests in `agents/tests/test_<your_agent>.py` using pytest and async fixtures.

## Testing Your Plugin

```python
# agents/tests/test_documentation_agent.py

import pytest
from autonomx_agents.core.base_agent import AgentContext, TaskInfo
from autonomx_agents.core.config import AgentConfig, LlmParameters


@pytest.fixture
def config():
    return AgentConfig(
        name="documentation",
        type="documentation",
        model="ollama/qwen2.5-coder:14b",
        provider="ollama",
        description="Test documentation agent",
        parameters=LlmParameters(temperature=0.3, max_tokens=4096),
    )


@pytest.fixture
def context():
    return AgentContext(
        execution_id="test-1",
        project_id="proj-1",
        task=TaskInfo(
            task_id="T-001",
            title="Build REST API",
            description="Create a REST API with CRUD endpoints",
        ),
        files={
            "src/api.py": "from flask import Flask\napp = Flask(__name__)\n...",
        },
    )


def test_build_prompt(config, context):
    """Plugin auto-discovers, so just import after registration."""
    from plugins.documentation_agent.documentation_agent import DocumentationAgent

    agent = DocumentationAgent(config)
    prompt = agent._build_prompt(context)

    assert "Build REST API" in prompt
    assert "src/api.py" in prompt
```

Run tests:

```bash
cd agents
pytest tests/test_documentation_agent.py -v
```
