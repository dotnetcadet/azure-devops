using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Azure.DevOps.Mcp.Tools;

[McpServerToolType]
public static class ContextTools
{
    [McpServerTool(Name = "azdo_get_context")]
    [Description("Show the Azure DevOps connection in effect for this repository: the organization, " +
                 "project, and default repository, plus where each value was resolved from " +
                 "(environment variable, git remote, or the saved azdo CLI profile). Call this first " +
                 "when unsure what the other tools will act on.")]
    public static string GetContext(AzureDevOpsContext context) => ToolSupport.Serialize(context.Describe());
}
