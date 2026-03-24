using AutoNomX.Application.Services;
using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AutoNomX.Tests;

public class MetricsServiceTests
{
    private readonly IAgentMetricsRepository _metricsRepo = Substitute.For<IAgentMetricsRepository>();
    private readonly ICoderWorkerRepository _workerRepo = Substitute.For<ICoderWorkerRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<MetricsService> _logger = Substitute.For<ILogger<MetricsService>>();
    private readonly MetricsService _sut;

    public MetricsServiceTests()
    {
        _sut = new MetricsService(_metricsRepo, _workerRepo, _unitOfWork, _logger);
    }

    [Fact]
    public async Task RecordTaskCompletion_CreatesNewMetrics()
    {
        var agentId = Guid.NewGuid();
        _metricsRepo.GetByAgentAndModelAsync(agentId, "test-model", Arg.Any<CancellationToken>())
            .Returns((AgentMetrics?)null);

        await _sut.RecordTaskCompletionAsync(agentId, "test-model", 3, 1500, 12.5, true);

        await _metricsRepo.Received(1).AddOrUpdateAsync(
            Arg.Is<AgentMetrics>(m =>
                m.AgentId == agentId &&
                m.ModelUsed == "test-model" &&
                m.TotalExecutions == 1 &&
                m.SuccessCount == 1 &&
                m.FailureCount == 0 &&
                m.TotalTokensUsed == 1500),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordTaskCompletion_UpdatesExistingMetrics()
    {
        var agentId = Guid.NewGuid();
        var existing = new AgentMetrics
        {
            AgentId = agentId,
            ModelUsed = "test-model",
            TotalExecutions = 5,
            SuccessCount = 4,
            FailureCount = 1,
            AvgIterations = 2.5,
            TotalTokensUsed = 5000,
            AvgDurationSeconds = 10.0,
            AvgScore = 7.0,
        };

        _metricsRepo.GetByAgentAndModelAsync(agentId, "test-model", Arg.Any<CancellationToken>())
            .Returns(existing);

        await _sut.RecordTaskCompletionAsync(agentId, "test-model", 3, 1000, 15.0, false);

        Assert.Equal(6, existing.TotalExecutions);
        Assert.Equal(4, existing.SuccessCount);
        Assert.Equal(2, existing.FailureCount);
        Assert.Equal(6000, existing.TotalTokensUsed);
    }

    [Fact]
    public async Task RecordTaskCompletion_WithReviewScores()
    {
        var agentId = Guid.NewGuid();
        _metricsRepo.GetByAgentAndModelAsync(agentId, "model", Arg.Any<CancellationToken>())
            .Returns((AgentMetrics?)null);

        var scores = new ReviewScores(8.0, 7.5, 9.0, 7.0, 8.5);

        await _sut.RecordTaskCompletionAsync(agentId, "model", 2, 1000, 10.0, true, scores);

        await _metricsRepo.Received(1).AddOrUpdateAsync(
            Arg.Is<AgentMetrics>(m => m.AvgScore == 8.0), // (8+7.5+9+7+8.5)/5 = 8.0
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetWorkerPerformance_ReturnsCorrectStats()
    {
        var workerId = Guid.NewGuid();
        var worker = new CoderWorker
        {
            Id = workerId,
            Name = "worker-a",
            Model = "qwen:32b",
            Provider = "ollama",
        };
        _workerRepo.GetByIdAsync(workerId, Arg.Any<CancellationToken>()).Returns(worker);

        var metrics = new List<AgentMetrics>
        {
            new()
            {
                AgentId = workerId,
                ModelUsed = "qwen:32b",
                TotalExecutions = 10,
                SuccessCount = 8,
                FailureCount = 2,
                AvgIterations = 2.5,
                TotalTokensUsed = 15000,
                AvgScore = 7.5,
            },
        };
        _metricsRepo.GetByAgentIdAsync(workerId, Arg.Any<CancellationToken>()).Returns(metrics);

        var perf = await _sut.GetWorkerPerformanceAsync(workerId);

        Assert.Equal("worker-a", perf.WorkerName);
        Assert.Equal(10, perf.TotalTasks);
        Assert.Equal(0.8, perf.SuccessRate);
        Assert.Equal(7.5, perf.AvgScore);
    }

    [Fact]
    public async Task GetWorkerPerformance_ReturnsZerosForNewWorker()
    {
        var workerId = Guid.NewGuid();
        var worker = new CoderWorker
        {
            Id = workerId,
            Name = "worker-new",
            Model = "model",
            Provider = "ollama",
        };
        _workerRepo.GetByIdAsync(workerId, Arg.Any<CancellationToken>()).Returns(worker);
        _metricsRepo.GetByAgentIdAsync(workerId, Arg.Any<CancellationToken>())
            .Returns(new List<AgentMetrics>());

        var perf = await _sut.GetWorkerPerformanceAsync(workerId);

        Assert.Equal(0, perf.TotalTasks);
        Assert.Equal(0, perf.SuccessRate);
    }
}
