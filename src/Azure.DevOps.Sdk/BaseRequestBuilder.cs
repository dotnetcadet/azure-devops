using Azure.DevOps.Sdk.Http;

namespace Azure.DevOps.Sdk;

/// <summary>
/// The base type for every generated request builder. It carries the request adapter and the
/// path parameters accumulated so far along the fluent chain (organization, project, and any
/// resource identifiers selected via indexers). Builders are immutable and cheap to allocate,
/// which is what makes <c>client.Git.Repositories[id].PullRequests[n]</c> style chaining safe.
/// </summary>
public abstract class BaseRequestBuilder
{
    protected BaseRequestBuilder(IRequestAdapter adapter, IReadOnlyDictionary<string, object?> pathParameters)
    {
        Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        PathParameters = pathParameters ?? throw new ArgumentNullException(nameof(pathParameters));
    }

    /// <summary>The transport used to execute requests built from this node.</summary>
    protected IRequestAdapter Adapter { get; }

    /// <summary>The path parameters captured up to this node in the chain.</summary>
    protected IReadOnlyDictionary<string, object?> PathParameters { get; }

    /// <summary>Returns a copy of the current path parameters with an additional value, for descending into a child builder.</summary>
    protected Dictionary<string, object?> AppendPathParameter(string name, object? value)
    {
        var clone = new Dictionary<string, object?>(PathParameters, StringComparer.Ordinal)
        {
            [name] = value,
        };
        return clone;
    }

    /// <summary>Seeds a new <see cref="RequestInformation"/> with this node's adapter context and path parameters.</summary>
    protected RequestInformation CreateRequest(HttpMethod method, string host, string pathTemplate)
    {
        var request = new RequestInformation
        {
            HttpMethod = method,
            Host = host,
            PathTemplate = pathTemplate,
        };

        foreach (var pair in PathParameters)
        {
            request.PathParameters[pair.Key] = pair.Value;
        }

        return request;
    }
}
