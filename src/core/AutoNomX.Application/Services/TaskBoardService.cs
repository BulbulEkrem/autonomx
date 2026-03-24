using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Events;
using AutoNomX.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AutoNomX.Application.Services;

/// <summary>
/// Kanban-style task board for managing tasks within a pipeline.
/// Handles task assignment, dependency resolution, and file locking.
/// </summary>
public class TaskBoardService(
    ITaskRepository taskRepo,
    ICoderWorkerRepository workerRepo,
    IProjectFileRepository fileRepo,
    IUnitOfWork unitOfWork,
    IMediator mediator,
    ILogger<TaskBoardService> logger)
{
    /// <summary>Initialize the board with tasks from the planner output.</summary>
    public async Task InitializeBoardAsync(
        Guid projectId,
        IEnumerable<TaskItem> tasks,
        CancellationToken ct = default)
    {
        foreach (var task in tasks)
        {
            task.ProjectId = projectId;
            task.Status = TaskItemStatus.Ready;
            await taskRepo.AddAsync(task, ct);
        }
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("TaskBoard initialized with {Count} tasks for project {ProjectId}",
            tasks.Count(), projectId);
    }

    /// <summary>
    /// Get tasks that are ready to be picked up:
    /// - Status = Ready
    /// - All dependencies completed
    /// - No file lock conflicts
    /// </summary>
    public async Task<IReadOnlyList<TaskItem>> GetReadyTasksAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        var allTasks = await taskRepo.GetByProjectIdAsync(projectId, ct);
        var completedTaskIds = allTasks
            .Where(t => t.Status == TaskItemStatus.Done)
            .Select(t => t.Id.ToString())
            .ToHashSet();

        var lockedFiles = await fileRepo.GetLockedFilesAsync(projectId, ct);
        var lockedPaths = lockedFiles.Select(f => f.Path).ToHashSet();

        var readyTasks = allTasks
            .Where(t => t.Status == TaskItemStatus.Ready)
            .Where(t => t.Dependencies.All(dep => completedTaskIds.Contains(dep)))
            .Where(t => !t.FilesTouched.Any(f => lockedPaths.Contains(f)))
            .ToList();

        return readyTasks;
    }

    /// <summary>Assign a task to a worker. Locks files and updates status.</summary>
    public async Task<bool> AssignTaskAsync(
        Guid taskId,
        Guid workerId,
        CancellationToken ct = default)
    {
        var task = await taskRepo.GetByIdAsync(taskId, ct);
        if (task is null || task.Status != TaskItemStatus.Ready)
            return false;

        var worker = await workerRepo.GetByIdAsync(workerId, ct);
        if (worker is null || worker.Status != WorkerStatus.Idle)
            return false;

        // Try to acquire file locks
        foreach (var file in task.FilesTouched)
        {
            var locked = await fileRepo.TryAcquireLockAsync(
                task.ProjectId, file, workerId.ToString(), ct);
            if (!locked)
            {
                logger.LogWarning("File lock failed for {File}, rolling back assignment", file);
                // Release any locks we already acquired
                foreach (var acquired in task.FilesTouched.TakeWhile(f => f != file))
                    await fileRepo.ReleaseLockAsync(task.ProjectId, acquired, ct);
                return false;
            }
        }

        // Update task
        var oldStatus = task.Status;
        task.Status = TaskItemStatus.InProgress;
        task.AssignedWorker = workerId.ToString();
        task.GitBranch = $"task/{task.Id.ToString()[..8]}";
        await taskRepo.UpdateAsync(task, ct);

        // Update worker
        var oldWorkerStatus = worker.Status;
        worker.Status = WorkerStatus.Working;
        worker.CurrentTaskId = taskId;
        await workerRepo.UpdateAsync(worker, ct);

        await unitOfWork.SaveChangesAsync(ct);

        // Publish events
        await mediator.Publish(new TaskStatusChangedEvent(
            taskId, task.ProjectId, oldStatus, task.Status, workerId.ToString()), ct);
        await mediator.Publish(new WorkerStatusChangedEvent(
            workerId, oldWorkerStatus, worker.Status, taskId), ct);

        logger.LogInformation("Task {TaskId} assigned to worker {WorkerId}", taskId, workerId);
        return true;
    }

    /// <summary>Mark a task as completed. Release file locks and free worker.</summary>
    public async Task CompleteTaskAsync(
        Guid taskId,
        string resultJson,
        CancellationToken ct = default)
    {
        var task = await taskRepo.GetByIdAsync(taskId, ct);
        if (task is null) return;

        var oldStatus = task.Status;
        task.Status = TaskItemStatus.Done;
        await taskRepo.UpdateAsync(task, ct);

        // Release file locks
        foreach (var file in task.FilesTouched)
            await fileRepo.ReleaseLockAsync(task.ProjectId, file, ct);

        // Free worker
        if (task.AssignedWorker is not null && Guid.TryParse(task.AssignedWorker, out var workerId))
        {
            var worker = await workerRepo.GetByIdAsync(workerId, ct);
            if (worker is not null)
            {
                worker.Status = WorkerStatus.Idle;
                worker.CurrentTaskId = null;
                await workerRepo.UpdateAsync(worker, ct);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);

        await mediator.Publish(new TaskStatusChangedEvent(
            taskId, task.ProjectId, oldStatus, task.Status, task.AssignedWorker), ct);

        logger.LogInformation("Task {TaskId} completed", taskId);
    }

    /// <summary>Mark a task as failed. Increment retry count.</summary>
    public async Task FailTaskAsync(
        Guid taskId,
        string error,
        CancellationToken ct = default)
    {
        var task = await taskRepo.GetByIdAsync(taskId, ct);
        if (task is null) return;

        task.RetryCount++;
        var oldStatus = task.Status;

        if (task.RetryCount >= task.MaxRetries)
        {
            task.Status = TaskItemStatus.Failed;
            logger.LogError("Task {TaskId} permanently failed after {Retries} retries: {Error}",
                taskId, task.RetryCount, error);
        }
        else
        {
            task.Status = TaskItemStatus.Ready; // Reset to ready for retry
            task.AssignedWorker = null;
            logger.LogWarning("Task {TaskId} failed (attempt {Retry}/{Max}): {Error}",
                taskId, task.RetryCount, task.MaxRetries, error);
        }

        // Release file locks
        foreach (var file in task.FilesTouched)
            await fileRepo.ReleaseLockAsync(task.ProjectId, file, ct);

        // Free worker
        if (task.AssignedWorker is not null && Guid.TryParse(task.AssignedWorker, out var workerId))
        {
            var worker = await workerRepo.GetByIdAsync(workerId, ct);
            if (worker is not null)
            {
                worker.Status = WorkerStatus.Idle;
                worker.CurrentTaskId = null;
                await workerRepo.UpdateAsync(worker, ct);
            }
        }

        await taskRepo.UpdateAsync(task, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await mediator.Publish(new TaskStatusChangedEvent(
            taskId, task.ProjectId, oldStatus, task.Status, task.AssignedWorker), ct);
    }

    /// <summary>Set a task to Revision status (reviewer rejected).</summary>
    public async Task RequestRevisionAsync(
        Guid taskId,
        string feedback,
        CancellationToken ct = default)
    {
        var task = await taskRepo.GetByIdAsync(taskId, ct);
        if (task is null) return;

        var oldStatus = task.Status;
        task.Status = TaskItemStatus.Revision;
        await taskRepo.UpdateAsync(task, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await mediator.Publish(new TaskStatusChangedEvent(
            taskId, task.ProjectId, oldStatus, task.Status, task.AssignedWorker), ct);
    }

    /// <summary>Get board status summary for a project.</summary>
    public async Task<BoardStatus> GetBoardStatusAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        var tasks = await taskRepo.GetByProjectIdAsync(projectId, ct);

        return new BoardStatus(
            ProjectId: projectId,
            TotalTasks: tasks.Count,
            ReadyCount: tasks.Count(t => t.Status == TaskItemStatus.Ready),
            InProgressCount: tasks.Count(t => t.Status == TaskItemStatus.InProgress),
            TestingCount: tasks.Count(t => t.Status == TaskItemStatus.Testing),
            ReviewCount: tasks.Count(t => t.Status == TaskItemStatus.Review),
            DoneCount: tasks.Count(t => t.Status == TaskItemStatus.Done),
            FailedCount: tasks.Count(t => t.Status == TaskItemStatus.Failed),
            RevisionCount: tasks.Count(t => t.Status == TaskItemStatus.Revision));
    }

    /// <summary>Check if all tasks for a project are completed.</summary>
    public async Task<bool> AreAllTasksCompletedAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        var tasks = await taskRepo.GetByProjectIdAsync(projectId, ct);
        return tasks.Count > 0 && tasks.All(t => t.Status == TaskItemStatus.Done);
    }

    /// <summary>Check if all coding tasks are either Done or Failed (no InProgress/Ready).</summary>
    public async Task<bool> IsCodingPhaseCompleteAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        var tasks = await taskRepo.GetByProjectIdAsync(projectId, ct);
        if (tasks.Count == 0) return true;

        return tasks.All(t =>
            t.Status is TaskItemStatus.Done or TaskItemStatus.Failed);
    }

    // ── Self-Pick Task Selection ────────────────────────────────

    /// <summary>
    /// Pick the best available task for a specific worker using smart selection:
    /// 1. Dependencies satisfied (all deps are Done)
    /// 2. No file lock conflicts
    /// 3. Context affinity (same area as worker's last task)
    /// 4. Priority ordering: Must > Should > Could
    /// </summary>
    public async Task<TaskItem?> PickTaskForWorkerAsync(
        Guid workerId,
        Guid projectId,
        CancellationToken ct = default)
    {
        var worker = await workerRepo.GetByIdAsync(workerId, ct);
        if (worker is null || worker.Status != WorkerStatus.Idle)
            return null;

        var readyTasks = await GetReadyTasksAsync(projectId, ct);
        if (readyTasks.Count == 0)
            return null;

        // Get worker's last completed task for context affinity
        var allTasks = await taskRepo.GetByProjectIdAsync(projectId, ct);
        var lastWorkerTask = allTasks
            .Where(t => t.AssignedWorker == workerId.ToString() && t.Status == TaskItemStatus.Done)
            .MaxBy(t => t.UpdatedAt);

        var lastFiles = lastWorkerTask?.FilesTouched.ToHashSet() ?? [];

        // Score and sort tasks
        var scored = readyTasks
            .Select(task =>
            {
                var score = 0;

                // Priority bonus
                score += task.Priority switch
                {
                    TaskItemPriority.Must => 300,
                    TaskItemPriority.Should => 200,
                    TaskItemPriority.Could => 100,
                    _ => 0,
                };

                // Context affinity: bonus if task touches same files/dirs as worker's last task
                if (lastFiles.Count > 0 && task.FilesTouched.Count > 0)
                {
                    var overlap = task.FilesTouched.Count(f =>
                        lastFiles.Any(lf => SharesDirectory(f, lf)));
                    score += overlap * 50;
                }

                return (task, score);
            })
            .OrderByDescending(x => x.score)
            .ToList();

        var bestTask = scored.FirstOrDefault().task;
        if (bestTask is null)
            return null;

        // Attempt assignment
        var assigned = await AssignTaskAsync(bestTask.Id, workerId, ct);
        return assigned ? bestTask : null;
    }

    // ── File Locking ────────────────────────────────────────────

    /// <summary>Lock specific files for a task.</summary>
    public async Task<bool> LockFilesAsync(
        Guid projectId,
        Guid taskId,
        IEnumerable<string> files,
        string workerId,
        CancellationToken ct = default)
    {
        var filesToLock = files.ToList();
        var locked = new List<string>();

        foreach (var file in filesToLock)
        {
            var success = await fileRepo.TryAcquireLockAsync(projectId, file, workerId, ct);
            if (!success)
            {
                logger.LogWarning("File lock conflict: {File} (task={TaskId})", file, taskId);
                // Rollback
                foreach (var acquired in locked)
                    await fileRepo.ReleaseLockAsync(projectId, acquired, ct);
                return false;
            }
            locked.Add(file);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Unlock all files held by a task.</summary>
    public async Task UnlockFilesAsync(
        Guid projectId,
        Guid taskId,
        CancellationToken ct = default)
    {
        var task = await taskRepo.GetByIdAsync(taskId, ct);
        if (task is null) return;

        foreach (var file in task.FilesTouched)
            await fileRepo.ReleaseLockAsync(projectId, file, ct);

        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Check if a specific file is locked.</summary>
    public async Task<bool> IsFileLockedAsync(
        Guid projectId,
        string filePath,
        CancellationToken ct = default)
    {
        var file = await fileRepo.GetByPathAsync(projectId, filePath, ct);
        return file?.LockedByWorker is not null;
    }

    /// <summary>Get all locked files for a project.</summary>
    public async Task<IReadOnlyList<ProjectFile>> GetLockedFilesAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        return await fileRepo.GetLockedFilesAsync(projectId, ct);
    }

    // ── Private helpers ─────────────────────────────────────────

    private static bool SharesDirectory(string path1, string path2)
    {
        var dir1 = Path.GetDirectoryName(path1) ?? "";
        var dir2 = Path.GetDirectoryName(path2) ?? "";
        return dir1.Equals(dir2, StringComparison.OrdinalIgnoreCase) && dir1 != "";
    }
}

public record BoardStatus(
    Guid ProjectId,
    int TotalTasks,
    int ReadyCount,
    int InProgressCount,
    int TestingCount,
    int ReviewCount,
    int DoneCount,
    int FailedCount,
    int RevisionCount);
