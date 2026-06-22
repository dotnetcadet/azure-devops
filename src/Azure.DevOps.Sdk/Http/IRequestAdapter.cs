using System.Text.Json;
using Azure.DevOps.Sdk.Models;

namespace Azure.DevOps.Sdk.Http;

/// <summary>
/// The transport abstraction the generated request builders depend on. It turns a
/// <see cref="RequestInformation"/> into an HTTP call and deserializes the response. Having a
/// single seam keeps the generated surface tiny and makes the SDK testable (substitute a fake
/// adapter) and retargetable (substitute the host resolver / HTTP stack).
/// </summary>
public interface IRequestAdapter
{
    /// <summary>The JSON options used for (de)serialization.</summary>
    JsonSerializerOptions SerializerOptions { get; }

    /// <summary>Sends the request and deserializes the response body to <typeparamref name="T"/>.</summary>
    Task<T?> SendAsync<T>(RequestInformation request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the request and deserializes a collection response, transparently unwrapping the
    /// Azure DevOps <c>{ count, value }</c> envelope and capturing the continuation token.
    /// </summary>
    Task<PagedList<T>> SendCollectionAsync<T>(RequestInformation request, CancellationToken cancellationToken = default);

    /// <summary>Sends the request and ignores the response body (for 204 / fire-and-forget operations).</summary>
    Task SendNoContentAsync(RequestInformation request, CancellationToken cancellationToken = default);

    /// <summary>Sends the request and returns the raw response stream (for file / binary downloads).</summary>
    Task<Stream> SendStreamAsync(RequestInformation request, CancellationToken cancellationToken = default);
}
