using System.Diagnostics;
using AutoNomX.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoNomX.Infrastructure.Git;

/// <summary>
/// Git operations via CLI wrapper.
/// Uses git commands directly for maximum compatibility.
/// </summary>
public class GitCliService(ILogger<GitCliService> logger) : IGitService
{
    public async Task InitRepoAsync(string projectPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(projectPath);
        await RunGitAsync(projectPath, "init", ct);
        await RunGitAsync(projectPath, "checkout -b main", ct);

        // Create .gitignore
        var gitignore = Path.Combine(projectPath, ".gitignore");
        if (!File.Exists(gitignore))
        {
            await File.WriteAllTextAsync(gitignore,
                "bin/\nobj/\n*.user\n.vs/\n__pycache__/\n*.pyc\n.env\nnode_modules/\n", ct);
        }

        await RunGitAsync(projectPath, "add -A", ct);
        await RunGitAsync(projectPath, "commit -m \"feat: initial project setup\" --allow-empty", ct);

        logger.LogInformation("Git repo initialized at {Path}", projectPath);
    }

    public async Task<string> CreateBranchAsync(string projectPath, string branchName, CancellationToken ct = default)
    {
        // Sanitize branch name
        var safeName = branchName
            .Replace(" ", "-")
            .Replace("/", "-")
            .ToLowerInvariant();

        await RunGitAsync(projectPath, $"checkout -b {safeName}", ct);
        logger.LogInformation("Branch '{Branch}' created", safeName);
        return safeName;
    }

    public async Task CheckoutAsync(string projectPath, string branchName, CancellationToken ct = default)
    {
        await RunGitAsync(projectPath, $"checkout {branchName}", ct);
    }

    public async Task CommitAllAsync(string projectPath, string message, CancellationToken ct = default)
    {
        await RunGitAsync(projectPath, "add -A", ct);

        // Check if there are changes to commit
        var status = await RunGitAsync(projectPath, "status --porcelain", ct);
        if (string.IsNullOrWhiteSpace(status))
        {
            logger.LogDebug("No changes to commit");
            return;
        }

        // Escape message for shell
        var escapedMsg = message.Replace("\"", "\\\"");
        await RunGitAsync(projectPath, $"commit -m \"{escapedMsg}\"", ct);
        logger.LogInformation("Committed: {Message}", message);
    }

    public async Task<MergeResult> MergeBranchAsync(
        string projectPath,
        string sourceBranch,
        string targetBranch = "main",
        CancellationToken ct = default)
    {
        await CheckoutAsync(projectPath, targetBranch, ct);

        try
        {
            // Try fast-forward first
            await RunGitAsync(projectPath, $"merge --ff-only {sourceBranch}", ct);
            logger.LogInformation("Merged {Source} → {Target} (fast-forward)", sourceBranch, targetBranch);
            return new MergeResult(Success: true, HasConflicts: false, ConflictFiles: []);
        }
        catch
        {
            // Fall back to regular merge
            try
            {
                await RunGitAsync(projectPath, $"merge {sourceBranch} --no-edit", ct);
                logger.LogInformation("Merged {Source} → {Target}", sourceBranch, targetBranch);
                return new MergeResult(Success: true, HasConflicts: false, ConflictFiles: []);
            }
            catch
            {
                // Check for conflicts
                var conflictOutput = await RunGitAsync(projectPath, "diff --name-only --diff-filter=U", ct);
                var conflictFiles = conflictOutput
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .ToList();

                if (conflictFiles.Count > 0)
                {
                    logger.LogWarning("Merge conflict: {Source} → {Target}, files: {Files}",
                        sourceBranch, targetBranch, string.Join(", ", conflictFiles));

                    // Abort the failed merge
                    await RunGitAsync(projectPath, "merge --abort", ct);

                    return new MergeResult(Success: false, HasConflicts: true, ConflictFiles: conflictFiles);
                }

                throw;
            }
        }
    }

    public async Task<BranchStatus> GetBranchStatusAsync(string projectPath, CancellationToken ct = default)
    {
        var branch = (await RunGitAsync(projectPath, "rev-parse --abbrev-ref HEAD", ct)).Trim();

        var statusOutput = await RunGitAsync(projectPath, "status --porcelain", ct);
        var changedFiles = statusOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Length > 3 ? l[3..] : l.Trim())
            .ToList();

        // ahead/behind relative to main
        var ahead = 0;
        var behind = 0;
        if (branch != "main")
        {
            try
            {
                var revList = await RunGitAsync(projectPath,
                    $"rev-list --left-right --count main...{branch}", ct);
                var parts = revList.Trim().Split('\t');
                if (parts.Length == 2)
                {
                    behind = int.TryParse(parts[0], out var b) ? b : 0;
                    ahead = int.TryParse(parts[1], out var a) ? a : 0;
                }
            }
            catch { /* main may not exist yet */ }
        }

        return new BranchStatus(branch, changedFiles, ahead, behind);
    }

    public async Task<IReadOnlyList<GitLogEntry>> GetLogAsync(
        string projectPath, int count = 10, CancellationToken ct = default)
    {
        var output = await RunGitAsync(projectPath,
            $"log --oneline --format=\"%H|%s|%an|%aI\" -n {count}", ct);

        var entries = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line =>
            {
                var parts = line.Split('|', 4);
                return parts.Length >= 4
                    ? new GitLogEntry(
                        Hash: parts[0],
                        Message: parts[1],
                        Author: parts[2],
                        Date: DateTime.TryParse(parts[3], out var d) ? d : DateTime.UtcNow)
                    : new GitLogEntry(parts[0], line, "unknown", DateTime.UtcNow);
            })
            .ToList();

        return entries;
    }

    public async Task RevertLastCommitAsync(string projectPath, CancellationToken ct = default)
    {
        await RunGitAsync(projectPath, "revert HEAD --no-edit", ct);
        logger.LogInformation("Reverted last commit");
    }

    public async Task<string> GetDiffAsync(
        string projectPath, string branchA, string branchB, CancellationToken ct = default)
    {
        return await RunGitAsync(projectPath, $"diff {branchA}..{branchB} --stat", ct);
    }

    // ── Shell helper ────────────────────────────────────────────

    private static async Task<string> RunGitAsync(string workingDir, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process");

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"git {arguments} failed (exit={process.ExitCode}): {stderr}");

        return stdout;
    }
}
