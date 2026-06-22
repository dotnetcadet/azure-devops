using Azure.DevOps.Cli.Ui;
using Azure.DevOps.Sdk.Models;
using Sharprompt;

namespace Azure.DevOps.Cli.Commands;

/// <summary>The SDK-backed feature actions surfaced in the main menu.</summary>
public static class Features
{
    public static Task ListProjectsAsync(AzdoSession session) => RunSafely(async () =>
    {
        ConsoleUx.Heading($"Projects in '{session.Current!.Organization}'");
        var projects = await session.Client.Core.Projects.ListAsync();
        ConsoleUx.Table(
            new[] { "Name", "Id", "State", "Visibility" },
            projects.Select(p => (IReadOnlyList<string?>)new[]
            {
                p.Name, p.Id?.ToString(), p.State?.ToString(), p.Visibility?.ToString(),
            }).ToList());

        if (projects.Count > 0 && Prompt.Confirm("Set one as the active project?", defaultValue: false))
        {
            var chosen = Prompt.Select("Project", projects, pageSize: 15, textSelector: p => p.Name ?? "(unnamed)");
            if (chosen.Name is not null)
            {
                session.UseProject(chosen.Name);
                ConsoleUx.Success($"Active project set to '{chosen.Name}'.");
            }
        }
    });

    public static Task ListRepositoriesAsync(AzdoSession session) => RunSafely(async () =>
    {
        if (!await EnsureProjectAsync(session))
        {
            return;
        }

        ConsoleUx.Heading($"Repositories in '{session.ActiveProject}'");
        var repos = await session.Client.Git.Repositories.ListAsync();
        ConsoleUx.Table(
            new[] { "Name", "Default branch", "Id" },
            repos.Select(r => (IReadOnlyList<string?>)new[]
            {
                r.Name, r.DefaultBranch?.Replace("refs/heads/", string.Empty), r.Id?.ToString(),
            }).ToList());
    });

    public static Task ListPullRequestsAsync(AzdoSession session) => RunSafely(async () =>
    {
        if (!await EnsureProjectAsync(session))
        {
            return;
        }

        var repos = await session.Client.Git.Repositories.ListAsync();
        if (repos.Count == 0)
        {
            ConsoleUx.Warn("No repositories in this project.");
            return;
        }

        var repo = Prompt.Select("Repository", repos, pageSize: 15, textSelector: r => r.Name ?? "(unnamed)");
        ConsoleUx.Heading($"Pull requests in '{repo.Name}'");

        var prs = await session.Client.Git.Repositories[repo.Id?.ToString() ?? repo.Name!]
            .PullRequests.GetPullRequestsAsync();

        ConsoleUx.Table(
            new[] { "Id", "Title", "Status", "Author", "Source → Target" },
            prs.Select(pr => (IReadOnlyList<string?>)new[]
            {
                pr.PullRequestId?.ToString(),
                pr.Title,
                pr.Status?.ToString(),
                pr.CreatedBy?.DisplayName,
                $"{Short(pr.SourceRefName)} → {Short(pr.TargetRefName)}",
            }).ToList());
    });

    public static Task ListBuildsAsync(AzdoSession session) => RunSafely(async () =>
    {
        if (!await EnsureProjectAsync(session))
        {
            return;
        }

        ConsoleUx.Heading($"Recent builds in '{session.ActiveProject}'");
        var builds = await session.Client.Build.Builds.ListAsync();
        ConsoleUx.Table(
            new[] { "Id", "Number", "Definition", "Status", "Result" },
            builds.Take(25).Select(b => (IReadOnlyList<string?>)new[]
            {
                b.Id?.ToString(), b.BuildNumber, b.Definition?.Name, b.Status?.ToString(), b.Result?.ToString(),
            }).ToList());
    });

    public static Task GetWorkItemAsync(AzdoSession session) => RunSafely(async () =>
    {
        var id = Prompt.Input<string>("Work item id", validators: new[] { Validators.Required() });
        var workItem = await session.Client.WorkItemTracking.WorkItems[id.Trim()].GetWorkItemAsync();
        if (workItem is null)
        {
            ConsoleUx.Warn("Not found.");
            return;
        }

        ConsoleUx.Heading($"Work item #{workItem.Id}");
        ConsoleUx.Table(
            new[] { "Field", "Value" },
            new IReadOnlyList<string?>[]
            {
                new string?[] { "Type", Field(workItem.Fields, "System.WorkItemType") },
                new string?[] { "Title", Field(workItem.Fields, "System.Title") },
                new string?[] { "State", Field(workItem.Fields, "System.State") },
                new string?[] { "Assigned to", Field(workItem.Fields, "System.AssignedTo") },
            });
    });

    public static Task CreateWorkItemAsync(AzdoSession session) => RunSafely(async () =>
    {
        if (!await EnsureProjectAsync(session))
        {
            return;
        }

        var type = Prompt.Input<string>("Work item type", defaultValue: "Task", validators: new[] { Validators.Required() });
        var title = Prompt.Input<string>("Title", validators: new[] { Validators.Required() });
        var description = Prompt.Input<string>("Description (optional)", defaultValue: string.Empty);

        var patch = new JsonPatchDocument().Add("/fields/System.Title", title.Trim());
        if (!string.IsNullOrWhiteSpace(description))
        {
            patch.Add("/fields/System.Description", description.Trim());
        }

        if (!Prompt.Confirm($"Create a '{type}' titled \"{title}\" in '{session.ActiveProject}'?", defaultValue: true))
        {
            return;
        }

        var created = await session.Client.WorkItemTracking.WorkItems[type.Trim()].CreateAsync(patch);
        ConsoleUx.Success($"Created {type} #{created?.Id}.");
    });

    private static async Task<bool> EnsureProjectAsync(AzdoSession session)
    {
        if (!string.IsNullOrWhiteSpace(session.ActiveProject))
        {
            return true;
        }

        ConsoleUx.Info("This action needs a project. Loading projects...");
        var projects = await session.Client.Core.Projects.ListAsync();
        if (projects.Count == 0)
        {
            ConsoleUx.Warn("No projects available.");
            return false;
        }

        var chosen = Prompt.Select("Select a project to use", projects, pageSize: 15, textSelector: p => p.Name ?? "(unnamed)");
        if (chosen.Name is null)
        {
            return false;
        }

        session.UseProject(chosen.Name);
        ConsoleUx.Success($"Using project '{chosen.Name}'.");
        return true;
    }

    private static string Field(Dictionary<string, object>? fields, string key) =>
        fields is not null && fields.TryGetValue(key, out var value) ? FormatFieldValue(value) : "-";

    private static string FormatFieldValue(object? value)
    {
        if (value is System.Text.Json.JsonElement element)
        {
            return element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => element.GetString() ?? "-",
                System.Text.Json.JsonValueKind.Object => element.TryGetProperty("displayName", out var dn)
                    ? dn.GetString() ?? element.ToString()
                    : element.ToString(),
                _ => element.ToString(),
            };
        }

        return value?.ToString() ?? "-";
    }

    private static string Short(string? refName) =>
        string.IsNullOrEmpty(refName) ? "-" : refName.Replace("refs/heads/", string.Empty);

    private static async Task RunSafely(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (VssServiceException ex)
        {
            ConsoleUx.Error($"Azure DevOps error (HTTP {(int)ex.StatusCode}): {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            ConsoleUx.Error($"Network error: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            ConsoleUx.Error("The request timed out or was cancelled.");
        }
        catch (PlatformNotSupportedException ex)
        {
            ConsoleUx.Error($"{ex.Message} Re-run 'Profile — add / sign in' to re-enter the token on this OS.");
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            ConsoleUx.Error("Could not decrypt the stored token. Re-add the profile to re-enter the token.");
        }
        catch (InvalidOperationException ex)
        {
            ConsoleUx.Error(ex.Message);
        }
        catch (PromptCanceledException)
        {
            // user pressed Esc/Ctrl-C inside a prompt; just return to the menu.
        }
    }
}
