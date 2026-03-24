using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoNomX.Application.Services;

/// <summary>
/// Records and queries agent/worker performance metrics.
/// Used by Model Manager for informed decisions.
/// </summary>
public class MetricsService(
    IAgentMetricsRepository metricsRepo,
    ICoderWorkerRepository workerRepo,
    IUnitOfWork unitOfWork,
    ILogger<MetricsService> logger)
{
    /// <summary>Record a task completion with metrics.</summary>
    public async Task RecordTaskCompletionAsync(
        Guid agentId,
        string model,
        int iterations,
        long tokensUsed,
        double durationSeconds,
        bool success,
        ReviewScores? scores = null,
        CancellationToken ct = default)
    {
        var existing = await metricsRepo.GetByAgentAndModelAsync(agentId, model, ct);

        if (existing is not null)
        {
            existing.TotalExecutions++;
            if (success) existing.SuccessCount++;
            else existing.FailureCount++;
            existing.TotalTokensUsed += tokensUsed;

            // Running average for iterations
            existing.AvgIterations = ((existing.AvgIterations * (existing.TotalExecutions - 1)) + iterations)
                / existing.TotalExecutions;

            // Running average for duration
            existing.AvgDurationSeconds = ((existing.AvgDurationSeconds * (existing.TotalExecutions - 1)) + durationSeconds)
                / existing.TotalExecutions;

            // Update score if provided
            if (scores is not null)
            {
                var avgScore = (scores.Correctness + scores.CodeQuality + scores.Security
                    + scores.Performance + scores.Completeness) / 5.0;
                existing.AvgScore = ((existing.AvgScore * (existing.TotalExecutions - 1)) + avgScore)
                    / existing.TotalExecutions;
            }

            await metricsRepo.AddOrUpdateAsync(existing, ct);
        }
        else
        {
            var avgScore = scores is not null
                ? (scores.Correctness + scores.CodeQuality + scores.Security
                    + scores.Performance + scores.Completeness) / 5.0
                : 0.0;

            var metrics = new AgentMetrics
            {
                AgentId = agentId,
                ModelUsed = model,
                TotalExecutions = 1,
                SuccessCount = success ? 1 : 0,
                FailureCount = success ? 0 : 1,
                AvgIterations = iterations,
                TotalTokensUsed = tokensUsed,
                AvgDurationSeconds = durationSeconds,
                AvgScore = avgScore,
            };

            await metricsRepo.AddOrUpdateAsync(metrics, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        logger.LogDebug("Recorded metrics for agent {AgentId}, model {Model}", agentId, model);
    }

    /// <summary>Get a worker's overall performance.</summary>
    public async Task<WorkerPerformance> GetWorkerPerformanceAsync(
        Guid workerId,
        CancellationToken ct = default)
    {
        var worker = await workerRepo.GetByIdAsync(workerId, ct);
        if (worker is null)
            return new WorkerPerformance(workerId, "unknown", "unknown", 0, 0, 0, 0, 0);

        var metrics = await metricsRepo.GetByAgentIdAsync(workerId, ct);
        var total = metrics.Sum(m => m.TotalExecutions);
        var successes = metrics.Sum(m => m.SuccessCount);
        var avgIterations = total > 0 ? metrics.Average(m => m.AvgIterations) : 0;
        var avgTokens = total > 0 ? metrics.Sum(m => m.TotalTokensUsed) / total : 0;
        var avgScore = total > 0 ? metrics.Average(m => m.AvgScore) : 0;

        return new WorkerPerformance(
            WorkerId: workerId,
            WorkerName: worker.Name,
            CurrentModel: worker.Model,
            TotalTasks: total,
            SuccessRate: total > 0 ? (double)successes / total : 0,
            AvgIterations: avgIterations,
            AvgTokensPerTask: avgTokens,
            AvgScore: avgScore);
    }

    /// <summary>Get performance stats for a specific model.</summary>
    public async Task<ModelPerformance> GetModelPerformanceAsync(
        string model,
        CancellationToken ct = default)
    {
        var workers = await workerRepo.GetAllAsync(ct);
        var allMetrics = new List<AgentMetrics>();

        foreach (var worker in workers)
        {
            var m = await metricsRepo.GetByAgentAndModelAsync(worker.Id, model, ct);
            if (m is not null) allMetrics.Add(m);
        }

        var total = allMetrics.Sum(m => m.TotalExecutions);
        var successes = allMetrics.Sum(m => m.SuccessCount);

        return new ModelPerformance(
            Model: model,
            TotalTasks: total,
            SuccessRate: total > 0 ? (double)successes / total : 0,
            AvgIterations: total > 0 ? allMetrics.Average(m => m.AvgIterations) : 0,
            TotalTokens: allMetrics.Sum(m => m.TotalTokensUsed),
            AvgScore: total > 0 ? allMetrics.Average(m => m.AvgScore) : 0,
            WorkerCount: allMetrics.Count);
    }

    /// <summary>Get all worker performances.</summary>
    public async Task<IReadOnlyList<WorkerPerformance>> GetAllWorkerPerformancesAsync(
        CancellationToken ct = default)
    {
        var workers = await workerRepo.GetAllAsync(ct);
        var performances = new List<WorkerPerformance>();

        foreach (var worker in workers)
            performances.Add(await GetWorkerPerformanceAsync(worker.Id, ct));

        return performances;
    }
}

public record WorkerPerformance(
    Guid WorkerId,
    string WorkerName,
    string CurrentModel,
    int TotalTasks,
    double SuccessRate,
    double AvgIterations,
    long AvgTokensPerTask,
    double AvgScore);

public record ModelPerformance(
    string Model,
    int TotalTasks,
    double SuccessRate,
    double AvgIterations,
    long TotalTokens,
    double AvgScore,
    int WorkerCount);

public record ReviewScores(
    double Correctness,
    double CodeQuality,
    double Security,
    double Performance,
    double Completeness);
