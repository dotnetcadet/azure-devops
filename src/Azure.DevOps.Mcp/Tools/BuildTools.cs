using System.ComponentModel;
using Azure.DevOps.Sdk.Build.Models;
using ModelContextProtocol.Server;

namespace Azure.DevOps.Mcp.Tools;

[McpServerToolType]
public static class BuildTools
{
    [McpServerTool(Name = "azdo_list_pipelines")]
    [Description("List build pipeline definitions in the current project (id + name + folder path). " +
                 "Use the id with azdo_run_pipeline.")]
    public static Task<string> ListPipelines(AzureDevOpsContext context, CancellationToken ct = default) =>
        ToolSupport.Run(async () =>
        {
            var definitions = await context.Client.Build.Definitions.ListAsync(cancellationToken: ct);
            return definitions.Select(d => new { id = d.Id, name = d.Name, path = d.Path });
        });

    [McpServerTool(Name = "azdo_list_builds")]
    [Description("List recent builds in the current project.")]
    public static Task<string> ListBuilds(
        AzureDevOpsContext context,
        [Description("Maximum number of builds to return (default 25)")] int? top = null,
        CancellationToken ct = default) =>
        ToolSupport.Run(async () =>
        {
            var builds = await context.Client.Build.Builds.ListAsync(cancellationToken: ct);
            return builds.Take(top is > 0 ? top.Value : 25).Select(Project);
        });

    [McpServerTool(Name = "azdo_get_build")]
    [Description("Get a single build by id.")]
    public static Task<string> GetBuild(
        AzureDevOpsContext context,
        [Description("The build id")] int buildId,
        CancellationToken ct = default) =>
        ToolSupport.Run(async () =>
        {
            var build = await context.Client.Build.Builds[buildId].GetAsync(cancellationToken: ct);
            return build is null ? new { error = "Build not found." } : (object)Project(build);
        });

    [McpServerTool(Name = "azdo_run_pipeline")]
    [Description("Queue (run) a build pipeline by its definition id, optionally on a specific branch " +
                 "(short name like 'main' or a full ref). Returns the queued build.")]
    public static Task<string> RunPipeline(
        AzureDevOpsContext context,
        [Description("The pipeline/definition id (from azdo_list_pipelines)")] int definitionId,
        [Description("Branch to build (optional; defaults to the pipeline's default branch)")] string? branch = null,
        CancellationToken ct = default) =>
        ToolSupport.Run(async () =>
        {
            var build = new Build
            {
                Definition = new DefinitionReference { Id = definitionId },
                SourceBranch = string.IsNullOrWhiteSpace(branch) ? null : ToolSupport.ToBranchRef(branch),
            };

            var queued = await context.Client.Build.Builds.QueueAsync(build, cancellationToken: ct);
            return queued is null ? new { error = "Build was not queued." } : (object)Project(queued);
        });

    private static object Project(Build b) => new
    {
        id = b.Id,
        buildNumber = b.BuildNumber,
        definition = b.Definition?.Name,
        status = b.Status?.ToString(),
        result = b.Result?.ToString(),
        sourceBranch = ToolSupport.ShortRef(b.SourceBranch),
        requestedFor = b.RequestedFor?.DisplayName,
        queueTime = b.QueueTime,
    };
}
