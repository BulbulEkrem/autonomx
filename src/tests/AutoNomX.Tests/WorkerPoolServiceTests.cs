using AutoNomX.Application.Services;
using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AutoNomX.Tests;

public class WorkerPoolServiceTests
{
    private readonly ICoderWorkerRepository _workerRepo = Substitute.For<ICoderWorkerRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ILogger<WorkerPoolService> _logger = Substitute.For<ILogger<WorkerPoolService>>();
    private readonly WorkerPoolService _sut;

    public WorkerPoolServiceTests()
    {
        _sut = new WorkerPoolService(_workerRepo, _unitOfWork, _mediator, _logger);
    }

    [Fact]
    public async Task InitializeFromConfigAsync_CreatesCorrectWorkers()
    {
        // Arrange: 2x qwen + 1x deepseek = 3 workers
        _workerRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CoderWorker>());

        var templates = new[]
        {
            new WorkerTemplate(2, "qwen2.5-coder:32b", "ollama"),
            new WorkerTemplate(1, "deepseek-coder:33b", "lm_studio"),
        };

        // Act
        await _sut.InitializeFromConfigAsync(templates);

        // Assert: 3 workers created
        await _workerRepo.Received(3).AddAsync(Arg.Any<CoderWorker>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        // Verify names: worker-a, worker-b, worker-c
        await _workerRepo.Received(1).AddAsync(
            Arg.Is<CoderWorker>(w => w.Name == "worker-a" && w.Model == "qwen2.5-coder:32b"),
            Arg.Any<CancellationToken>());
        await _workerRepo.Received(1).AddAsync(
            Arg.Is<CoderWorker>(w => w.Name == "worker-b" && w.Model == "qwen2.5-coder:32b"),
            Arg.Any<CancellationToken>());
        await _workerRepo.Received(1).AddAsync(
            Arg.Is<CoderWorker>(w => w.Name == "worker-c" && w.Model == "deepseek-coder:33b"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeFromConfigAsync_SkipsIfWorkersExist()
    {
        _workerRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CoderWorker> { CreateWorker("worker-a") });

        await _sut.InitializeFromConfigAsync([new WorkerTemplate(2, "model", "provider")]);

        await _workerRepo.DidNotReceive().AddAsync(Arg.Any<CoderWorker>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddWorkerAsync_CreatesWorkerWithAutoName()
    {
        _workerRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CoderWorker>
            {
                CreateWorker("worker-a"),
                CreateWorker("worker-b"),
            });

        var worker = await _sut.AddWorkerAsync("codellama:34b", "ollama");

        Assert.Equal("worker-c", worker.Name);
        Assert.Equal("codellama:34b", worker.Model);
        await _workerRepo.Received(1).AddAsync(Arg.Any<CoderWorker>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveWorkerAsync_IdleWorker_RemovesImmediately()
    {
        var worker = CreateWorker("worker-a");
        _workerRepo.GetByIdAsync(worker.Id, Arg.Any<CancellationToken>())
            .Returns(worker);

        var result = await _sut.RemoveWorkerAsync(worker.Id);

        Assert.True(result);
        await _workerRepo.Received(1).RemoveAsync(worker.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveWorkerAsync_BusyWorker_MarksOffline()
    {
        var worker = CreateWorker("worker-a");
        worker.Status = WorkerStatus.Working;
        worker.CurrentTaskId = Guid.NewGuid();
        _workerRepo.GetByIdAsync(worker.Id, Arg.Any<CancellationToken>())
            .Returns(worker);

        var result = await _sut.RemoveWorkerAsync(worker.Id);

        Assert.False(result);
        Assert.Equal(WorkerStatus.Offline, worker.Status);
        await _workerRepo.Received(1).UpdateAsync(worker, Arg.Any<CancellationToken>());
        await _workerRepo.DidNotReceive().RemoveAsync(worker.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveWorkerAsync_BusyWorkerForce_RemovesImmediately()
    {
        var worker = CreateWorker("worker-a");
        worker.Status = WorkerStatus.Working;
        _workerRepo.GetByIdAsync(worker.Id, Arg.Any<CancellationToken>())
            .Returns(worker);

        var result = await _sut.RemoveWorkerAsync(worker.Id, force: true);

        Assert.True(result);
        await _workerRepo.Received(1).RemoveAsync(worker.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPoolStatusAsync_ReturnsCorrectCounts()
    {
        var workers = new List<CoderWorker>
        {
            CreateWorker("a", WorkerStatus.Idle),
            CreateWorker("b", WorkerStatus.Working),
            CreateWorker("c", WorkerStatus.Working),
            CreateWorker("d", WorkerStatus.Offline),
        };
        _workerRepo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(workers);

        var status = await _sut.GetPoolStatusAsync();

        Assert.Equal(4, status.TotalWorkers);
        Assert.Equal(1, status.IdleCount);
        Assert.Equal(2, status.WorkingCount);
        Assert.Equal(1, status.OfflineCount);
    }

    [Fact]
    public async Task CleanupOfflineWorkersAsync_RemovesOfflineWithNoTask()
    {
        var offlineNoTask = CreateWorker("x", WorkerStatus.Offline);
        var offlineWithTask = CreateWorker("y", WorkerStatus.Offline);
        offlineWithTask.CurrentTaskId = Guid.NewGuid();
        var idle = CreateWorker("z", WorkerStatus.Idle);

        _workerRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CoderWorker> { offlineNoTask, offlineWithTask, idle });

        await _sut.CleanupOfflineWorkersAsync();

        await _workerRepo.Received(1).RemoveAsync(offlineNoTask.Id, Arg.Any<CancellationToken>());
        await _workerRepo.DidNotReceive().RemoveAsync(offlineWithTask.Id, Arg.Any<CancellationToken>());
        await _workerRepo.DidNotReceive().RemoveAsync(idle.Id, Arg.Any<CancellationToken>());
    }

    private static CoderWorker CreateWorker(string name, WorkerStatus status = WorkerStatus.Idle)
        => new() { Name = name, Model = "test-model", Provider = "ollama", Status = status };
}
