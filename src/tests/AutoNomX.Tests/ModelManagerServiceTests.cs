using AutoNomX.Application.Services;
using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AutoNomX.Tests;

public class ModelManagerServiceTests
{
    private readonly IAgentGateway _agentGateway = Substitute.For<IAgentGateway>();
    private readonly ICoderWorkerRepository _workerRepo = Substitute.For<ICoderWorkerRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAgentMetricsRepository _metricsRepo = Substitute.For<IAgentMetricsRepository>();
    private readonly ModelManagerService _sut;

    public ModelManagerServiceTests()
    {
        var metricsLogger = Substitute.For<ILogger<MetricsService>>();
        var metricsService = new MetricsService(_metricsRepo, _workerRepo, _unitOfWork, metricsLogger);
        var logger = Substitute.For<ILogger<ModelManagerService>>();
        _sut = new ModelManagerService(_agentGateway, _workerRepo, _unitOfWork, metricsService, logger);
    }

    [Fact]
    public async Task RequestTaskAssignment_ReturnsDecisionFromAgent()
    {
        var workers = new List<CoderWorker>
        {
            CreateWorker("worker-a", "ollama/qwen:32b"),
            CreateWorker("worker-b", "ollama/deepseek:16b"),
        };

        var task = new TaskItem
        {
            ProjectId = Guid.NewGuid(),
            Title = "Build auth module",
            Description = "Implement OAuth2 login",
            Priority = TaskItemPriority.Must,
        };

        _metricsRepo.GetByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<AgentMetrics>());

        _agentGateway.RunAgentAsync(AgentType.ModelManager, Arg.Any<AgentExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(new AgentExecutionResult(
                ExecutionId: Guid.NewGuid().ToString(),
                Success: true,
                ResultJson: $"{{\"decision_type\":\"task_assignment\",\"assigned_to\":\"{workers[0].Name}\",\"model\":\"ollama/qwen:32b\",\"reasoning\":\"Best for auth tasks\"}}"));

        var decision = await _sut.RequestTaskAssignmentAsync(task, workers);

        Assert.Equal("worker-a", decision.WorkerName);
        Assert.Equal("ollama/qwen:32b", decision.Model);
        Assert.Contains("auth", decision.Reasoning);
    }

    [Fact]
    public async Task RequestTaskAssignment_FallsBackOnAgentFailure()
    {
        var workers = new List<CoderWorker>
        {
            CreateWorker("worker-a", "ollama/model"),
        };

        var task = new TaskItem
        {
            ProjectId = Guid.NewGuid(),
            Title = "Test task",
        };

        _metricsRepo.GetByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<AgentMetrics>());

        _agentGateway.RunAgentAsync(AgentType.ModelManager, Arg.Any<AgentExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(new AgentExecutionResult(
                ExecutionId: Guid.NewGuid().ToString(),
                Success: false,
                ResultJson: "",
                Error: "Agent unavailable"));

        var decision = await _sut.RequestTaskAssignmentAsync(task, workers);

        Assert.Equal("worker-a", decision.WorkerName);
        Assert.Contains("Default", decision.Reasoning);
    }

    [Fact]
    public async Task HandleFailure_ReturnsNullBelowThreshold()
    {
        var result = await _sut.HandleFailureAsync(Guid.NewGuid(), Guid.NewGuid(), 1);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleFailure_RequestsEscalationAtThreshold()
    {
        var worker = CreateWorker("worker-a", "ollama/codellama:13b");
        _workerRepo.GetByIdAsync(worker.Id, Arg.Any<CancellationToken>()).Returns(worker);

        _agentGateway.RunAgentAsync(AgentType.ModelManager, Arg.Any<AgentExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(new AgentExecutionResult(
                ExecutionId: Guid.NewGuid().ToString(),
                Success: true,
                ResultJson: "{\"decision_type\":\"model_switch\",\"new_model\":\"ollama/qwen2.5-coder:32b\",\"reason\":\"Upgrading after 2 failures\",\"escalation_step\":1}"));

        var decision = await _sut.HandleFailureAsync(worker.Id, Guid.NewGuid(), 2);

        Assert.NotNull(decision);
        Assert.Equal("ollama/qwen2.5-coder:32b", decision!.NewModel);
        Assert.Equal("ollama/codellama:13b", decision.OldModel);
        Assert.Equal(1, decision.EscalationStep);
    }

    [Fact]
    public async Task HandleFailure_AppliesDefaultEscalationOnAgentFailure()
    {
        var worker = CreateWorker("worker-a", "ollama/codellama:13b");
        _workerRepo.GetByIdAsync(worker.Id, Arg.Any<CancellationToken>()).Returns(worker);

        _agentGateway.RunAgentAsync(AgentType.ModelManager, Arg.Any<AgentExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(new AgentExecutionResult(
                ExecutionId: Guid.NewGuid().ToString(),
                Success: false,
                ResultJson: "",
                Error: "Agent failed"));

        var decision = await _sut.HandleFailureAsync(worker.Id, Guid.NewGuid(), 2);

        Assert.NotNull(decision);
        Assert.Equal("ollama/qwen2.5-coder:32b", decision!.NewModel);
    }

    [Fact]
    public async Task UpdateWorkerModel_ChangesModel()
    {
        var worker = CreateWorker("worker-a", "ollama/old-model");
        _workerRepo.GetByIdAsync(worker.Id, Arg.Any<CancellationToken>()).Returns(worker);

        await _sut.UpdateWorkerModelAsync(worker.Id, "ollama/new-model");

        Assert.Equal("ollama/new-model", worker.Model);
        await _workerRepo.Received(1).UpdateAsync(worker, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static CoderWorker CreateWorker(string name, string model) => new()
    {
        Name = name,
        Model = model,
        Provider = "ollama",
        Status = WorkerStatus.Idle,
    };
}
