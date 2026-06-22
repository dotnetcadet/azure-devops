namespace Azure.DevOps.Sdk.Http;

/// <summary>
/// Resolves the logical service host declared by an operation (e.g. <c>vsrm.dev.azure.com</c>)
/// into the absolute base URI used for the request.
/// </summary>
/// <remarks>
/// Three behaviors are supported:
/// <list type="bullet">
/// <item><b>Cloud (default)</b> — prefixes <c>https://</c>, so each Azure DevOps service keeps its
/// own subdomain (release on <c>vsrm.dev.azure.com</c>, graph on <c>vssps.dev.azure.com</c>, …).</item>
/// <item><b>Overrides</b> — a map from a declared host to a replacement base, for redirecting
/// individual services.</item>
/// <item><b>Collapse</b> — every service resolves to a single base URI. This is how Azure DevOps
/// Server (on-premises) works, where all services are served from one collection URL.</item>
/// </list>
/// </remarks>
public sealed class ServiceHostResolver
{
    private readonly IReadOnlyDictionary<string, string>? _overrides;
    private readonly string? _collapseBase;

    /// <summary>Creates a cloud resolver, optionally with per-host overrides.</summary>
    /// <param name="overrides">
    /// Optional map from the operation's declared host to a replacement absolute base URI.
    /// </param>
    public ServiceHostResolver(IReadOnlyDictionary<string, string>? overrides = null)
    {
        _overrides = overrides;
    }

    private ServiceHostResolver(string collapseBase)
    {
        _collapseBase = collapseBase.TrimEnd('/');
    }

    /// <summary>The default cloud resolver.</summary>
    public static ServiceHostResolver Default { get; } = new();

    /// <summary>
    /// Creates a resolver that routes every service to a single base URI — used for Azure DevOps
    /// Server (on-premises) and any deployment where all services share one host.
    /// </summary>
    public static ServiceHostResolver CollapseTo(string baseUri) => new(baseUri);

    /// <summary>Resolves the absolute base URI (no trailing slash) for the given declared host.</summary>
    public string Resolve(string declaredHost)
    {
        if (_collapseBase is not null)
        {
            return _collapseBase;
        }

        if (_overrides is not null && _overrides.TryGetValue(declaredHost, out var replacement))
        {
            return replacement.TrimEnd('/');
        }

        return "https://" + declaredHost;
    }
}
