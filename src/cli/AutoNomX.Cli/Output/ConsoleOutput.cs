using AutoNomX.Application.Services;
using AutoNomX.Domain;
using AutoNomX.Domain.Entities;
using Spectre.Console;

namespace AutoNomX.Cli.Output;

/// <summary>Formatted console output using Spectre.Console.</summary>
public static class ConsoleOutput
{
    public static void WriteHeader()
    {
        AnsiConsole.Write(new FigletText("AutoNomX").Color(Color.Blue));
        AnsiConsole.MarkupLine("[dim]Autonomous Software Development System[/]");
        AnsiConsole.WriteLine();
    }

    public static void WriteSuccess(string message)
        => AnsiConsole.MarkupLine($"[green]✅ {Markup.Escape(message)}[/]");

    public static void WriteError(string message)
        => AnsiConsole.MarkupLine($"[red]❌ {Markup.Escape(message)}[/]");

    public static void WriteWarning(string message)
        => AnsiConsole.MarkupLine($"[yellow]⚠️  {Markup.Escape(message)}[/]");

    public static void WriteInfo(string message)
        => AnsiConsole.MarkupLine($"[blue]ℹ️  {Markup.Escape(message)}[/]");

    public static string StateEmoji(string state) => state.ToUpperInvariant() switch
    {
        "IDLE" => "⏳",
        "PLANNING" => "📋",
        "ARCHITECTING" => "🏗️",
        "CODING" => "💻",
        "TESTING" => "🧪",
        "REVIEWING" => "🔍",
        "COMPLETED" => "✅",
        "FAILED" => "❌",
        "PAUSED" => "⏸️",
        _ => "🔄",
    };

    public static string StatusColor(PipelineStatus status) => status switch
    {
        PipelineStatus.Running => "blue",
        PipelineStatus.Completed => "green",
        PipelineStatus.Failed => "red",
        PipelineStatus.Paused => "yellow",
        _ => "dim",
    };

    public static void WritePipelineStatus(PipelineStatusInfo info)
    {
        var emoji = StateEmoji(info.CurrentStep);
        var color = StatusColor(info.Status);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"[bold]Pipeline Status[/]")
            .AddColumn("[bold]Property[/]")
            .AddColumn("[bold]Value[/]");

        table.AddRow("Pipeline ID", $"[dim]{info.PipelineRunId}[/]");
        table.AddRow("Project ID", $"[dim]{info.ProjectId}[/]");
        table.AddRow("Status", $"[{color}]{emoji} {info.Status}[/]");
        table.AddRow("Current Step", $"[{color}]{info.CurrentStep}[/]");
        table.AddRow("Iteration", info.Iteration.ToString());
        table.AddRow("Started", info.StartedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-");
        table.AddRow("Completed", info.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-");

        if (!string.IsNullOrEmpty(info.ErrorMessage))
            table.AddRow("Error", $"[red]{Markup.Escape(info.ErrorMessage)}[/]");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        WriteBoardStatus(info.BoardStatus);
    }

    public static void WriteBoardStatus(BoardStatus board)
    {
        var chart = new BreakdownChart()
            .Width(60);

        if (board.DoneCount > 0) chart.AddItem("Done", board.DoneCount, Color.Green);
        if (board.InProgressCount > 0) chart.AddItem("In Progress", board.InProgressCount, Color.Blue);
        if (board.TestingCount > 0) chart.AddItem("Testing", board.TestingCount, Color.Yellow);
        if (board.ReviewCount > 0) chart.AddItem("Review", board.ReviewCount, Color.Purple);
        if (board.ReadyCount > 0) chart.AddItem("Ready", board.ReadyCount, Color.Grey);
        if (board.FailedCount > 0) chart.AddItem("Failed", board.FailedCount, Color.Red);
        if (board.RevisionCount > 0) chart.AddItem("Revision", board.RevisionCount, Color.Orange1);

        if (board.TotalTasks > 0)
        {
            AnsiConsole.MarkupLine($"[bold]Task Board[/] ({board.DoneCount}/{board.TotalTasks} completed)");
            AnsiConsole.Write(chart);
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]No tasks on the board yet.[/]");
        }
    }

    public static void WriteProjectsTable(IEnumerable<Project> projects)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Projects[/]")
            .AddColumn("[bold]ID[/]")
            .AddColumn("[bold]Name[/]")
            .AddColumn("[bold]Status[/]")
            .AddColumn("[bold]Created[/]");

        foreach (var p in projects)
        {
            var statusColor = p.Status switch
            {
                ProjectStatus.Completed => "green",
                ProjectStatus.Failed => "red",
                ProjectStatus.InProgress => "blue",
                ProjectStatus.Paused => "yellow",
                _ => "dim",
            };

            table.AddRow(
                $"[dim]{p.Id.ToString()[..8]}...[/]",
                Markup.Escape(p.Name),
                $"[{statusColor}]{p.Status}[/]",
                p.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
        }

        AnsiConsole.Write(table);
    }

    public static void WriteWorkersTable(IEnumerable<CoderWorker> workers)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Worker Pool[/]")
            .AddColumn("[bold]ID[/]")
            .AddColumn("[bold]Name[/]")
            .AddColumn("[bold]Model[/]")
            .AddColumn("[bold]Status[/]")
            .AddColumn("[bold]Current Task[/]");

        foreach (var w in workers)
        {
            var statusColor = w.Status switch
            {
                WorkerStatus.Idle => "green",
                WorkerStatus.Working => "blue",
                WorkerStatus.Offline => "red",
                _ => "yellow",
            };

            table.AddRow(
                $"[dim]{w.Id.ToString()[..8]}...[/]",
                Markup.Escape(w.Name),
                Markup.Escape(w.Model),
                $"[{statusColor}]{w.Status}[/]",
                w.CurrentTaskId?.ToString()[..8] ?? "[dim]-[/]");
        }

        AnsiConsole.Write(table);
    }

    public static void WriteLogsTable(IEnumerable<AgentHistory> logs)
    {
        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("[bold]Time[/]")
            .AddColumn("[bold]Agent[/]")
            .AddColumn("[bold]Model[/]")
            .AddColumn("[bold]Tokens[/]")
            .AddColumn("[bold]Content[/]");

        foreach (var log in logs.OrderByDescending(l => l.CreatedAt).Take(50))
        {
            var content = log.Content.Length > 80
                ? log.Content[..80] + "..."
                : log.Content;

            table.AddRow(
                log.CreatedAt.ToString("HH:mm:ss"),
                Markup.Escape(log.Role),
                Markup.Escape(log.ModelUsed ?? "-"),
                log.TokensUsed.ToString(),
                Markup.Escape(content));
        }

        AnsiConsole.Write(table);
    }

    public static async Task WithSpinnerAsync(string message, Func<Task> action)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync(message, async _ => await action());
    }

    public static async Task<T> WithSpinnerAsync<T>(string message, Func<Task<T>> action)
    {
        T result = default!;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync(message, async _ => { result = await action(); });
        return result;
    }
}
