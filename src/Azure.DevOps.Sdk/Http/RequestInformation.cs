namespace Azure.DevOps.Sdk.Http;

/// <summary>
/// An abstract description of an outgoing Azure DevOps request, assembled by a generated
/// request builder and materialized into an <see cref="HttpRequestMessage"/> by the
/// <see cref="IRequestAdapter"/>. Keeping the description declarative (template + parameter
/// bags) lets the adapter own URL composition, escaping, and host resolution.
/// </summary>
public sealed class RequestInformation
{
    /// <summary>The HTTP verb.</summary>
    public HttpMethod HttpMethod { get; set; } = HttpMethod.Get;

    /// <summary>
    /// The logical service the request targets (for example <c>dev.azure.com</c> or
    /// <c>vsrm.dev.azure.com</c>). Resolved to a concrete authority by the adapter's host resolver.
    /// </summary>
    public string Host { get; set; } = "dev.azure.com";

    /// <summary>
    /// The path template with <c>{name}</c> placeholders, taken verbatim from the OpenAPI path
    /// (e.g. <c>/{organization}/{project}/_apis/git/repositories/{repositoryId}</c>).
    /// </summary>
    public string PathTemplate { get; set; } = "/";

    /// <summary>Values substituted into the path template, keyed by placeholder name.</summary>
    public IDictionary<string, object?> PathParameters { get; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Query string values, keyed by query name. Includes <c>api-version</c>.</summary>
    public IDictionary<string, object?> QueryParameters { get; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Additional request headers.</summary>
    public IDictionary<string, string> Headers { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The object to serialize as the request body, if any.</summary>
    public object? Content { get; set; }

    /// <summary>A pre-built body content (e.g. a stream upload) that bypasses JSON serialization.</summary>
    public HttpContent? RawContent { get; set; }

    /// <summary>The body content type. Defaults to <c>application/json</c> when <see cref="Content"/> is set.</summary>
    public string? ContentType { get; set; }

    /// <summary>The Accept header value. Defaults to <c>application/json</c>.</summary>
    public string Accept { get; set; } = "application/json";

    /// <summary>Sets a path parameter and returns this instance for chaining.</summary>
    public RequestInformation WithPathParameter(string name, object? value)
    {
        PathParameters[name] = value;
        return this;
    }

    /// <summary>Sets a query parameter (ignored when <paramref name="value"/> is null) and returns this instance.</summary>
    public RequestInformation WithQueryParameter(string name, object? value)
    {
        if (value is not null)
        {
            QueryParameters[name] = value;
        }

        return this;
    }
}
