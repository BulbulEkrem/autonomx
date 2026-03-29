using AutoNomX.Application.Services;
using AutoNomX.Application.StateMachine;
using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AutoNomX.Tests;

/// <summary>
/// Integration-style tests for parallel worker execution.
/// Verifies that multiple workers can run concurrently and self-pick tasks.
/// </summary>
public class ParallelExecutionTests
{
    private readonly ICoderWorkerRepository _workerRepo = Substitute.For<ICoderWorkerRepository>();
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly IProjectFileRepository _fileRepo = Substitute.For<IProjectFileRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ILogger<WorkerPoolService> _poolLogger = Substitute.For<ILogger<WorkerPoolService>>();
    private readonly ILogger<TaskBoardService> _boardLogger = Substitute.For<ILogger<TaskBoardService>>();
    private readonly Guid _projectId = Guid.NewGuid();

    [Fact]
    public async Task ThreeWorkers_PickDifferentTasks_InParallel()
    {
        // Arrange: 3 workers, 5 tasks
        var workers = new List<CoderWorker>
        {
            CreateWorker("worker-a"),
            CreateWorker("worker-b"),
            CreateWorker("worker-c"),
        };

        var tasks = new List<TaskItem>
        {
            CreateTask("Task 1", TaskItemPriority.Must, ["src/api/a.cs"]),
            CreateTask("Task 2", TaskItemPriority.Must, ["src/api/b.cs"]),
            CreateTask("Task 3", TaskItemPriority.Should, ["src/cli/c.cs"]),
            CreateTask("Task 4", TaskItemPriority.Should, ["src/domain/d.cs"]),
            CreateTask("Task 5", TaskItemPriority.Could, ["src/infra/e.cs"]),
        };

        _workerRepo.GetIdleWorkersAsync(Arg.Any<CancellationToken>()).Returns(workers);
        _taskRepo.GetByProjectIdAsync(_projectId, Arg.Any<CancellationToken>()).Returns(tasks);
        _fileRepo.GetLockedFilesAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(new List<ProjectFile>());
        _fileRepo.TryAcquireLockAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var board = new TaskBoardService(_taskRepo, _workerRepo, _fileRepo, _unitOfWork, _mediator, _boardLogger);

        // Act: each worker self-picks
        var pickedTasks = new List<TaskItem?>();
        foreach (var worker in workers)
        {
            _workerRepo.GetByIdAsync(worker.Id, Arg.Any<CancellationToken>()).Returns(worker);
            var picked = await board.PickTaskForWorkerAsync(worker.Id, _projectId);
            pickedTasks.Add(picked);

            // Simulate that picked task is now assigned (status changes)
            if (picked is not null)
            {
                picked.Status = TaskItemStatus.InProgress;
                picked.AssignedWorker = worker.Id.ToString();
                worker.Status = WorkerStatus.Working;
            }
        }

        // Assert: all 3 workers got different tasks
        var pickedIds = pickedTasks.Where(t => t is not null).Select(t => t!.Id).ToList();
        Assert.Equal(3, pickedIds.Count);
        Assert.Equal(3, pickedIds.Distinct().Count()); // all unique
    }

    [Fact]
    public async Task WorkerPool_InitAndDynamicAdd()
    {
        var pool = new WorkerPoolService(_workerRepo, _unitOfWork, _poolLogger);

        _workerRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CoderWorker>());

        // Init with 2 workers
        await pool.InitializeFromConfigAsync([
            new WorkerTemplate(2, "qwen2.5-coder:32b", "ollama"),
        ]);

        await _workerRepo.Received(2).AddAsync(Arg.Any<CoderWorker>(), Arg.Any<CancellationToken>());

        // Now simulate 2 existing workers for dynamic add
        _workerRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CoderWorker>
            {
                CreateWorker("worker-a"),
                CreateWorker("worker-b"),
            });

        // Act: add a new worker at runtime
        var added = await pool.AddWorkerAsync("deepseek-coder:33b", "ollama");
        Assert.Equal("worker-c", added.Name);
        Assert.Equal("deepseek-coder:33b", added.Model);
    }

    [Fact]
    public async Task DryRun_ThreeWorkers_ParallelScenario()
    {
        // Simulate dry-run: 3 workers with tasks, verify assignment logic
        var workers = new List<CoderWorker>
        {
            CreateWorker("worker-a"),
            CreateWorker("worker-b"),
            CreateWorker("worker-c", model: "deepseek-coder:33b"),
        };

        // 6 tasks with varying priorities and dependencies
        var task1 = CreateTask("Setup API routes", TaskItemPriority.Must, ["src/api/routes.cs"]);
        var task2 = CreateTask("Create DB schema", TaskItemPriority.Must, ["src/infra/schema.cs"]);
        var task3 = CreateTask("Build CLI parser", TaskItemPriority.Should, ["src/cli/parser.cs"]);
        var task4 = CreateTask("Add API auth", TaskItemPriority.Should, ["src/api/auth.cs"]);
        task4.Dependencies = [task1.Id.ToString()]; // depends on task1
        var task5 = CreateTask("Write tests", TaskItemPriority.Could, ["tests/api.cs"]);
        var task6 = CreateTask("Add logging", TaskItemPriority.Could, ["src/infra/logging.cs"]);

        var allTasks = new List<TaskItem> { task1, task2, task3, task4, task5, task6 };

        _taskRepo.GetByProjectIdAsync(_projectId, Arg.Any<CancellationToken>()).Returns(allTasks);
        _fileRepo.GetLockedFilesAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(new List<ProjectFile>());
        _fileRepo.TryAcquireLockAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var board = new TaskBoardService(_taskRepo, _workerRepo, _fileRepo, _unitOfWork, _mediator, _boardLogger);

        // Round 1: 3 workers pick 3 tasks
        var round1 = new List<(string worker, string task)>();
        foreach (var worker in workers)
        {
            _workerRepo.GetByIdAsync(worker.Id, Arg.Any<CancellationToken>()).Returns(worker);
            var picked = await board.PickTaskForWorkerAsync(worker.Id, _projectId);
            if (picked is not null)
            {
                round1.Add((worker.Name, picked.Title));
                picked.Status = TaskItemStatus.InProgress;
                picked.AssignedWorker = worker.Id.ToString();
                worker.Status = WorkerStatus.Working;
            }
        }

        // Verify: Must priority tasks picked first, task4 NOT picked (dep unmet)
        Assert.Equal(3, round1.Count);
        Assert.Contains(round1, r => r.task == "Setup API routes");
        Assert.Contains(round1, r => r.task == "Create DB schema");
        Assert.DoesNotContain(round1, r => r.task == "Add API auth"); // blocked by dep

        // Simulate Round 1 completion: worker-a finishes task1
        task1.Status = TaskItemStatus.Done;
        workers[0].Status = WorkerStatus.Idle;
        workers[0].CurrentTaskId = null;

        // Now task4 should be pickable (dependency satisfied)
        _workerRepo.GetByIdAsync(workers[0].Id, Arg.Any<CancellationToken>()).Returns(workers[0]);
        var round2Pick = await board.PickTaskForWorkerAsync(workers[0].Id, _projectId);

        // task4 depends on task1 which is now Done → should be eligible
        // Worker-a was in api/ area, task4 is api/ → context affinity bonus
        Assert.NotNull(round2Pick);
    }

    // ── Helpers ─────────────────────────────────────────────────

    private CoderWorker CreateWorker(string name, WorkerStatus status = WorkerStatus.Idle, string model = "qwen2.5-coder:32b")
        => new() { Name = name, Model = model, Provider = "ollama", Status = status };

    private TaskItem CreateTask(string title, TaskItemPriority priority = TaskItemPriority.Should, List<string>? files = null)
        => new()
        {
            ProjectId = _projectId,
            Title = title,
            Status = TaskItemStatus.Ready,
            Priority = priority,
            FilesTouched = files ?? [],
        };
}
