using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Events;
using AutoNomX.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoNomX.Application.Services;

/// <summary>
/// Manages the dynamic coder worker pool.
/// Handles worker lifecycle, initialization from config, and runtime add/remove.
/// </summary>
public class WorkerPoolService(
    ICoderWorkerRepository workerRepo,
    IUnitOfWork unitOfWork,
    ILogger<WorkerPoolService> logger)
{
    /// <summary>
    /// Initialize workers from config templates.
    /// E.g., 2x qwen:32b + 1x deepseek:33b → worker-a, worker-b, worker-c
    /// </summary>
    public async Task InitializeFromConfigAsync(
        IEnumerable<WorkerTemplate> templates,
        CancellationToken ct = default)
    {
        var existing = await workerRepo.GetAllAsync(ct);
        if (existing.Count > 0)
        {
            logger.LogInformation("Worker pool already has {Count} workers, skipping init", existing.Count);
            return;
        }

        var letterIndex = 0;
        foreach (var template in templates)
        {
            for (var i = 0; i < template.Count; i++)
            {
                var name = $"worker-{(char)('a' + letterIndex)}";
                letterIndex++;

                var worker = new CoderWorker
                {
                    Name = name,
                    Model = template.Model,
                    Provider = template.Provider,
                    Status = WorkerStatus.Idle,
                };

                await workerRepo.AddAsync(worker, ct);
                logger.LogInformation("Created worker {Name} ({Model})", name, template.Model);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Worker pool initialized with {Count} workers", letterIndex);
    }

    /// <summary>Add a new worker at runtime.</summary>
    public async Task<CoderWorker> AddWorkerAsync(
        string model,
        string provider = "ollama",
        string? name = null,
        CancellationToken ct = default)
    {
        var existing = await workerRepo.GetAllAsync(ct);
        name ??= $"worker-{(char)('a' + existing.Count)}";

        var worker = new CoderWorker
        {
            Name = name,
            Model = model,
            Provider = provider,
            Status = WorkerStatus.Idle,
        };

        await workerRepo.AddAsync(worker, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Added worker {Name} ({Model})", name, model);
        return worker;
    }

    /// <summary>
    /// Remove a worker. If busy, marks for removal and waits for completion.
    /// Returns true if removed immediately, false if deferred.
    /// </summary>
    public async Task<bool> RemoveWorkerAsync(
        Guid workerId,
        bool force = false,
        CancellationToken ct = default)
    {
        var worker = await workerRepo.GetByIdAsync(workerId, ct);
        if (worker is null)
        {
            logger.LogWarning("Worker {Id} not found for removal", workerId);
            return false;
        }

        if (worker.Status == WorkerStatus.Working && !force)
        {
            // Mark as offline — will be cleaned up after task completes
            worker.Status = WorkerStatus.Offline;
            await workerRepo.UpdateAsync(worker, ct);
            await unitOfWork.SaveChangesAsync(ct);

            logger.LogInformation("Worker {Name} marked for removal (busy, will remove after task)",
                worker.Name);
            return false;
        }

        await workerRepo.RemoveAsync(workerId, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("Worker {Name} removed", worker.Name);
        return true;
    }

    /// <summary>Remove a worker by name.</summary>
    public async Task<bool> RemoveWorkerByNameAsync(
        string name,
        bool force = false,
        CancellationToken ct = default)
    {
        var all = await workerRepo.GetAllAsync(ct);
        var worker = all.FirstOrDefault(w => w.Name == name);
        if (worker is null)
        {
            logger.LogWarning("Worker '{Name}' not found", name);
            return false;
        }

        return await RemoveWorkerAsync(worker.Id, force, ct);
    }

    /// <summary>Get all workers with their status.</summary>
    public async Task<IReadOnlyList<CoderWorker>> GetAllWorkersAsync(CancellationToken ct = default)
        => await workerRepo.GetAllAsync(ct);

    /// <summary>Get idle workers available for task assignment.</summary>
    public async Task<IReadOnlyList<CoderWorker>> GetIdleWorkersAsync(CancellationToken ct = default)
        => await workerRepo.GetIdleWorkersAsync(ct);

    /// <summary>Get pool summary status.</summary>
    public async Task<WorkerPoolStatus> GetPoolStatusAsync(CancellationToken ct = default)
    {
        var workers = await workerRepo.GetAllAsync(ct);
        return new WorkerPoolStatus(
            TotalWorkers: workers.Count,
            IdleCount: workers.Count(w => w.Status == WorkerStatus.Idle),
            WorkingCount: workers.Count(w => w.Status == WorkerStatus.Working),
            OfflineCount: workers.Count(w => w.Status == WorkerStatus.Offline),
            Workers: workers);
    }

    /// <summary>
    /// Clean up workers marked as Offline after their tasks complete.
    /// Called by TaskBoardService.CompleteTaskAsync.
    /// </summary>
    public async Task CleanupOfflineWorkersAsync(CancellationToken ct = default)
    {
        var all = await workerRepo.GetAllAsync(ct);
        var toRemove = all.Where(w => w.Status == WorkerStatus.Offline && w.CurrentTaskId is null).ToList();

        foreach (var worker in toRemove)
        {
            await workerRepo.RemoveAsync(worker.Id, ct);
            logger.LogInformation("Cleaned up offline worker {Name}", worker.Name);
        }

        if (toRemove.Count > 0)
            await unitOfWork.SaveChangesAsync(ct);
    }
}

public record WorkerTemplate(int Count, string Model, string Provider = "ollama");

public record WorkerPoolStatus(
    int TotalWorkers,
    int IdleCount,
    int WorkingCount,
    int OfflineCount,
    IReadOnlyList<CoderWorker> Workers);
