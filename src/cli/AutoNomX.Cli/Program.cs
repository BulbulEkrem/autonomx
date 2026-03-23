using System.CommandLine;

var rootCommand = new RootCommand("AutoNomX — Autonomous Software Development System");

var newCommand = new Command("new", "Create a new project")
{
    new Argument<string>("description", "Project description in natural language")
};

var statusCommand = new Command("status", "Show project status")
{
    new Argument<string>("project-id", "Project ID")
};

var workersCommand = new Command("workers", "Show worker pool status");

rootCommand.AddCommand(newCommand);
rootCommand.AddCommand(statusCommand);
rootCommand.AddCommand(workersCommand);

newCommand.SetHandler((string description) =>
{
    Console.WriteLine($"Creating project: {description}");
    // TODO (M5): Implement project creation via API
}, newCommand.Arguments[0] as Argument<string> ?? throw new InvalidOperationException());

return await rootCommand.InvokeAsync(args);
