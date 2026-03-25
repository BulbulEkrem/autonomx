using AutoNomX.Application.Services;
using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AutoNomX.Tests;

public class TaskBoardSelfPickTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ICoderWorkerRepository _workerRepo = Substitute.For<ICoderWorkerRepository>();
    private readonly IProjectFileRepository _fileRepo = Substitute.For<IProjectFileRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ILogger<TaskBoardService> _logger = Substitute.For<ILogger<TaskBoardService>>();
    private readonly TaskBoardService _sut;
    private readonly Guid _projectId = Guid.NewGuid();

    public TaskBoardSelfPickTests()
    {
        _sut = new TaskBoardService(_taskRepo, _workerRepo, _fileRepo, _unitOfWork, _mediator, _logger);
    }

    [Fact]
    public async Task PickTaskForWorkerAsync_PrefersHigherPriority()
    {
        var worker = CreateIdleWorker();
        _workerRepo.GetByIdAsync(worker.Id, Arg.Any<CancellationToken>()).Returns(worker);

        var mustTask = CreateTask("Must task", TaskItemPriority.Must);
        var shouldTask = CreateTask("Should task", TaskItemPriority.Should);
        var couldTask = CreateTask("Could task", TaskItemPriority.Could);

        SetupTasks([mustTask, shouldTask, couldTask]);
        SetupLockSuccess();

        var picked = await _sut.PickTaskForWorkerAsync(worker.Id, _projectId);

        Assert.NotNull(picked);
        Assert.Equal("Must task", picked.Title);
    }

    [Fact]
    public async Task PickTaskForWorkerAsync_SkipsTasksWithUnmetDependencies()
    {
        var worker = CreateIdleWorker();
        _workerRepo.GetByIdAsync(worker.Id, Arg.Any<CancellationToken>()).Returns(worker);

        var depTask = CreateTask("Dependency");
        depTask.Status = TaskItemStatus.InProgress; // Not done yet

        var blockedTask = CreateTask("Blocked", TaskItemPriority.Must);
        blockedTask.Dependencies = [depTask.Id.ToString()];

        var readyTask = CreateTask("Ready", TaskItemPriority.Should);

        SetupTasks([depTask, blockedTask, readyTask]);
        SetupLockSuccess();

        var picked = await _sut.PickTaskForWorkerAsync(worker.Id, _projectId);

        Assert.NotNull(picked);
        Assert.Equal("Ready", picked.Title);
    }

    [Fact]
    public async Task PickTaskForWorkerAsync_SkipsTasksWithFileConflicts()
    {
        var worker = CreateIdleWorker();
        _workerRepo.GetByIdAsync(worker.Id, Arg.Any<CancellationToken>()).Returns(worker);

        var conflictTask = CreateTask("Conflict", TaskItemPriority.Must);
        conflictTask.FilesTouched = ["src/file.cs"];

        var safeTask = CreateTask("Safe", TaskItemPriority.Should);
        safeTask.FilesTouched = ["src/other.cs"];

        SetupTasks([conflictTask, safeTask]);

        // file.cs is locked
        _fileRepo.GetLockedFilesAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(new List<ProjectFile> { new() { Path = "src/file.cs", ProjectId = _projectId } });
        SetupLockSuccess();

        var picked = await _sut.PickTaskForWorkerAsync(worker.Id, _projectId);

        Assert.NotNull(picked);
        Assert.Equal("Safe", picked.Title);
    }

    [Fact]
    public async Task PickTaskForWorkerAsync_PrefersContextAffinity()
    {
        var worker = CreateIdleWorker();
        _workerRepo.GetByIdAsync(worker.Id, Arg.Any<CancellationToken>()).Returns(worker);

        // Worker previously completed a task touching src/api/ files
        var previousTask = CreateTask("Previous");
        previousTask.AssignedWorker = worker.Id.ToString();
        previousTask.Status = TaskItemStatus.Done;
        previousTask.UpdatedAt = DateTime.UtcNow;
        previousTask.FilesTouched = ["src/api/Controller.cs"];

        // Two equally prioritized ready tasks
        var apiTask = CreateTask("API task", TaskItemPriority.Should);
        apiTask.FilesTouched = ["src/api/Service.cs"]; // Same directory = affinity bonus

        var cliTask = CreateTask("CLI task", TaskItemPriority.Should);
        cliTask.FilesTouched = ["src/cli/Program.cs"];

        _taskRepo.GetByProjectIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(new List<TaskItem> { previousTask, apiTask, cliTask });

        _fileRepo.GetLockedFilesAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(new List<ProjectFile>());
        SetupLockSuccess();

        var picked = await _sut.PickTaskForWorkerAsync(worker.Id, _projectId);

        Assert.NotNull(picked);
        Assert.Equal("API task", picked.Title);
    }

    [Fact]
    public async Task PickTaskForWorkerAsync_ReturnsNullForBusyWorker()
    {
        var worker = CreateIdleWorker();
        worker.Status = WorkerStatus.Working;
        _workerRepo.GetByIdAsync(worker.Id, Arg.Any<CancellationToken>()).Returns(worker);

        var result = await _sut.PickTaskForWorkerAsync(worker.Id, _projectId);

        Assert.Null(result);
    }

    [Fact]
    public async Task IsCodingPhaseCompleteAsync_TrueWhenAllDoneOrFailed()
    {
        var done = CreateTask("Done task");
        done.Status = TaskItemStatus.Done;
        var failed = CreateTask("Failed task");
        failed.Status = TaskItemStatus.Failed;

        _taskRepo.GetByProjectIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(new List<TaskItem> { done, failed });

        var result = await _sut.IsCodingPhaseCompleteAsync(_projectId);

        Assert.True(result);
    }

    [Fact]
    public async Task IsCodingPhaseCompleteAsync_FalseWhenInProgress()
    {
        var done = CreateTask("Done");
        done.Status = TaskItemStatus.Done;
        var inProgress = CreateTask("InProgress");
        inProgress.Status = TaskItemStatus.InProgress;

        _taskRepo.GetByProjectIdAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(new List<TaskItem> { done, inProgress });

        var result = await _sut.IsCodingPhaseCompleteAsync(_projectId);

        Assert.False(result);
    }

    // ── File Locking Tests ──────────────────────────────────────

    [Fact]
    public async Task LockFilesAsync_LocksAllFiles()
    {
        _fileRepo.TryAcquireLockAsync(_projectId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _sut.LockFilesAsync(_projectId, Guid.NewGuid(),
            ["src/a.cs", "src/b.cs"], "worker-1");

        Assert.True(result);
        await _fileRepo.Received(2).TryAcquireLockAsync(
            _projectId, Arg.Any<string>(), "worker-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LockFilesAsync_RollsBackOnConflict()
    {
        _fileRepo.TryAcquireLockAsync(_projectId, "src/a.cs", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _fileRepo.TryAcquireLockAsync(_projectId, "src/b.cs", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false); // conflict on second file

        var result = await _sut.LockFilesAsync(_projectId, Guid.NewGuid(),
            ["src/a.cs", "src/b.cs"], "worker-1");

        Assert.False(result);
        // Should have released the first lock
        await _fileRepo.Received(1).ReleaseLockAsync(_projectId, "src/a.cs", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IsFileLockedAsync_ReturnsTrueWhenLocked()
    {
        _fileRepo.GetByPathAsync(_projectId, "src/file.cs", Arg.Any<CancellationToken>())
            .Returns(new ProjectFile { Path = "src/file.cs", ProjectId = _projectId, LockedByWorker = "worker-a" });

        var locked = await _sut.IsFileLockedAsync(_projectId, "src/file.cs");

        Assert.True(locked);
    }

    [Fact]
    public async Task IsFileLockedAsync_ReturnsFalseWhenNotLocked()
    {
        _fileRepo.GetByPathAsync(_projectId, "src/file.cs", Arg.Any<CancellationToken>())
            .Returns(new ProjectFile { Path = "src/file.cs", ProjectId = _projectId, LockedByWorker = null });

        var locked = await _sut.IsFileLockedAsync(_projectId, "src/file.cs");

        Assert.False(locked);
    }

    // ── Helpers ─────────────────────────────────────────────────

    private CoderWorker CreateIdleWorker() => new()
    {
        Name = "worker-a",
        Model = "test-model",
        Provider = "ollama",
        Status = WorkerStatus.Idle,
    };

    private TaskItem CreateTask(string title, TaskItemPriority priority = TaskItemPriority.Should)
        => new()
        {
            ProjectId = _projectId,
            Title = title,
            Status = TaskItemStatus.Ready,
            Priority = priority,
        };

    private void SetupTasks(List<TaskItem> tasks)
    {
        _taskRepo.GetByProjectIdAsync(_projectId, Arg.Any<CancellationToken>()).Returns(tasks);
        _fileRepo.GetLockedFilesAsync(_projectId, Arg.Any<CancellationToken>())
            .Returns(new List<ProjectFile>());
    }

    private void SetupLockSuccess()
    {
        _fileRepo.TryAcquireLockAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }
}
