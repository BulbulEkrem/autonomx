-- AutoNomX Initial Database Schema
-- Migration: 001_InitialCreate
-- Date: 2026-03-23

-- ── Projects ────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS projects (
    "Id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Name"            VARCHAR(256) NOT NULL,
    "Description"     VARCHAR(4096),
    "Status"          VARCHAR(32) NOT NULL DEFAULT 'Created',
    "RepositoryPath"  VARCHAR(1024),
    "Config"          JSONB,
    "CreatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"       TIMESTAMPTZ
);

-- ── Tasks (+ Task Board) ───────────────────────────────────
CREATE TABLE IF NOT EXISTS tasks (
    "Id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId"       UUID NOT NULL REFERENCES projects("Id") ON DELETE CASCADE,
    "Title"           VARCHAR(512) NOT NULL,
    "Description"     VARCHAR(8192),
    "Status"          VARCHAR(32) NOT NULL DEFAULT 'Ready',
    "Priority"        VARCHAR(16) NOT NULL DEFAULT 'Should',
    "AssignedAgent"   VARCHAR(128),
    "AssignedWorker"  VARCHAR(128),
    "GitBranch"       VARCHAR(256),
    "RetryCount"      INT NOT NULL DEFAULT 0,
    "MaxRetries"      INT NOT NULL DEFAULT 3,
    "Dependencies"    JSONB NOT NULL DEFAULT '[]',
    "FilesTouched"    JSONB NOT NULL DEFAULT '[]',
    "LockedFiles"     JSONB NOT NULL DEFAULT '[]',
    "CreatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"       TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_tasks_project_status ON tasks("ProjectId", "Status");

-- ── Agents ──────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS agents (
    "Id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Name"            VARCHAR(128) NOT NULL UNIQUE,
    "Type"            VARCHAR(32) NOT NULL,
    "Model"           VARCHAR(256) NOT NULL,
    "Provider"        VARCHAR(64) NOT NULL,
    "IsActive"        BOOLEAN NOT NULL DEFAULT TRUE,
    "LlmConfig"       JSONB,
    "CreatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"       TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_agents_type ON agents("Type");

-- ── Pipeline Runs ───────────────────────────────────────────
CREATE TABLE IF NOT EXISTS pipeline_runs (
    "Id"               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId"        UUID NOT NULL REFERENCES projects("Id") ON DELETE CASCADE,
    "Status"           VARCHAR(32) NOT NULL DEFAULT 'Pending',
    "CurrentStep"      VARCHAR(128) NOT NULL,
    "CurrentIteration" INT NOT NULL DEFAULT 0,
    "StartedAt"        TIMESTAMPTZ,
    "CompletedAt"      TIMESTAMPTZ,
    "ErrorMessage"     VARCHAR(4096),
    "CreatedAt"        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"        TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_pipeline_runs_project_status ON pipeline_runs("ProjectId", "Status");

-- ── Coder Workers ───────────────────────────────────────────
CREATE TABLE IF NOT EXISTS coder_workers (
    "Id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Name"            VARCHAR(128) NOT NULL,
    "Model"           VARCHAR(256) NOT NULL,
    "Provider"        VARCHAR(64) NOT NULL,
    "Status"          VARCHAR(32) NOT NULL DEFAULT 'Idle',
    "CurrentTaskId"   UUID REFERENCES tasks("Id") ON DELETE SET NULL,
    "CreatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"       TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_coder_workers_status ON coder_workers("Status");

-- ── Agent History ───────────────────────────────────────────
CREATE TABLE IF NOT EXISTS agent_history (
    "Id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "AgentId"         UUID NOT NULL REFERENCES agents("Id") ON DELETE CASCADE,
    "AgentInstanceId" VARCHAR(128),
    "TaskId"          UUID REFERENCES tasks("Id") ON DELETE SET NULL,
    "Role"            VARCHAR(32) NOT NULL,
    "Content"         TEXT NOT NULL,
    "ModelUsed"       VARCHAR(256),
    "TokensUsed"      INT NOT NULL DEFAULT 0,
    "CreatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"       TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_agent_history_agent ON agent_history("AgentId");
CREATE INDEX IF NOT EXISTS idx_agent_history_task ON agent_history("TaskId");
CREATE INDEX IF NOT EXISTS idx_agent_history_instance ON agent_history("AgentInstanceId");

-- ── Agent Metrics ───────────────────────────────────────────
CREATE TABLE IF NOT EXISTS agent_metrics (
    "Id"               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "AgentId"          UUID NOT NULL REFERENCES agents("Id") ON DELETE CASCADE,
    "ModelUsed"        VARCHAR(256) NOT NULL,
    "AvgIterations"    DOUBLE PRECISION NOT NULL DEFAULT 0,
    "AvgScore"         DOUBLE PRECISION NOT NULL DEFAULT 0,
    "TotalExecutions"  INT NOT NULL DEFAULT 0,
    "SuccessCount"     INT NOT NULL DEFAULT 0,
    "FailureCount"     INT NOT NULL DEFAULT 0,
    "TotalTokensUsed"  BIGINT NOT NULL DEFAULT 0,
    "AvgDurationSeconds" DOUBLE PRECISION NOT NULL DEFAULT 0,
    "CreatedAt"        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"        TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_agent_metrics_agent_model ON agent_metrics("AgentId", "ModelUsed");

-- ── Messages ────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS messages (
    "Id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "FromAgent"       VARCHAR(128) NOT NULL,
    "ToAgent"         VARCHAR(128) NOT NULL,
    "EventType"       VARCHAR(64) NOT NULL,
    "Payload"         JSONB,
    "ProjectId"       UUID,
    "TaskId"          UUID,
    "CreatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"       TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_messages_project ON messages("ProjectId");
CREATE INDEX IF NOT EXISTS idx_messages_event_type ON messages("EventType");

-- ── Files ───────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS files (
    "Id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId"       UUID NOT NULL REFERENCES projects("Id") ON DELETE CASCADE,
    "TaskId"          UUID REFERENCES tasks("Id") ON DELETE SET NULL,
    "Path"            VARCHAR(1024) NOT NULL,
    "ContentHash"     VARCHAR(128),
    "LockedByWorker"  VARCHAR(128),
    "LockedAt"        TIMESTAMPTZ,
    "CreatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"       TIMESTAMPTZ
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_files_project_path ON files("ProjectId", "Path");

-- ── Change Log ──────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS change_log (
    "Id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProjectId"       UUID NOT NULL REFERENCES projects("Id") ON DELETE CASCADE,
    "ChangeType"      VARCHAR(32) NOT NULL,
    "UserMessage"     VARCHAR(8192) NOT NULL,
    "AgentResponse"   VARCHAR(8192),
    "Decisions"       JSONB,
    "IsApproved"      BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"       TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_change_log_project ON change_log("ProjectId");
