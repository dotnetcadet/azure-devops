using Azure.DevOps.Cli;
using Azure.DevOps.Cli.Commands;
using Azure.DevOps.Cli.Ui;
using Sharprompt;

if (args.Length > 0 && args[0] is "-h" or "--help" or "help")
{
    PrintUsage();
    return 0;
}

if (args.Length > 0 && args[0] is "--version" or "version")
{
    Console.WriteLine("azdo 1.0.0 — Azure DevOps CLI (REST 7.2)");
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
          azdo            Launch the interactive console (manage profiles and browse resources).
          azdo --help     Show this help.
          azdo --version  Show version information.

        The interactive console supports multiple organizations and projects via named profiles.
        Personal access tokens are stored per-user and protected with Windows DPAPI where available.
        """);
}
