<p align="center">
  <h1 align="center">AutoNomX</h1>
  <p align="center"><strong>Autonomous Software Company — AI-powered development pipeline</strong></p>
</p>

---

AutoNomX is a fully autonomous software development system that transforms natural language requests into production-ready code. Describe what you want in plain English (or any language), and AutoNomX assembles an AI team of seven specialized agents — Product Owner, Planner, Architect, Model Manager, Coder Workers, Tester, and Reviewer — to analyze, plan, code, test, review, and deliver your software automatically.

Unlike simple code generators, AutoNomX operates as an **entire software company**. It breaks requirements into user stories, creates technical tasks with dependency graphs, assigns optimal AI models to each task based on performance history, manages parallel coding with file-level locking, runs tests, performs code reviews with security audits, and iterates until the code meets quality standards. Think of it as a tireless development team that works 24/7.

The system is built on a **two-layer architecture**: a .NET Core 8 control plane that manages orchestration, state machines, and persistence, paired with a Python agent runtime that handles AI reasoning, LLM communication, and code execution. They communicate via gRPC for commands and PostgreSQL LISTEN/NOTIFY for real-time events. Every agent runs on local LLMs by default (via Ollama), keeping your code and data private.

## Architecture

```
                           AutoNomX Architecture
 ┌─────────────────────────────────────────────────────────────────────┐
 │                        User (CLI / API)                            │
 └──────────────────────────────┬──────────────────────────────────────┘
                                │
 ┌──────────────────────────────▼──────────────────────────────────────┐
 │                    .NET Core 8+ (Control Plane)                     │
 │                                                                     │
 │  ┌──────────┐  ┌──────────────┐  ┌──────────────┐  ┌────────────┐ │
 │  │ CLI App  │  │  ASP.NET API │  │ Orchestrator │  │   State    │ │
 │  │ (System  │  │  + SignalR   │  │   Service    │  │  Machine   │ │
 │  │ Command  │  │              │  │              │  │ (Stateless)│ │
 │  │  Line)   │  │              │  │              │  │            │ │
 │  └──────────┘  └──────────────┘  └──────┬───────┘  └────────────┘ │
 │                                         │                          │
 │  ┌──────────────┐  ┌───────────┐  ┌─────▼────────┐               │
 │  │ Worker Pool  │  │   Task    │  │    Model     │               │
 │  │   Service    │  │   Board   │  │   Manager    │               │
 │  │              │  │  Service  │  │   Service    │               │
 │  └──────────────┘  └───────────┘  └──────────────┘               │
 │                                                                    │
 │  ┌──────────────────────────────────────────────────────────────┐  │
 │  │  PostgreSQL 16+ (JSONB + LISTEN/NOTIFY Event Bus)           │  │
 │  └──────────────────────────────────────────────────────────────┘  │
 └───────────────────────────┬─────────────────────────────────────────┘
                             │ gRPC (bidirectional streaming)
 ┌───────────────────────────▼─────────────────────────────────────────┐
 │                  Python 3.11+ (Agent Runtime)                       │
 │                                                                     │
 │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐│
 │  │ Product  │ │ Planner  │ │Architect │ │  Coder   │ │ Tester   ││
 │  │  Owner   │ │  Agent   │ │  Agent   │ │ Workers  │ │  Agent   ││
 │  └──────────┘ └──────────┘ └──────────┘ └──────────┘ └──────────┘│
 │  ┌──────────┐ ┌──────────┐                                        │
 │  │ Reviewer │ │  Model   │  ┌──────────────────────────────────┐  │
 │  │  Agent   │ │ Manager  │  │  LLM Gateway (LiteLLM)          │  │
 │  └──────────┘ └──────────┘  │  Ollama · LM Studio · OpenAI    │  │
 │                              │  Anthropic · Any OpenAI-compat   │  │
 │  ┌──────────────────────┐   └──────────────────────────────────┘  │
 │  │  Code Executor       │                                         │
 │  │  Docker · Host       │   ┌──────────────────────────────────┐  │
 │  │  Sandbox             │   │  Plugin System (@agent_register) │  │
 │  └──────────────────────┘   └──────────────────────────────────┘  │
 └─────────────────────────────────────────────────────────────────────┘
```

## Pipeline

```
User Request ──► Product Owner ──► Planner ──► Architect ───┐
                (analyze &         (stories →   (structure    │
                 user stories)      tasks)      & sprints)    │
                                                              │
    ┌──────────── ITERATIVE LOOP ─────────────────────────────┤
    │                                                         │
    │  Model Manager ─── assign optimal worker + model        │
    │       │                                                 │
    │  Coder Workers ─── parallel coding, file locks, git     │
    │       │              branch per task                     │
    │  Tester ─────────── write tests, execute, coverage      │
    │       │                                                 │
    │  Reviewer ──► APPROVE ──► merge ──► next task ──────────┘
    │             ► REVISION ──► back to worker (retry)
    │
    └──── All tasks complete ──► DELIVER
```

## Agent Team

| Agent | Role | Default Model | Description |
|-------|------|---------------|-------------|
| **Product Owner** | Requirements | `deepseek-r1:14b` | Analyzes user requests, creates user stories, interactive chat for clarification |
| **Planner** | Task Breakdown | `deepseek-r1:14b` | Converts user stories into technical tasks with dependencies and priorities |
| **Architect** | Structure | `qwen2.5-coder:14b` | Designs project structure, scaffolding, sprint management, conflict resolution |
| **Model Manager** | Optimization | `deepseek-r1:14b` | Intelligent LLM assignment based on task type, performance history, escalation |
| **Coder Workers** | Implementation | Variable | Dynamic pool of coding agents, self-pick tasks, parallel execution |
| **Tester** | Quality | `qwen2.5-coder:14b` | Writes and runs tests, reports coverage and failures |
| **Reviewer** | Approval | `claude-sonnet` | Code quality review, security audit, approve or request revision |

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0+ | Control plane build & runtime |
| [Python](https://python.org) | 3.11+ | Agent runtime |
| [Docker](https://docker.com) | 24+ | Code execution & services |
| [Docker Compose](https://docs.docker.com/compose/) | v2+ | Service orchestration |
| [PostgreSQL](https://postgresql.org) | 16+ | Database (or via Docker) |
| [Ollama](https://ollama.com) | Latest | Local LLM inference (recommended) |
| [Git](https://git-scm.com) | 2.40+ | Version control |

Optional: [LM Studio](https://lmstudio.ai) as an alternative local LLM provider.

## Quick Start

```bash
# 1. Clone the repository
git clone https://github.com/BulbulEkrem/autonomx.git
cd autonomx

# 2. Start infrastructure (PostgreSQL + Ollama)
docker-compose up -d postgres ollama

# 3. Pull required LLM models
ollama pull deepseek-r1:14b
ollama pull qwen2.5-coder:14b
ollama pull qwen2.5-coder:32b

# 4. Build .NET solution & install Python agents
dotnet build src/AutoNomX.sln
pip install -e ./agents

# 5. Run your first project (dry-run mode — no LLM calls)
dotnet run --project src/cli/AutoNomX.Cli -- new "Build a calculator app" --dry-run
```

## CLI Command Reference

| Command | Arguments / Options | Description |
|---------|---------------------|-------------|
| `autonomx new <description>` | `--dry-run` | Create a new project and start the AI pipeline |
| `autonomx status <project-id>` | | Show project status, pipeline state, git branch, recent commits |
| `autonomx projects` | | List all projects |
| `autonomx run` | `--project <id>` (required) | Resume a paused pipeline |
| `autonomx workers` | | Show worker pool status with performance metrics |
| `autonomx workers add` | `--model <m>` (required), `--provider <p>` (default: ollama), `--name <n>` | Add a new coder worker at runtime |
| `autonomx workers remove <name>` | `--force` | Remove a worker (graceful shutdown or forced) |
| `autonomx config coders` | `--count <n>` (default: 2), `--model <m>` (required), `--provider <p>` | Configure the default worker pool template |
| `autonomx chat <project-id>` | | Interactive chat with the Product Owner agent (approve/reject changes) |
| `autonomx metrics` | `<project-id>` (optional) | Show performance metrics: worker stats, model comparisons |
| `autonomx logs <project-id>` | `--agent <type>` | Show agent execution logs, optionally filtered by agent type |

## Configuration

AutoNomX uses YAML configuration files in the `config/` directory:

| File | Purpose |
|------|---------|
| `config/agents.yaml` | Agent definitions, models, parameters, worker pool template |
| `config/pipelines.yaml` | Pipeline stages, development loop, iteration limits, git workflow |
| `config/llm.yaml` | LLM provider definitions, base URLs, API keys, escalation strategy |
| `config/llm-models.yaml` | Model registry with capabilities, speed/cost tiers, escalation tiers |

Environment variables are configured in `.env` (copy from `.env.example`):

```bash
cp .env.example .env
# Edit .env with your settings
```

See [Deployment Guide](docs/DEPLOYMENT.md) for detailed configuration instructions.

## Worker Pool Management

```bash
# View current pool status
autonomx workers

# Add a high-performance worker
autonomx workers add --model "qwen2.5-coder:32b" --provider ollama --name "heavy-lifter"

# Add an API-backed worker for complex tasks
autonomx workers add --model "gpt-4o" --provider openai --name "cloud-worker"

# Remove a worker gracefully (finishes current task first)
autonomx workers remove "heavy-lifter"

# Force remove (immediately)
autonomx workers remove "heavy-lifter" --force

# Configure default pool template
autonomx config coders --count 3 --model "qwen2.5-coder:32b" --provider ollama
```

## Project Structure

```
autonomx/
├── protos/                          # gRPC shared definitions (.proto)
│   ├── common.proto                 #   Shared enums & messages
│   ├── agent_service.proto          #   Agent execution service
│   ├── project_service.proto        #   Project & task management
│   └── code_execution_service.proto #   Code execution service
├── src/                             # .NET Core (Clean Architecture)
│   ├── AutoNomX.sln
│   ├── core/
│   │   ├── AutoNomX.Domain/         #   Entities, Enums, Interfaces (no deps)
│   │   ├── AutoNomX.Application/    #   Services, CQRS, State Machine
│   │   └── AutoNomX.Infrastructure/ #   DB, gRPC clients, Docker, Git, EventBus
│   ├── api/AutoNomX.Api/            #   ASP.NET Core Web API + SignalR
│   ├── cli/AutoNomX.Cli/            #   CLI app (System.CommandLine)
│   └── tests/AutoNomX.Tests/        #   xUnit + NSubstitute tests
├── agents/                          # Python agent runtime
│   ├── autonomx_agents/
│   │   ├── server.py                #   gRPC server entry point
│   │   ├── core/                    #   BaseAgent, Registry, Config
│   │   ├── llm/                     #   LiteLLM gateway + prompts/
│   │   ├── agents/                  #   7 built-in agent implementations
│   │   ├── tools/                   #   Agent tools (file, git, shell, search)
│   │   ├── executor/                #   Code execution (Docker, Host, Sandbox)
│   │   └── grpc_services/           #   gRPC service implementations
│   ├── plugins/                     #   Custom agent plugins
│   └── tests/                       #   pytest test suite
├── config/                          # YAML configuration files
├── docker/                          # Dockerfiles
├── scripts/                         # Helper scripts (proto gen, setup)
├── workspace/                       # Runtime project workspace
├── docs/                            # Documentation
├── docker-compose.yml               # Service orchestration
└── Makefile                         # Build & development commands
```

## Development Guide

### Build & Test

```bash
# Build everything
make build                    # Build .NET solution

# Run all tests
make test                     # .NET + Python tests
make test-dotnet              # Only .NET tests
make test-python              # Only Python tests

# Linting
make lint                     # Run ruff (Python) + dotnet format (.NET)
```

### Generate gRPC Code

After modifying any `.proto` file in `protos/`:

```bash
make proto                    # Generates C# + Python code from .proto files
```

### Docker Operations

```bash
make run                      # Start all services (API, agents, postgres, ollama)
make run-dev                  # Start only infrastructure (postgres + ollama)
make stop                     # Stop all services
make logs                     # Follow service logs
make build-docker             # Build Docker images
```

### Clean Architecture Layers

```
Domain (innermost)     → No dependencies. Entities, interfaces, enums.
    ↑
Application            → Depends only on Domain. Services, CQRS, state machine.
    ↑
Infrastructure         → External integrations: DB (Npgsql), gRPC, Docker, Git.
    ↑
Api / Cli              → Presentation layer. Thin, delegates to Application.
```

## Plugin Development

AutoNomX supports custom agents via the plugin system. See the full [Plugin Development Guide](docs/PLUGIN-GUIDE.md) for details.

**Quick overview** — 5 steps to create a custom agent:

```python
# 1. Create agents/plugins/my_agent/my_agent.py
from autonomx_agents.core.base_agent import BaseAgent, AgentContext, AgentResult
from autonomx_agents.core.registry import agent_register

# 2. Register with decorator
@agent_register("my_custom_agent")
class MyCustomAgent(BaseAgent):
    SYSTEM_PROMPT = "You are a specialized agent that..."

    # 3. Implement execute()
    async def execute(self, context: AgentContext) -> AgentResult:
        prompt = self._build_prompt(context)
        response = await self.llm.chat(self.config.model, self.SYSTEM_PROMPT, prompt)
        return AgentResult(success=True, result={"output": response})

# 4. Add to config/agents.yaml
# 5. Reference in pipeline config if needed
```

## Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Agent Framework | Custom (no LangChain) | Full control over agent lifecycle, prompts, and execution |
| Worker Pool | Dynamic, self-pick | Workers score tasks by priority + affinity, enabling parallel execution |
| File Locking | Hard lock | Prevents merge conflicts — two workers cannot touch the same files |
| Git Workflow | Feature branch per task | Clean history, ff-only merge preference, reviewer approval gates |
| Event Bus | PostgreSQL LISTEN/NOTIFY | Zero additional infrastructure, upgradeable to Redis later |
| Code Execution | Docker (Strategy Pattern) | Secure sandboxing, swappable via `ICodeExecutor` interface |
| LLM Gateway | LiteLLM | Unified interface for Ollama, LM Studio, OpenAI, Anthropic, and more |
| Model Escalation | 4-tier ladder | Same model retry → upgrade local → change worker → API model |
| State Machine | Stateless library | Declarative state transitions, persistence-friendly |

## Documentation

- [Architecture Design](docs/ARCHITECTURE.md) — Full architectural details
- [Deployment Guide](docs/DEPLOYMENT.md) — Installation, configuration, troubleshooting
- [Plugin Guide](docs/PLUGIN-GUIDE.md) — Custom agent development
- [API Reference](docs/API-REFERENCE.md) — gRPC service definitions
- [Quick Start](docs/QUICKSTART.md) — Getting started guide
- [Changelog](CHANGELOG.md) — Release history

## License

This project is licensed under the [MIT License](LICENSE).
