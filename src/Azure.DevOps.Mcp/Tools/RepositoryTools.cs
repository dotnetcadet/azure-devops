using System.ComponentModel;
using Azure.DevOps.Sdk.Git.Models;
using ModelContextProtocol.Server;

namespace Azure.DevOps.Mcp.Tools;

[McpServerToolType]
public static class RepositoryTools
{
    [McpServerTool(Name = "azdo_list_projects")]
    [Description("List the projects in the Azure DevOps organization.")]
    public static Task<string> ListProjects(AzureDevOpsContext context, CancellationToken ct = default) =>
        ToolSupport.Run(async () =>
        {
            var projects = await context.Client.Core.Projects.ListAsync(cancellationToken: ct);
            return projects.Select(p => new { id = p.Id, name = p.Name, state = p.State?.ToString(), visibility = p.Visibility?.ToString() });
        });

    [McpServerTool(Name = "azdo_list_repositories")]
    [Description("List the Git repositories in the current project.")]
    public static Task<string> ListRepositories(AzureDevOpsContext context, CancellationToken ct = default) =>
        ToolSupport.Run(async () =>
        {
            var repos = await context.Client.Git.Repositories.ListAsync(cancellationToken: ct);
            return repos.Select(r => new
            {
                id = r.Id,
                name = r.Name,
                defaultBranch = ToolSupport.ShortRef(r.DefaultBranch),
                webUrl = r.WebUrl,
            });
        });

    [McpServerTool(Name = "azdo_list_branches")]
    [Description("List the branches of a repository. If 'repository' is omitted, the repository detected " +
                 "from the current git remote is used.")]
    public static Task<string> ListBranches(
        AzureDevOpsContext context,
        [Description("Repository name or id (optional; defaults to the current repo)")] string? repository = null,
        CancellationToken ct = default) =>
        ToolSupport.Run(async () =>
        {
            var repo = context.ResolveRepository(repository);
            var refs = await context.Client.Git.Repositories[repo].Refs.ListAsync(
                cfg => cfg.QueryParameters.Filter = "heads/", cancellationToken: ct);
            return refs.Select(r => new { name = ToolSupport.ShortRef(r.Name), objectId = r.ObjectId });
        });

    [McpServerTool(Name = "azdo_list_pull_requests")]
    [Description("List pull requests in a repository. 'status' may be active, completed, abandoned, or all " +
                 "(default active). If 'repository' is omitted, the current repo is used.")]
    public static Task<string> ListPullRequests(
        AzureDevOpsContext context,
        [Description("Repository name or id (optional; defaults to the current repo)")] string? repository = null,
        [Description("Filter: active | completed | abandoned | all (optional)")] string? status = null,
        CancellationToken ct = default) =>
        ToolSupport.Run(async () =>
        {
            var repo = context.ResolveRepository(repository);
            var prs = await context.Client.Git.Repositories[repo].PullRequests.GetPullRequestsAsync(cfg =>
            {
                if (TryParseStatus(status, out var parsed))
                {
                    cfg.QueryParameters.SearchCriteriaStatus = parsed;
                }
            }, ct);

            return prs.Select(Project);
        });

    [McpServerTool(Name = "azdo_get_pull_request")]
    [Description("Get a single pull request by id from a repository (defaults to the current repo).")]
    public static Task<string> GetPullRequest(
        AzureDevOpsContext context,
        [Description("The pull request id")] int pullRequestId,
        [Description("Repository name or id (optional; defaults to the current repo)")] string? repository = null,
        CancellationToken ct = default) =>
        ToolSupport.Run(async () =>
        {
            var repo = context.ResolveRepository(repository);
            var pr = await context.Client.Git.Repositories[repo].PullRequests[pullRequestId].GetPullRequestAsync(cancellationToken: ct);
            return pr is null ? new { error = "Pull request not found." } : (object)Project(pr);
        });

    [McpServerTool(Name = "azdo_create_pull_request")]
    [Description("Create a pull request from a source branch into a target branch in a repository " +
                 "(defaults to the current repo). Branch names may be short (e.g. 'feature/x') or full refs.")]
    public static Task<string> CreatePullRequest(
        AzureDevOpsContext context,
        [Description("Source branch (the branch with your changes)")] string sourceBranch,
        [Description("Target branch to merge into (e.g. 'main')")] string targetBranch,
        [Description("Pull request title")] string title,
        [Description("Pull request description (optional)")] string? description = null,
        [Description("Repository name or id (optional; defaults to the current repo)")] string? repository = null,
        CancellationToken ct = default) =>
        ToolSupport.Run(async () =>
        {
            var repo = context.ResolveRepository(repository);
            var created = await context.Client.Git.Repositories[repo].PullRequests.CreateAsync(new GitPullRequest
            {
                SourceRefName = ToolSupport.ToBranchRef(sourceBranch),
                TargetRefName = ToolSupport.ToBranchRef(targetBranch),
                Title = title,
                Description = description,
            }, cancellationToken: ct);

            return created is null ? new { error = "Pull request was not created." } : (object)Project(created);
        });

    private static object Project(GitPullRequest pr) => new
    {
        id = pr.PullRequestId,
        title = pr.Title,
        status = pr.Status?.ToString(),
        isDraft = pr.IsDraft,
        author = pr.CreatedBy?.DisplayName,
        sourceBranch = ToolSupport.ShortRef(pr.SourceRefName),
        targetBranch = ToolSupport.ShortRef(pr.TargetRefName),
        creationDate = pr.CreationDate,
    };

    private static bool TryParseStatus(string? status, out PullRequestStatus parsed)
    {
        parsed = default;
        return !string.IsNullOrWhiteSpace(status) && Enum.TryParse(status, ignoreCase: true, out parsed);
    }
}
