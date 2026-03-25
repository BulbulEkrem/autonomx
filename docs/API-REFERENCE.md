# AutoNomX API Reference

AutoNomX uses **gRPC** for communication between the .NET control plane and the Python agent runtime. All service definitions are in the `protos/` directory and serve as the source of truth for both layers.

## Table of Contents

- [Agent Service](#agent-service)
- [Project Service](#project-service)
- [Code Execution Service](#code-execution-service)
- [Common Types](#common-types)

---

## Agent Service

**Proto file:** `protos/agent_service.proto`
**Package:** `autonomx.agent`
**Direction:** .NET → Python (the .NET orchestrator calls agents on the Python runtime)

### Service Definition

```protobuf
service AgentService {
  rpc ExecuteAgent(ExecuteAgentRequest) returns (ExecuteAgentResponse);
  rpc ExecuteAgentStream(ExecuteAgentRequest) returns (stream AgentStreamEvent);
  rpc GetAgentStatus(GetAgentStatusRequest) returns (GetAgentStatusResponse);
  rpc CancelAgent(CancelAgentRequest) returns (CancelAgentResponse);
}
```

### Methods

#### ExecuteAgent

Execute an agent synchronously. Blocks until the agent completes.

| Field | Type | Description |
|-------|------|-------------|
| **Request** | | |
| `execution_id` | `string` | Unique execution identifier (UUID) |
| `project_id` | `string` | Project this execution belongs to |
| `agent_type` | `AgentType` | Agent to execute (e.g., `AGENT_TYPE_CODER`) |
| `config` | `AgentConfig` | Agent configuration (model, provider, params) |
| `task` | `TaskInfo` | Task information for the agent |
| `context` | `string` | JSON — context from previous agents in the pipeline |
| `metadata` | `map<string, string>` | Additional key-value metadata |
| **Response** | | |
| `execution_id` | `string` | Matching execution ID |
| `success` | `bool` | Whether execution succeeded |
| `result` | `string` | JSON — agent output |
| `error` | `string` | Error message (empty on success) |
| `metrics` | `AgentMetrics` | Token usage, duration, iterations |

#### ExecuteAgentStream

Execute an agent with real-time streaming output. Returns a stream of events.

Uses the same `ExecuteAgentRequest` as `ExecuteAgent`.

**Stream events:**

| Field | Type | Description |
|-------|------|-------------|
| `execution_id` | `string` | Matching execution ID |
| `event_type` | `StreamEventType` | Event category |
| `data` | `string` | JSON payload |
| `timestamp` | `string` | ISO 8601 timestamp |

**StreamEventType values:**

| Value | Description |
|-------|-------------|
| `STREAM_EVENT_TYPE_LOG` | Log message from the agent |
| `STREAM_EVENT_TYPE_PROGRESS` | Progress update (0.0–1.0) |
| `STREAM_EVENT_TYPE_OUTPUT` | Partial output from the agent |
| `STREAM_EVENT_TYPE_ERROR` | Error occurred during execution |
| `STREAM_EVENT_TYPE_COMPLETED` | Agent execution completed |

#### GetAgentStatus

Query the current status of a running agent execution.

| Field | Type | Description |
|-------|------|-------------|
| **Request** | | |
| `execution_id` | `string` | Execution to query |
| **Response** | | |
| `execution_id` | `string` | Matching execution ID |
| `agent_type` | `AgentType` | Type of agent running |
| `status` | `string` | Current status (running, completed, failed) |
| `progress` | `double` | Progress percentage (0.0–1.0) |

#### CancelAgent

Cancel a running agent execution.

| Field | Type | Description |
|-------|------|-------------|
| **Request** | | |
| `execution_id` | `string` | Execution to cancel |
| `reason` | `string` | Cancellation reason |
| **Response** | | |
| `success` | `bool` | Whether cancellation succeeded |
| `message` | `string` | Status message |

### AgentMetrics

```protobuf
message AgentMetrics {
  int32 total_tokens = 1;
  int32 prompt_tokens = 2;
  int32 completion_tokens = 3;
  double duration_seconds = 4;
  int32 iterations = 5;
  string model_used = 6;
}
```

---

## Project Service

**Proto file:** `protos/project_service.proto`
**Package:** `autonomx.project`
**Direction:** Python → .NET (agents query project/task state from the control plane)

### Service Definition

```protobuf
service ProjectService {
  rpc GetProject(GetProjectRequest) returns (GetProjectResponse);
  rpc GetTaskBoard(GetTaskBoardRequest) returns (GetTaskBoardResponse);
  rpc UpdateTaskStatus(UpdateTaskStatusRequest) returns (UpdateTaskStatusResponse);
  rpc AcquireFileLock(FileLockRequest) returns (FileLockResponse);
  rpc ReleaseFileLock(FileLockRequest) returns (FileLockResponse);
}
```

### Methods

#### GetProject

Fetch project information including all tasks.

| Field | Type | Description |
|-------|------|-------------|
| **Request** | | |
| `project_id` | `string` | Project ID to fetch |
| **Response** | | |
| `project_id` | `string` | Project identifier |
| `name` | `string` | Project name |
| `description` | `string` | Project description |
| `status` | `ProjectStatus` | Current project status |
| `config` | `string` | JSON — project configuration |
| `tasks` | `repeated TaskInfo` | All tasks in the project |

#### GetTaskBoard

Get the task board (Kanban-style) with optional status filtering.

| Field | Type | Description |
|-------|------|-------------|
| **Request** | | |
| `project_id` | `string` | Project ID |
| `filter_status` | `TaskStatus` | Optional status filter |
| **Response** | | |
| `tasks` | `repeated TaskInfo` | Matching tasks |

#### UpdateTaskStatus

Update the status of a task (e.g., mark as in-progress, completed, failed).

| Field | Type | Description |
|-------|------|-------------|
| **Request** | | |
| `task_id` | `string` | Task to update |
| `new_status` | `TaskStatus` | New status value |
| `assigned_worker` | `string` | Worker assigned (if applicable) |
| `files_touched` | `repeated string` | Files modified by the task |
| `result` | `string` | JSON — optional task result |
| **Response** | | |
| `success` | `bool` | Whether update succeeded |
| `message` | `string` | Status message |

#### AcquireFileLock / ReleaseFileLock

Manage file-level locks to prevent concurrent modifications.

| Field | Type | Description |
|-------|------|-------------|
| **Request** | | |
| `task_id` | `string` | Task requesting the lock |
| `worker_id` | `string` | Worker requesting the lock |
| `file_paths` | `repeated string` | Files to lock/unlock |
| **Response** | | |
| `success` | `bool` | Whether lock operation succeeded |
| `locked_by_others` | `repeated string` | Files locked by other workers |
| `message` | `string` | Status message |

---

## Code Execution Service

**Proto file:** `protos/code_execution_service.proto`
**Package:** `autonomx.execution`
**Direction:** Python → Host (agents execute code in sandboxed environments)

### Service Definition

```protobuf
service CodeExecutionService {
  rpc Execute(ExecuteCodeRequest) returns (ExecuteCodeResponse);
  rpc ExecuteStream(ExecuteCodeRequest) returns (stream ExecuteCodeEvent);
}
```

### Methods

#### Execute

Execute a command and return the complete result.

| Field | Type | Description |
|-------|------|-------------|
| **Request** | | |
| `execution_id` | `string` | Unique execution identifier |
| `project_id` | `string` | Project context |
| `command` | `string` | Shell command to execute |
| `working_directory` | `string` | Working directory path |
| `environment` | `ExecutionEnvironment` | Execution environment |
| `timeout_seconds` | `int32` | Execution timeout |
| `env_vars` | `map<string, string>` | Environment variables |
| **Response** | | |
| `execution_id` | `string` | Matching execution ID |
| `exit_code` | `int32` | Process exit code |
| `stdout` | `string` | Standard output |
| `stderr` | `string` | Standard error |
| `duration_seconds` | `double` | Execution duration |
| `timed_out` | `bool` | Whether execution timed out |

#### ExecuteStream

Execute a command with real-time output streaming.

**Stream events:**

| Field | Type | Description |
|-------|------|-------------|
| `execution_id` | `string` | Matching execution ID |
| `output_type` | `OutputType` | Output stream type |
| `data` | `string` | Output content |
| `timestamp` | `string` | ISO 8601 timestamp |

**OutputType values:**

| Value | Description |
|-------|-------------|
| `OUTPUT_TYPE_STDOUT` | Standard output line |
| `OUTPUT_TYPE_STDERR` | Standard error line |
| `OUTPUT_TYPE_EXIT` | Process exited (data contains exit code) |

### ExecutionEnvironment

| Value | Description |
|-------|-------------|
| `EXECUTION_ENVIRONMENT_DOCKER` | Execute inside a Docker container (default, most secure) |
| `EXECUTION_ENVIRONMENT_HOST` | Execute on the host machine directly |
| `EXECUTION_ENVIRONMENT_SANDBOX` | Execute in a lightweight sandbox |

---

## Common Types

**Proto file:** `protos/common.proto`
**Package:** `autonomx.common`

### Enums

#### AgentType

| Value | Numeric | Description |
|-------|---------|-------------|
| `AGENT_TYPE_UNSPECIFIED` | 0 | Default / unknown |
| `AGENT_TYPE_PRODUCT_OWNER` | 1 | Requirements analysis, user stories |
| `AGENT_TYPE_PLANNER` | 2 | Task breakdown, dependencies |
| `AGENT_TYPE_ARCHITECT` | 3 | Structure, scaffolding, sprints |
| `AGENT_TYPE_MODEL_MANAGER` | 4 | LLM assignment, escalation |
| `AGENT_TYPE_CODER` | 5 | Code implementation |
| `AGENT_TYPE_TESTER` | 6 | Test writing, execution |
| `AGENT_TYPE_REVIEWER` | 7 | Code review, approval |

#### ProjectStatus

| Value | Numeric | Description |
|-------|---------|-------------|
| `PROJECT_STATUS_CREATED` | 1 | Project created, not yet started |
| `PROJECT_STATUS_PLANNING` | 2 | PO + Planner analyzing requirements |
| `PROJECT_STATUS_IN_PROGRESS` | 3 | Development loop active |
| `PROJECT_STATUS_TESTING` | 4 | Running tests |
| `PROJECT_STATUS_REVIEWING` | 5 | Code review in progress |
| `PROJECT_STATUS_COMPLETED` | 6 | All tasks done, delivered |
| `PROJECT_STATUS_FAILED` | 7 | Pipeline failed |
| `PROJECT_STATUS_PAUSED` | 8 | Pipeline paused (resumable) |

#### TaskStatus

| Value | Numeric | Description |
|-------|---------|-------------|
| `TASK_STATUS_READY` | 1 | Ready to be picked by a worker |
| `TASK_STATUS_IN_PROGRESS` | 2 | Worker is coding |
| `TASK_STATUS_TESTING` | 3 | Tests running |
| `TASK_STATUS_REVIEW` | 4 | Under code review |
| `TASK_STATUS_DONE` | 5 | Completed and merged |
| `TASK_STATUS_FAILED` | 6 | Failed (retryable) |
| `TASK_STATUS_REVISION` | 7 | Reviewer requested changes |

#### TaskPriority

| Value | Numeric | Description |
|-------|---------|-------------|
| `TASK_PRIORITY_MUST` | 1 | Critical, must be completed |
| `TASK_PRIORITY_SHOULD` | 2 | Important, should be completed |
| `TASK_PRIORITY_COULD` | 3 | Nice to have |

### Messages

#### AgentConfig

```protobuf
message AgentConfig {
  string agent_id = 1;        // Agent instance identifier
  AgentType agent_type = 2;   // Agent type enum
  string model = 3;           // LLM model (e.g., "ollama/qwen2.5-coder:32b")
  string provider = 4;        // Provider name (e.g., "ollama")
  map<string, string> parameters = 5;  // LLM parameters (temperature, max_tokens)
}
```

#### TaskInfo

```protobuf
message TaskInfo {
  string task_id = 1;
  string project_id = 2;
  string title = 3;
  string description = 4;
  TaskStatus status = 5;
  TaskPriority priority = 6;
  repeated string dependencies = 7;    // Task IDs that must complete first
  repeated string files_touched = 8;   // Files this task modifies
  string assigned_worker = 9;          // Worker assigned to this task
}
```

#### LlmConfig

```protobuf
message LlmConfig {
  string model = 1;        // Model identifier
  string provider = 2;     // Provider name
  double temperature = 3;  // Sampling temperature (0.0–1.0)
  int32 max_tokens = 4;    // Maximum tokens to generate
}
```

---

## gRPC Code Generation

After modifying any `.proto` file:

```bash
make proto
```

This generates:
- **C#** classes in `src/core/AutoNomX.Infrastructure/Grpc/Generated/`
- **Python** classes in `agents/autonomx_agents/grpc_services/generated/`

## Client Usage

### .NET Client (calling Python agents)

```csharp
// The IAgentGateway interface abstracts gRPC calls
var result = await agentGateway.RunAgentAsync(
    AgentType.Coder,
    new AgentExecutionContext
    {
        ProjectId = project.Id.ToString(),
        TaskId = task.Id.ToString(),
        Context = JsonSerializer.Serialize(contextData),
    },
    cancellationToken);
```

### Python Client (calling .NET project service)

```python
import grpc
from autonomx_agents.grpc_services.generated import project_service_pb2_grpc

channel = grpc.aio.insecure_channel("localhost:5001")
stub = project_service_pb2_grpc.ProjectServiceStub(channel)

response = await stub.GetProject(
    project_service_pb2.GetProjectRequest(project_id="...")
)
```
