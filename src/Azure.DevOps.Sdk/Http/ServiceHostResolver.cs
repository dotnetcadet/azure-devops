namespace Azure.DevOps.Sdk.Http;

/// <summary>
/// Resolves the logical service host declared by an operation (e.g. <c>vsrm.dev.azure.com</c>)
/// into the absolute base URI used for the request. The default implementation simply prefixes
/// <c>https://</c>, but it accepts a remap table so the same SDK can target Azure DevOps Server
/// (on-premises) or sovereign clouds by redirecting every cloud host to a custom base.
/// </summary>
public sealed class ServiceHostResolver
{
    private readonly IReadOnlyDictionary<string, string>? _overrides;

    /// <summary>
    /// Creates a resolver.
    /// </summary>
    /// <param name="overrides">
    /// Optional map from the operation's declared host to a replacement absolute base URI
    /// (scheme + authority, optionally with a path prefix), e.g.
    /// <c>{ "dev.azure.com": "https://tfs.contoso.com/DefaultCollection" }</c>.
    /// </param>
    public ServiceHostResolver(IReadOnlyDictionary<string, string>? overrides = null)
    {
        _overrides = overrides;
    }

    /// <summary>The default cloud resolver.</summary>
    public static ServiceHostResolver Default { get; } = new();

    /// <summary>Resolves the absolute base URI (no trailing slash) for the given declared host.</summary>
    public string Resolve(string declaredHost)
    {
        if (_overrides is not null && _overrides.TryGetValue(declaredHost, out var replacement))
        {
            return replacement.TrimEnd('/');
        }

        return "https://" + declaredHost;
    }
}
