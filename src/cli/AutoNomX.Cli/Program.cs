using System.CommandLine;
using AutoNomX.Application;
using AutoNomX.Application.Services;
using AutoNomX.Cli.Output;
using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using AutoNomX.Domain.Interfaces;
using AutoNomX.Infrastructure;
using AutoNomX.Infrastructure.Grpc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

// ── Build Host ──────────────────────────────────────────────
var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Check for --dry-run flag
var dryRun = args.Contains("--dry-run");
if (dryRun)
{
    // Override real gateway with mock
    builder.Services.AddSingleton<IAgentGateway, DryRunAgentGateway>();
}

builder.Logging.SetMinimumLevel(LogLevel.Warning);

var host = builder.Build();

// ── CLI Commands ────────────────────────────────────────────
var rootCommand = new RootCommand("AutoNomX — Autonomous Software Development System");

var dryRunOption = new Option<bool>("--dry-run", "Run in dry-run mode (mock agents, no LLM)");
rootCommand.AddGlobalOption(dryRunOption);

// ── new ─────────────────────────────────────────────────────
var newCommand = new Command("new", "Create a new project and start the pipeline");
var descArg = new Argument<string>("description", "Project description in natural language");
newCommand.AddArgument(descArg);

newCommand.SetHandler(async (string description) =>
{
    ConsoleOutput.WriteHeader();

    if (dryRun)
        AnsiConsole.MarkupLine("[yellow bold]🏜️  DRY-RUN MODE — agents will return mock responses[/]\n");

    using var scope = host.Services.CreateScope();
    var projectRepo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
    var orchestrator = scope.ServiceProvider.GetRequiredService<OrchestratorService>();
    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    // Create project
    var project = new Project
    {
        Name = description.Length > 60 ? description[..60] : description,
        Description = description,
        Status = ProjectStatus.Created,
    };

    await projectRepo.AddAsync(project);
    await unitOfWork.SaveChangesAsync();

    ConsoleOutput.WriteInfo($"Project created: {project.Id}");
    AnsiConsole.MarkupLine($"  [dim]Name: {Markup.Escape(project.Name)}[/]");
    AnsiConsole.WriteLine();

    // Start pipeline
    await ConsoleOutput.WithSpinnerAsync("Starting pipeline...", async () =>
    {
        try
        {
            var run = await orchestrator.StartPipelineAsync(project.Id, description);
            ConsoleOutput.WriteSuccess($"Pipeline started: {run.Id}");
            AnsiConsole.MarkupLine($"  [dim]State: {run.CurrentStep}[/]");
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Pipeline failed: {ex.Message}");
        }
    });

}, descArg);

// ── status ──────────────────────────────────────────────────
var statusCommand = new Command("status", "Show project and pipeline status");
var statusIdArg = new Argument<string>("project-id", "Project ID (or pipeline run ID)");
statusCommand.AddArgument(statusIdArg);

statusCommand.SetHandler(async (string projectId) =>
{
    using var scope = host.Services.CreateScope();
    var projectRepo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
    var pipelineRepo = scope.ServiceProvider.GetRequiredService<IPipelineRunRepository>();
    var orchestrator = scope.ServiceProvider.GetRequiredService<OrchestratorService>();

    if (!Guid.TryParse(projectId, out var id))
    {
        ConsoleOutput.WriteError("Invalid ID format. Use a valid GUID.");
        return;
    }

    // Try as project ID first
    var project = await projectRepo.GetByIdAsync(id);
    if (project is not null)
    {
        AnsiConsole.MarkupLine($"[bold]Project:[/] {Markup.Escape(project.Name)}");
        AnsiConsole.MarkupLine($"[dim]Status: {project.Status}[/]\n");

        var activePipeline = await pipelineRepo.GetActiveByProjectIdAsync(id);
        if (activePipeline is not null)
        {
            var status = await orchestrator.GetPipelineStatusAsync(activePipeline.Id);
            ConsoleOutput.WritePipelineStatus(status);
        }
        else
        {
            var pipelines = await pipelineRepo.GetByProjectIdAsync(id);
            if (pipelines.Count > 0)
            {
                var latest = pipelines.OrderByDescending(p => p.CreatedAt).First();
                var status = await orchestrator.GetPipelineStatusAsync(latest.Id);
                ConsoleOutput.WritePipelineStatus(status);
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]No pipeline runs found.[/]");
            }
        }
        return;
    }

    // Try as pipeline run ID
    var run = await pipelineRepo.GetByIdAsync(id);
    if (run is not null)
    {
        var status = await orchestrator.GetPipelineStatusAsync(id);
        ConsoleOutput.WritePipelineStatus(status);
        return;
    }

    ConsoleOutput.WriteError($"No project or pipeline found with ID: {projectId}");

}, statusIdArg);

// ── projects ────────────────────────────────────────────────
var projectsCommand = new Command("projects", "List all projects");

projectsCommand.SetHandler(async () =>
{
    using var scope = host.Services.CreateScope();
    var projectRepo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();

    var projects = await projectRepo.GetAllAsync();
    if (projects.Count == 0)
    {
        AnsiConsole.MarkupLine("[dim]No projects found. Create one with:[/] [bold]autonomx new \"description\"[/]");
        return;
    }

    ConsoleOutput.WriteProjectsTable(projects);
});

// ── run ─────────────────────────────────────────────────────
var runCommand = new Command("run", "Resume a paused pipeline");
var runProjectOption = new Option<string>("--project", "Project ID") { IsRequired = true };
runCommand.AddOption(runProjectOption);

runCommand.SetHandler(async (string projectId) =>
{
    if (!Guid.TryParse(projectId, out var id))
    {
        ConsoleOutput.WriteError("Invalid project ID format.");
        return;
    }

    using var scope = host.Services.CreateScope();
    var pipelineRepo = scope.ServiceProvider.GetRequiredService<IPipelineRunRepository>();
    var orchestrator = scope.ServiceProvider.GetRequiredService<OrchestratorService>();

    var pipeline = await pipelineRepo.GetActiveByProjectIdAsync(id);
    if (pipeline is null)
    {
        // Get latest paused
        var all = await pipelineRepo.GetByProjectIdAsync(id);
        pipeline = all.FirstOrDefault(p => p.Status == PipelineStatus.Paused);
    }

    if (pipeline is null)
    {
        ConsoleOutput.WriteError("No active or paused pipeline found for this project.");
        return;
    }

    await ConsoleOutput.WithSpinnerAsync("Resuming pipeline...", async () =>
    {
        try
        {
            await orchestrator.ResumePipelineAsync(pipeline.Id);
            ConsoleOutput.WriteSuccess("Pipeline resumed.");
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError($"Resume failed: {ex.Message}");
        }
    });

}, runProjectOption);

// ── workers ─────────────────────────────────────────────────
var workersCommand = new Command("workers", "Show worker pool status");

workersCommand.SetHandler(async () =>
{
    using var scope = host.Services.CreateScope();
    var workerRepo = scope.ServiceProvider.GetRequiredService<ICoderWorkerRepository>();

    var workers = await workerRepo.GetAllAsync();
    if (workers.Count == 0)
    {
        AnsiConsole.MarkupLine("[dim]No workers registered.[/]");
        return;
    }

    ConsoleOutput.WriteWorkersTable(workers);
});

// ── logs ────────────────────────────────────────────────────
var logsCommand = new Command("logs", "Show agent execution logs");
var logsProjectArg = new Argument<string>("project-id", "Project ID");
var logsAgentOption = new Option<string?>("--agent", "Filter by agent name");
logsCommand.AddArgument(logsProjectArg);
logsCommand.AddOption(logsAgentOption);

logsCommand.SetHandler(async (string projectId, string? agentName) =>
{
    if (!Guid.TryParse(projectId, out var id))
    {
        ConsoleOutput.WriteError("Invalid project ID format.");
        return;
    }

    using var scope = host.Services.CreateScope();
    var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
    var historyRepo = scope.ServiceProvider.GetRequiredService<IAgentHistoryRepository>();

    // Get tasks for project to find related agent histories
    var tasks = await taskRepo.GetByProjectIdAsync(id);
    var allLogs = new List<AgentHistory>();

    foreach (var task in tasks)
    {
        var logs = await historyRepo.GetByTaskIdAsync(task.Id);
        allLogs.AddRange(logs);
    }

    if (agentName is not null)
        allLogs = allLogs.Where(l =>
            l.Role.Contains(agentName, StringComparison.OrdinalIgnoreCase)).ToList();

    if (allLogs.Count == 0)
    {
        AnsiConsole.MarkupLine("[dim]No logs found.[/]");
        return;
    }

    ConsoleOutput.WriteLogsTable(allLogs);

}, logsProjectArg, logsAgentOption);

// ── Register commands ───────────────────────────────────────
rootCommand.AddCommand(newCommand);
rootCommand.AddCommand(statusCommand);
rootCommand.AddCommand(projectsCommand);
rootCommand.AddCommand(runCommand);
rootCommand.AddCommand(workersCommand);
rootCommand.AddCommand(logsCommand);

return await rootCommand.InvokeAsync(args);
