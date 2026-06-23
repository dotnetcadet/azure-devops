using Azure.DevOps.Cli;
using Azure.DevOps.Cli.Commands;
using Azure.DevOps.Cli.Ui;
using Sharprompt;

var command = args.Length > 0 ? args[0].ToLowerInvariant() : null;

switch (command)
{
    case "-h" or "--help" or "help":
        PrintUsage();
        return 0;
    case "--version" or "version":
        Console.WriteLine("azdo 1.0.0 — Azure DevOps CLI (REST 7.2)");
        return 0;
}

var explicitInteractive = command is "-i" or "--interactive" or "interactive";

// Reject unknown arguments rather than silently dropping into the interactive console.
if (command is not null && !explicitInteractive)
{
    ConsoleUx.Error($"Unknown argument '{args[0]}'.");
    PrintUsage();
    return 1;
}

// Never launch the interactive console when stdin is redirected (an MCP host, a pipe, CI, …):
// Sharprompt needs a real terminal, so reading keys from a redirected stream would hang or crash.
if (Console.IsInputRedirected)
{
    if (explicitInteractive)
    {
        ConsoleUx.Error("Interactive mode requires a real terminal (stdin is redirected).");
        return 1;
    }

    ConsoleUx.Info("azdo is an interactive console — run it in a terminal, or `azdo --interactive`. See `azdo --help`.");
    return 0;
}

Prompt.ThrowExceptionOnCancel = true;
Console.OutputEncoding = System.Text.Encoding.UTF8;

ConsoleUx.Banner();

using var session = new AzdoSession();

if (session.Profiles.Count == 0)
{
    ConsoleUx.Info("Welcome! Let's connect your first Azure DevOps organization.");
    await ProfileCommands.LoginAsync(session);
}

const string ListProjects = "Projects — list / set active";
const string ListRepos = "Repositories — list";
const string ListPrs = "Pull requests — list";
const string ListBuilds = "Builds — recent";
const string GetWorkItem = "Work item — get by id";
const string CreateWorkItem = "Work item — create";
const string SwitchProfile = "Profile — switch active";
const string AddProfile = "Profile — add / sign in";
const string ListProfiles = "Profile — list";
const string RemoveProfile = "Profile — remove";
const string Quit = "Quit";

var menu = new[]
{
    ListProjects, ListRepos, ListPrs, ListBuilds, GetWorkItem, CreateWorkItem,
    SwitchProfile, AddProfile, ListProfiles, RemoveProfile, Quit,
};

while (true)
{
    var who = session.Current is { } c ? $"{c.Name} ({c.Organization}{(string.IsNullOrEmpty(session.ActiveProject) ? "" : "/" + session.ActiveProject)})" : "no profile";
    ConsoleUx.Write(ConsoleColor.DarkCyan, $"\nActive: {who}");

    string choice;
    try
    {
        choice = Prompt.Select("What would you like to do?", menu, pageSize: 12);
    }
    catch (PromptCanceledException)
    {
        break; // Ctrl-C / Esc at the main menu exits.
    }

    if (choice == Quit)
    {
        break;
    }

    try
    {
        switch (choice)
        {
            case ListProjects: await Features.ListProjectsAsync(session); break;
            case ListRepos: await Features.ListRepositoriesAsync(session); break;
            case ListPrs: await Features.ListPullRequestsAsync(session); break;
            case ListBuilds: await Features.ListBuildsAsync(session); break;
            case GetWorkItem: await Features.GetWorkItemAsync(session); break;
            case CreateWorkItem: await Features.CreateWorkItemAsync(session); break;
            case SwitchProfile: ProfileCommands.Switch(session); break;
            case AddProfile: await ProfileCommands.LoginAsync(session); break;
            case ListProfiles: ProfileCommands.List(session); break;
            case RemoveProfile: ProfileCommands.Remove(session); break;
        }
    }
    catch (PromptCanceledException)
    {
        ConsoleUx.Info("Cancelled — back to the menu.");
    }
}

ConsoleUx.Info("Goodbye.");
return 0;

static void PrintUsage()
{
    Console.WriteLine(
        """
        azdo — Azure DevOps CLI (built on the fluent Azure.DevOps.Sdk)

        Usage:
          azdo                  Launch the interactive console when run in a terminal.
          azdo -i, --interactive  Force the interactive console.
          azdo --help           Show this help.
          azdo --version        Show version information.

        The interactive console only starts in a real terminal — when stdin is redirected (a pipe,
        CI, or an MCP host) it prints this help instead of waiting for keyboard input.
        It supports multiple organizations and projects via named profiles; personal access tokens
        are stored per-user and protected with Windows DPAPI where available.
        """);
}
