using System.Collections;

namespace Azure.DevOps.Sdk.Models;

/// <summary>
/// A read-only list returned by a collection endpoint. In addition to the items, it
/// exposes the <see cref="ContinuationToken"/> emitted by Azure DevOps (via the
/// <c>x-ms-continuationtoken</c> response header) so callers can request the next page.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class PagedList<T> : IReadOnlyList<T>
{
    private readonly IReadOnlyList<T> _items;

    public PagedList(IReadOnlyList<T> items, string? continuationToken = null)
    {
        _items = items ?? Array.Empty<T>();
        ContinuationToken = continuationToken;
    }

    /// <summary>The continuation token for the next page, or <c>null</c> when there are no more pages.</summary>
    public string? ContinuationToken { get; }

    /// <summary><c>true</c> when a continuation token is present, indicating more results are available.</summary>
    public bool HasMore => !string.IsNullOrEmpty(ContinuationToken);

    public T this[int index] => _items[index];

    public int Count => _items.Count;

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal static PagedList<T> Empty { get; } = new(Array.Empty<T>());
}
