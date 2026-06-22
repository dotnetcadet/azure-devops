using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.DevOps.Sdk.Models;

namespace Azure.DevOps.Mcp;

/// <summary>Compact JSON for tool results, and uniform error handling so tools never throw to the host.</summary>
internal static class ToolSupport
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string Serialize(object? value) => JsonSerializer.Serialize(value, Options);

    /// <summary>Runs a tool body, serializing the result or a clean error object the model can read.</summary>
    public static async Task<string> Run(Func<Task<object?>> action)
    {
        try
        {
            return Serialize(await action().ConfigureAwait(false));
        }
        catch (VssServiceException ex)
        {
            return Serialize(new { error = $"Azure DevOps returned HTTP {(int)ex.StatusCode}: {ex.Message}", typeKey = ex.TypeKey });
        }
        catch (HttpRequestException ex)
        {
            return Serialize(new { error = $"Network error reaching Azure DevOps: {ex.Message}" });
        }
        catch (InvalidOperationException ex)
        {
            return Serialize(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Serialize(new { error = ex.Message });
        }
    }

    /// <summary>Normalizes a branch name to a full ref (e.g. <c>main</c> → <c>refs/heads/main</c>).</summary>
    public static string ToBranchRef(string branch) =>
        branch.StartsWith("refs/", StringComparison.OrdinalIgnoreCase) ? branch : "refs/heads/" + branch;

    /// <summary>Shortens a full ref for display (<c>refs/heads/main</c> → <c>main</c>).</summary>
    public static string? ShortRef(string? refName) =>
        refName?.Replace("refs/heads/", string.Empty);
}
