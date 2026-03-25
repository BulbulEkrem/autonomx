# Changelog

All notable changes to the AutoNomX project are documented in this file.

## [1.0.0] - 2026-03-25

AutoNomX v1.0 — the complete autonomous software development system.

---

### M9: Polish & Documentation

- Complete README.md rewrite with architecture diagrams, CLI reference, quick start guide
- Plugin development guide (`docs/PLUGIN-GUIDE.md`)
- Deployment guide with OS-specific instructions (`docs/DEPLOYMENT.md`)
- gRPC API reference (`docs/API-REFERENCE.md`)
- XML documentation comments for all public .NET types
- Updated `.env.example` with all environment variables
- Code cleanup: consistent naming, documentation coverage

### M8: Model Manager & Optimization

- **Model Manager Agent** — intelligent LLM assignment based on task type and complexity
- Performance metrics collection and reporting (`MetricsService`)
- Model registry with capability profiles (`config/llm-models.yaml`)
- 4-tier escalation ladder: retry same → upgrade model → change worker → use API model
- `autonomx metrics` CLI command for performance analysis
- Task complexity scoring for optimal model selection

### M7: Git Integration & PO Chat

- **Git Service** — init, branch, commit, merge, diff, revert operations
- `IGitService` abstraction with `GitCliService` implementation
- Feature branch per task workflow (auto-create, ff-merge on approval)
- **Product Owner Chat** — interactive chat sessions for requirement clarification
- `ChatService` with session management and message history
- `autonomx chat <project-id>` CLI command
- Conflict detection and resolution in merge operations

### M6: Worker Pool & Parallel Execution

- **Dynamic Worker Pool** — add/remove coder workers at runtime
- `WorkerPoolService` with config-driven initialization
- Kanban-style **Task Board** with smart self-pick strategy
- File-level hard locking to prevent concurrent modifications
- `autonomx workers` CLI commands (list, add, remove)
- `autonomx config coders` for pool template configuration
- Parallel task execution with dependency-aware scheduling

### M5: CLI & First E2E Pipeline

- **CLI Application** using `System.CommandLine`
- Commands: `new`, `status`, `projects`, `run`, `logs`
- `--dry-run` mode for testing without LLM calls
- Rich console output with colored tables and status indicators
- First end-to-end pipeline execution (dry-run)
- `ConsoleOutput` helper for formatted CLI output

### M4: Orchestrator & State Machine

- **Pipeline State Machine** using Stateless library
- States: Idle → Planning → Architecting → Coding → Testing → Reviewing → Completed
- Triggers: Start, PlanReady, CodeReady, TestPassed/Failed, ReviewApproved/Rejected
- **OrchestratorService** — central pipeline coordinator
- `PipelineEventHandler` for state change events
- Automatic state transitions with agent execution at each step

### M3: gRPC Integration & Event Bus

- **gRPC service definitions** — AgentService, ProjectService, CodeExecutionService
- Proto files as shared contract between .NET and Python
- **PostgreSQL Event Bus** — LISTEN/NOTIFY for real-time domain events
- `IAgentGateway` implementation connecting .NET orchestrator to Python agents
- Bidirectional streaming support for live agent output

### M2: Python Agent Core

- **BaseAgent** abstract class with lifecycle hooks and metrics tracking
- **Agent Registry** with `@agent_register` decorator and auto-discovery
- **LLM Gateway** — LiteLLM wrapper supporting Ollama, LM Studio, OpenAI, Anthropic
- **Plugin System** — auto-discover custom agents from `plugins/` directory
- 7 built-in agents: ProductOwner, Planner, Architect, Coder, Tester, Reviewer, ModelManager
- **gRPC Server** with `AgentServicer` implementation
- Pydantic-based configuration loading from YAML
- Structured JSON output format for all agent responses

### M1: .NET Domain & Infrastructure

- **Domain Entities**: Project, TaskItem, PipelineRun, CoderWorker, AgentDefinition, AgentHistory, AgentMetrics, ChatSession, ChatMessage, ProjectFile, ChangeLog
- **Clean Architecture** layers: Domain → Application → Infrastructure → API/CLI
- **Enums**: ProjectStatus, TaskItemStatus, TaskItemPriority, AgentType, WorkerStatus, PipelineStatus, PipelineState, PipelineTrigger, ChangeType
- **Repository Pattern** with EF Core implementations
- **PostgreSQL** with Npgsql, JSONB support, LISTEN/NOTIFY
- Entity Framework Core DbContext and initial migrations

### M0: Project Setup & Foundation

- Monorepo structure with .NET + Python + Proto + Docker
- `docker-compose.yml` with PostgreSQL, API, Agent Runtime, Ollama services
- gRPC `.proto` definitions: common types, agent service, project service, code execution
- .NET solution with 6 projects (Domain, Application, Infrastructure, API, CLI, Tests)
- Python package structure with core, agents, llm, tools, executor, grpc_services
- `Makefile` with build, test, proto-gen, docker, lint commands
- YAML configuration files: agents, pipelines, LLM providers
- Architecture documentation (`docs/ARCHITECTURE.md`)
- Quick start guide (`docs/QUICKSTART.md`)

---

**Total:** 35 commits | 12 test files | 10 milestones (M0–M9)
