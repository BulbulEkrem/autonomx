# AutoNomX - Claude Code Project Instructions

## Project Overview
AutoNomX is an autonomous software company system. It takes natural language requests and automatically plans, codes, tests, and delivers software using AI agents.

## Architecture (Two-Layer)
- **.NET Core 8+** → Control plane: API, CLI, Orchestrator, State Machine, Project Manager, DB access
- **Python 3.11+** → Agent runtime: AI agents, LLM Gateway (LiteLLM), code execution
- **Communication** → gRPC (commands) + PostgreSQL LISTEN/NOTIFY (events)
- **Database** → PostgreSQL 16+ (JSONB, LISTEN/NOTIFY)
- **Code Execution** → Docker containers (Strategy Pattern, swappable)

## Monorepo Structure
```
autonomx/
├── protos/                  # gRPC shared definitions (.proto files)
├── src/                     # .NET Core (Clean Architecture)
│   ├── AutoNomX.sln
│   ├── core/
│   │   ├── AutoNomX.Domain/          # Entities, Enums, Interfaces, Events
│   │   ├── AutoNomX.Application/     # Commands, Queries, Services, StateMachine
│   │   └── AutoNomX.Infrastructure/  # Persistence, EventBus, Grpc, Docker
│   ├── api/
│   │   └── AutoNomX.Api/             # ASP.NET Core Web API + SignalR
│   ├── cli/
│   │   └── AutoNomX.Cli/             # CLI app (System.CommandLine)
│   └── tests/
├── agents/                  # Python agent runtime
│   ├── autonomx_agents/
│   │   ├── server.py                 # gRPC server entry point
│   │   ├── core/                     # BaseAgent, Registry, Config, Message
│   │   ├── llm/                      # LiteLLM gateway, prompts/
│   │   ├── agents/                   # Built-in agents (planner, coder, tester...)
│   │   ├── tools/                    # Agent tools (file, git, shell, search)
│   │   ├── executor/                 # Code execution (Docker, Host, Sandbox)
│   │   └── grpc_services/            # gRPC service implementations
│   ├── plugins/                      # User plugins (custom agents)
│   └── tests/
├── docker/                  # Dockerfiles
├── scripts/                 # Helper scripts (proto gen, setup, gh issues)
├── config/                  # Default configs (agents.yaml, pipelines.yaml)
├── workspace/               # Runtime project workspace
├── docs/                    # Documentation
├── docker-compose.yml
└── Makefile
```

## .NET Architecture (Clean Architecture)
```
Domain (innermost)     → No dependencies, entities, interfaces, enums
    ↑
Application            → Depends only on Domain, CQRS commands/queries
    ↑
Infrastructure         → External world (DB, gRPC, Docker, EventBus)
    ↑
Api / Cli              → Presentation layer
```

## Key Design Decisions
- **Agent Framework**: Fully custom (no LangChain/CrewAI)
- **Worker Pool**: Dynamic coder pool, workers self-pick tasks from a Kanban-style task board
- **File Locking**: Hard lock — two workers cannot modify the same files simultaneously
- **Git Workflow**: Feature branch per task, merge on reviewer approval
- **Event Bus**: PostgreSQL LISTEN/NOTIFY (upgradeable to Redis later)
- **Code Execution**: Docker primary, abstracted via Strategy Pattern (ICodeExecutor)
- **LLM Gateway**: LiteLLM — supports Ollama, LM Studio, OpenAI, Anthropic, etc.

## Agents
| Agent | Role | Default Model |
|-------|------|---------------|
| Product Owner | Request analysis, user stories, interactive chat | deepseek-r1:14b |
| Planner | Stories → technical tasks | deepseek-r1:14b |
| Architect | Structure, scaffolding, sprint management | qwen2.5-coder:14b |
| Model Manager | LLM assignment, performance tracking | deepseek-r1:14b |
| Coder Workers | Code writing (dynamic pool) | VARIABLE |
| Tester | Test writing + execution | qwen2.5-coder:14b |
| Reviewer | Code quality, security, approval | claude-sonnet |

## Pipeline (Iterative Loop)
```
User Request → Product Owner → Planner → Architect ──┐
                                                      │
    ┌─────────── ITERATIVE LOOP ──────────────────────┤
    │                                                  │
    │  Architect (sprint manager) ← ─── ─── ─── ──┐  │
    │      ↓                                       │  │
    │  Model Manager (assign worker + model)       │  │
    │      ↓                                       │  │
    │  Workers (self-pick, parallel, git branch)   │  │
    │      ↓                                       │  │
    │  Tester (test + run)                         │  │
    │      ↓                                       │  │
    │  Reviewer → APPROVE → merge → back to top ───┘  │
    │           → REVISION → back to worker            │
    │                                                  │
    └──── Loop until all tasks done ──→ DELIVER ───────┘
```

## Coding Conventions

### .NET
- Use C# 12+ features (primary constructors, collection expressions)
- Nullable reference types enabled
- Use `record` for DTOs and value objects
- Repository Pattern for data access
- MediatR for CQRS (Commands/Queries)
- Stateless library for state machine
- Npgsql for PostgreSQL (LISTEN/NOTIFY support)
- Grpc.Net.Client for gRPC

### Python
- Python 3.11+ with type hints everywhere
- async/await for all I/O operations
- dataclasses or Pydantic for models
- grpcio + grpcio-tools for gRPC
- litellm for LLM gateway
- pytest for testing
- Plugin system via decorators (@agent_register)

### Both
- All configs in YAML (config/ directory)
- Proto files are the source of truth for shared types
- JSON output format for all agent responses
- Structured logging (JSON format)
- Environment variables for secrets (never hardcode)

## Development Milestones
- **M0**: Project setup, folder structure, protos, docker-compose
- **M1**: .NET Domain & Infrastructure
- **M2**: Python Agent Core
- **M3**: gRPC Integration & Event Bus
- **M4**: Orchestrator & State Machine
- **M5**: CLI & First E2E Pipeline
- **M6**: Worker Pool & Parallel Execution
- **M7**: Git Integration & PO Chat
- **M8**: Model Manager & Optimization
- **M9**: Polish & Documentation

## Current Milestone: M0 - Project Setup & Foundation

## Commands Reference
```bash
make proto          # Generate C# + Python code from .proto files
make build          # Build .NET solution
make test           # Run all tests
make run            # Start all services (docker-compose up)
make setup          # First-time setup
```

## Important Notes
- Read docs/ARCHITECTURE.md for full architectural details
- Proto files in protos/ generate code for BOTH .NET and Python
- Always run `make proto` after changing .proto files
- Database migrations are in src/core/AutoNomX.Infrastructure/Persistence/Migrations/
- Agent prompts are in agents/autonomx_agents/llm/prompts/
- Full architecture document: docs/ARCHITECTURE.md
