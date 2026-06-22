namespace Azure.DevOps.Sdk.Http;

/// <summary>
/// Per-request customization passed to a generated operation. Lets callers add headers
/// or override the request behavior without changing the operation's primary parameters.
/// </summary>
public class RequestConfiguration
{
    /// <summary>Additional headers to send with this request.</summary>
    public IDictionary<string, string> Headers { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Overrides the <c>api-version</c> sent for this request, when set.</summary>
    public string? ApiVersion { get; set; }
}

/// <summary>
/// A <see cref="RequestConfiguration"/> that also exposes the operation's strongly typed
/// query parameters, mirroring the Microsoft Graph SDK request configuration pattern:
/// <code>
/// repositories.GetAsync(config => config.QueryParameters.IncludeLinks = true);
/// </code>
/// </summary>
/// <typeparam name="TQueryParameters">The generated query parameter type for the operation.</typeparam>
public class RequestConfiguration<TQueryParameters> : RequestConfiguration
    where TQueryParameters : class, new()
{
    /// <summary>The strongly typed query parameters for this operation.</summary>
    public TQueryParameters QueryParameters { get; set; } = new();
}
