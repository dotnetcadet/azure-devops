namespace Azure.DevOps.Sdk.Http;

/// <summary>
/// Interprets a full Azure DevOps organization / collection URL and produces the host resolver and
/// the <c>{organization}</c> path value the rest of the SDK needs.
/// </summary>
/// <remarks>
/// Supported forms:
/// <list type="bullet">
/// <item><c>https://dev.azure.com/{org}</c> — cloud; the org is extracted and each service keeps its
/// own <c>*.dev.azure.com</c> subdomain.</item>
/// <item><c>https://{account}.visualstudio.com</c> — legacy cloud; routed through the modern
/// <c>dev.azure.com</c> endpoints using the account name.</item>
/// <item><c>https://server[/virtualDir]/{collection}</c> — Azure DevOps Server (on-premises) or any
/// custom deployment; every service is routed to the collection base.</item>
/// </list>
/// </remarks>
public static class AzureDevOpsUrl
{
    /// <summary>True when a value looks like an absolute http(s) URL rather than a bare org name.</summary>
    public static bool IsUrl(string? value) =>
        value is not null &&
        (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
         value.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    /// <summary>Parses an organization/collection URL into a host resolver and the organization segment.</summary>
    public static (ServiceHostResolver Resolver, string? Organization) Parse(string organizationUrl)
    {
        if (!Uri.TryCreate(organizationUrl.Trim().TrimEnd('/'), UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"'{organizationUrl}' is not a valid absolute URL.", nameof(organizationUrl));
        }

        var host = uri.Host.ToLowerInvariant();
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Cloud: dev.azure.com/{org} — keep per-service subdomain routing.
        if (host == "dev.azure.com")
        {
            return (ServiceHostResolver.Default, segments.Length > 0 ? segments[0] : null);
        }

        // Legacy cloud: {account}.visualstudio.com — route via the modern dev.azure.com endpoints.
        if (host.EndsWith(".visualstudio.com", StringComparison.Ordinal))
        {
            return (ServiceHostResolver.Default, host[..host.IndexOf('.')]);
        }

        // On-premises / custom: collapse every service onto the collection base.
        // The last path segment is the collection ({organization}); everything before it is the base.
        var collection = segments.Length > 0 ? segments[^1] : null;
        var prefix = segments.Length > 1 ? "/" + string.Join('/', segments[..^1]) : string.Empty;
        var collapseBase = $"{uri.Scheme}://{uri.Authority}{prefix}";

        return (ServiceHostResolver.CollapseTo(collapseBase), collection);
    }
}
