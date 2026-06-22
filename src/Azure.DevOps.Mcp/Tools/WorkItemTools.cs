using System.ComponentModel;
using System.Text.Json;
using Azure.DevOps.Sdk.Models;
using Azure.DevOps.Sdk.WorkItemTracking.Models;
using ModelContextProtocol.Server;

namespace Azure.DevOps.Mcp.Tools;

[McpServerToolType]
public static class WorkItemTools
{
    [McpServerTool(Name = "azdo_query_work_items")]
    [Description("Run a WIQL (Work Item Query Language) query in the current project and return the matching " +
                 "work item ids. Example: \"SELECT [System.Id] FROM WorkItems WHERE [System.State] = 'Active'\". " +
                 "Use azdo_get_work_item to fetch full details for an id.")]
    public static Task<string> QueryWorkItems(
        AzureDevOpsContext context,
        [Description("The WIQL query text")] string wiql,
        CancellationToken ct = default) =>
        ToolSupport.Run(async () =>
        {
            var result = await context.Client.WorkItemTracking.Wiql.QueryByWiqlAsync(
                new Wiql { Query = wiql }, cancellationToken: ct);
            return new
            {
                queryType = result?.QueryType?.ToString(),
                count = result?.WorkItems?.Count ?? 0,
                workItemIds = result?.WorkItems?.Select(w => w.Id).ToArray() ?? Array.Empty<int?>(),
            };
        });

    [McpServerTool(Name = "azdo_get_work_item")]
    [Description("Get a work item by id, including all of its fields (title, state, assigned to, etc.).")]
    public static Task<string> GetWorkItem(
        AzureDevOpsContext context,
        [Description("The work item id")] int id,
        CancellationToken ct = default) =>
        ToolSupport.Run(async () =>
        {
            var item = await context.Client.WorkItemTracking.WorkItems[id.ToString()].GetWorkItemAsync(cancellationToken: ct);
            return item is null
                ? new { error = "Work item not found." }
                : (object)new { id = item.Id, rev = item.Rev, fields = item.Fields };
        });

    [McpServerTool(Name = "azdo_create_work_item")]
    [Description("Create a work item of the given type (e.g. Bug, Task, User Story) in the current project.")]
    public static Task<string> CreateWorkItem(
        AzureDevOpsContext context,
        [Description("Work item type, e.g. 'Bug', 'Task', 'User Story'")] string workItemType,
        [Description("Title")] string title,
        [Description("Description (optional)")] string? description = null,
        [Description("Assignee display name or email (optional)")] string? assignedTo = null,
        [Description("Area path (optional)")] string? areaPath = null,
        [Description("Iteration path (optional)")] string? iterationPath = null,
        CancellationToken ct = default) =>
        ToolSupport.Run(async () =>
        {
            var patch = new JsonPatchDocument().Add("/fields/System.Title", title);
            if (!string.IsNullOrWhiteSpace(description)) patch.Add("/fields/System.Description", description);
            if (!string.IsNullOrWhiteSpace(assignedTo)) patch.Add("/fields/System.AssignedTo", assignedTo);
            if (!string.IsNullOrWhiteSpace(areaPath)) patch.Add("/fields/System.AreaPath", areaPath);
            if (!string.IsNullOrWhiteSpace(iterationPath)) patch.Add("/fields/System.IterationPath", iterationPath);

            var created = await context.Client.WorkItemTracking.WorkItems[workItemType].CreateAsync(patch, cancellationToken: ct);
            return new { id = created?.Id, url = created?.Url };
        });

    [McpServerTool(Name = "azdo_update_work_item")]
    [Description("Update fields on a work item. 'fieldsJson' is a JSON object of field reference names to " +
                 "values, e.g. {\"System.State\":\"Active\",\"System.AssignedTo\":\"jane@contoso.com\"}.")]
    public static Task<string> UpdateWorkItem(
        AzureDevOpsContext context,
        [Description("The work item id")] int id,
        [Description("JSON object mapping field reference names to new values")] string fieldsJson,
        CancellationToken ct = default) =>
        ToolSupport.Run(async () =>
        {
            using var doc = JsonDocument.Parse(fieldsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new { error = "fieldsJson must be a JSON object of field name to value." };
            }

            var patch = new JsonPatchDocument();
            foreach (var field in doc.RootElement.EnumerateObject())
            {
                patch.Replace($"/fields/{field.Name}", field.Value.Clone());
            }

            var updated = await context.Client.WorkItemTracking.WorkItems[id.ToString()].UpdateAsync(patch, cancellationToken: ct);
            return new { id = updated?.Id, rev = updated?.Rev, fields = updated?.Fields };
        });

    [McpServerTool(Name = "azdo_add_work_item_comment")]
    [Description("Add a comment to a work item's discussion.")]
    public static Task<string> AddWorkItemComment(
        AzureDevOpsContext context,
        [Description("The work item id")] int id,
        [Description("The comment text")] string text,
        CancellationToken ct = default) =>
        ToolSupport.Run(async () =>
        {
            var comment = await context.Client.WorkItemTracking.WorkItems[id.ToString()].Comments.AddCommentAsync(
                new CommentCreate { Text = text }, cancellationToken: ct);
            return new { id = comment?.Id, createdBy = comment?.CreatedBy?.DisplayName };
        });
}
