using System.Runtime.CompilerServices;
using System.Text.Json;
using AutoNomX.Domain;
using AutoNomX.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoNomX.Infrastructure.Grpc;

/// <summary>
/// Mock agent gateway for dry-run/demo mode.
/// Returns realistic-looking mock responses without calling any LLM.
/// </summary>
public sealed class DryRunAgentGateway(ILogger<DryRunAgentGateway> logger) : IAgentGateway
{
    public async Task<AgentExecutionResult> RunAgentAsync(
        AgentType agentType,
        AgentExecutionContext context,
        CancellationToken ct = default)
    {
        logger.LogInformation("[DRY-RUN] Running agent {AgentType} (execution={Id})",
            agentType, context.ExecutionId);

        // Simulate some work
        await Task.Delay(500, ct);

        var result = GenerateMockResult(agentType, context);

        logger.LogInformation("[DRY-RUN] Agent {AgentType} completed", agentType);

        return new AgentExecutionResult(
            ExecutionId: context.ExecutionId,
            Success: true,
            ResultJson: JsonSerializer.Serialize(result),
            TotalTokens: 150,
            DurationSeconds: 0.5,
            Iterations: 1,
            ModelUsed: "dry-run/mock");
    }

    public async IAsyncEnumerable<AgentProgressEvent> RunAgentStreamAsync(
        AgentType agentType,
        AgentExecutionContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return new AgentProgressEvent(context.ExecutionId,
            AgentProgressEventType.Log, $"{{\"message\":\"[DRY-RUN] Starting {agentType}\"}}", DateTime.UtcNow);

        await Task.Delay(300, ct);

        yield return new AgentProgressEvent(context.ExecutionId,
            AgentProgressEventType.Progress, "{\"progress\":0.5}", DateTime.UtcNow);

        await Task.Delay(200, ct);

        var result = GenerateMockResult(agentType, context);

        yield return new AgentProgressEvent(context.ExecutionId,
            AgentProgressEventType.Output, JsonSerializer.Serialize(result), DateTime.UtcNow);

        yield return new AgentProgressEvent(context.ExecutionId,
            AgentProgressEventType.Completed, "{\"success\":true}", DateTime.UtcNow);
    }

    public Task<AgentStatusInfo> GetAgentStatusAsync(string executionId, CancellationToken ct = default)
    {
        return Task.FromResult(new AgentStatusInfo(executionId, AgentType.Coder, "completed", 1.0));
    }

    public Task<bool> CancelAgentAsync(string executionId, string reason = "", CancellationToken ct = default)
    {
        logger.LogInformation("[DRY-RUN] Cancel requested for {Id}: {Reason}", executionId, reason);
        return Task.FromResult(true);
    }

    private static object GenerateMockResult(AgentType agentType, AgentExecutionContext context)
    {
        return agentType switch
        {
            AgentType.ProductOwner => new
            {
                project_name = "mock-project",
                summary = "Mock project analysis",
                user_stories = new[]
                {
                    new
                    {
                        id = "US-001",
                        title = "Core functionality",
                        description = "As a user, I want the core feature, so that I can use it",
                        acceptance_criteria = new[] { "Feature works correctly", "Has error handling" },
                        priority = "Must",
                        estimated_complexity = "M"
                    }
                },
                questions = Array.Empty<string>(),
                assumptions = new[] { "Standard project structure" }
            },
            AgentType.Planner => new
            {
                tasks = new[]
                {
                    new
                    {
                        id = "TASK-001",
                        story_id = "US-001",
                        title = "Implement core module",
                        description = "Create the main application module",
                        type = "feature",
                        files_to_create = new[] { "src/main.py" },
                        files_to_modify = Array.Empty<string>(),
                        dependencies = Array.Empty<string>(),
                        priority = "Must",
                        estimated_complexity = "M"
                    }
                },
                execution_order = new[] { new[] { "TASK-001" } },
                notes = "Mock planner output"
            },
            AgentType.Architect => new
            {
                architecture = new
                {
                    type = "cli-app",
                    language = "python",
                    framework = "none",
                    folder_structure = new Dictionary<string, string>
                    {
                        ["src/"] = "Source code",
                        ["tests/"] = "Test files",
                    }
                },
                scaffolding = new[]
                {
                    new { path = "src/__init__.py", content = "", description = "Package init" }
                },
                sprints = new[]
                {
                    new { id = "SPRINT-1", name = "Core", tasks = new[] { "TASK-001" }, goal = "Basic functionality" }
                }
            },
            AgentType.Coder => new
            {
                files = new[]
                {
                    new
                    {
                        path = "src/main.py",
                        action = "create",
                        content = "# Mock generated code\ndef main():\n    print('Hello from AutoNomX')\n\nif __name__ == '__main__':\n    main()\n",
                        description = "Main application entry point"
                    }
                },
                summary = "Created core module",
                needs_review = true
            },
            AgentType.Tester => new
            {
                test_files = new[]
                {
                    new
                    {
                        path = "tests/test_main.py",
                        content = "# Mock test\ndef test_main():\n    assert True\n",
                        description = "Basic test"
                    }
                },
                execution = new { command = "pytest tests/ -v", working_directory = ".", timeout_seconds = 60 },
                test_passed = true
            },
            AgentType.Reviewer => new
            {
                decision = "APPROVE",
                overall_score = 8,
                categories = new
                {
                    correctness = new { score = 8, notes = "Looks good" },
                    code_quality = new { score = 8, notes = "Clean code" },
                    security = new { score = 9, notes = "No issues" },
                    testing = new { score = 7, notes = "Basic coverage" },
                    architecture = new { score = 8, notes = "Good structure" }
                },
                issues = Array.Empty<object>(),
                feedback = "",
                notes = "Approved in dry-run mode"
            },
            _ => new { status = "ok", agent = agentType.ToString() }
        };
    }
}
