namespace AutoNomX.Domain.Interfaces;

/// <summary>
/// Git operations abstraction for workspace projects.
/// </summary>
public interface IGitService
{
    Task InitRepoAsync(string projectPath, CancellationToken ct = default);
    Task<string> CreateBranchAsync(string projectPath, string branchName, CancellationToken ct = default);
    Task CheckoutAsync(string projectPath, string branchName, CancellationToken ct = default);
    Task CommitAllAsync(string projectPath, string message, CancellationToken ct = default);
    Task<MergeResult> MergeBranchAsync(string projectPath, string sourceBranch, string targetBranch = "main", CancellationToken ct = default);
    Task<BranchStatus> GetBranchStatusAsync(string projectPath, CancellationToken ct = default);
    Task<IReadOnlyList<GitLogEntry>> GetLogAsync(string projectPath, int count = 10, CancellationToken ct = default);
    Task RevertLastCommitAsync(string projectPath, CancellationToken ct = default);
    Task<string> GetDiffAsync(string projectPath, string branchA, string branchB, CancellationToken ct = default);
}

public record BranchStatus(
    string CurrentBranch,
    IReadOnlyList<string> ChangedFiles,
    int Ahead,
    int Behind);

public record GitLogEntry(
    string Hash,
    string Message,
    string Author,
    DateTime Date);

public record MergeResult(
    bool Success,
    bool HasConflicts,
    IReadOnlyList<string> ConflictFiles);
