using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.DevOps.Sdk.Authentication;
using Azure.DevOps.Sdk.Models;
using Azure.DevOps.Sdk.Serialization;

namespace Azure.DevOps.Sdk.Http;

/// <summary>
/// The default <see cref="IRequestAdapter"/>: composes absolute URLs from a
/// <see cref="RequestInformation"/>, sends them over an <see cref="HttpClient"/> wired with the
/// authentication and retry handlers, and deserializes responses (including the Azure DevOps
/// collection envelope and continuation-token pagination).
/// </summary>
public sealed class AzureDevOpsRequestAdapter : IRequestAdapter, IDisposable
{
    private const string ContinuationTokenHeader = "x-ms-continuationtoken";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly ServiceHostResolver _hostResolver;
    private readonly IAzureDevOpsCredential? _credential;

    /// <summary>Creates an adapter that owns an <see cref="HttpClient"/> built from the credential.</summary>
    public AzureDevOpsRequestAdapter(
        IAzureDevOpsCredential credential,
        ServiceHostResolver? hostResolver = null,
        JsonSerializerOptions? serializerOptions = null)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _hostResolver = hostResolver ?? ServiceHostResolver.Default;
        SerializerOptions = serializerOptions ?? AzureDevOpsJson.Default;

        var pipeline = new RetryHandler
        {
            InnerHandler = new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
            },
        };

        _httpClient = new HttpClient(pipeline, disposeHandler: true);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("azure-devops-sdk-dotnet/1.0");
        _ownsHttpClient = true;
    }

    /// <summary>
    /// Creates an adapter over a caller-supplied <see cref="HttpClient"/> (not disposed by the adapter).
    /// When a <paramref name="credential"/> is supplied, the adapter authenticates each request itself,
    /// so authentication works even though the supplied client's handler pipeline is fixed.
    /// </summary>
    public AzureDevOpsRequestAdapter(
        HttpClient httpClient,
        ServiceHostResolver? hostResolver = null,
        JsonSerializerOptions? serializerOptions = null,
        IAzureDevOpsCredential? credential = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _hostResolver = hostResolver ?? ServiceHostResolver.Default;
        SerializerOptions = serializerOptions ?? AzureDevOpsJson.Default;
        _credential = credential;
        _ownsHttpClient = false;
    }

    /// <inheritdoc />
    public JsonSerializerOptions SerializerOptions { get; }

    /// <inheritdoc />
    public async Task<T?> SendAsync<T>(RequestInformation request, CancellationToken cancellationToken = default)
    {
        using var response = await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(bytes, SerializerOptions);
    }

    /// <inheritdoc />
    public async Task<PagedList<T>> SendCollectionAsync<T>(RequestInformation request, CancellationToken cancellationToken = default)
    {
        using var response = await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var continuationToken = response.Headers.TryGetValues(ContinuationTokenHeader, out var values)
            ? values.FirstOrDefault()
            : null;

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0)
        {
            return new PagedList<T>(Array.Empty<T>(), continuationToken);
        }

        var items = DeserializeCollection<T>(bytes);
        return new PagedList<T>(items, continuationToken);
    }

    /// <inheritdoc />
    public async Task SendNoContentAsync(RequestInformation request, CancellationToken cancellationToken = default)
    {
        using var response = await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Stream> SendStreamAsync(RequestInformation request, CancellationToken cancellationToken = default)
    {
        var response = await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

            // Buffer into memory so the stream outlives the response message.
            var buffer = new MemoryStream();
            await response.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;
            return buffer;
        }
        finally
        {
            response.Dispose();
        }
    }

    private List<T> DeserializeCollection<T>(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        // Bare array response.
        if (root.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<T>>(root.GetRawText(), SerializerOptions) ?? new List<T>();
        }

        // The standard { "count": n, "value": [...] } envelope.
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("value", out var valueElement)
            && valueElement.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<T>>(valueElement.GetRawText(), SerializerOptions) ?? new List<T>();
        }

        return new List<T>();
    }

    private async Task<HttpResponseMessage> SendCoreAsync(RequestInformation request, CancellationToken cancellationToken)
    {
        var uri = BuildUri(request);
        using var message = new HttpRequestMessage(request.HttpMethod, uri);

        if (!string.IsNullOrEmpty(request.Accept))
        {
            message.Headers.Accept.ParseAdd(request.Accept);
        }

        foreach (var header in request.Headers)
        {
            message.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        message.Content = BuildContent(request);

        if (_credential is not null)
        {
            await _credential.AuthenticateAsync(message, cancellationToken).ConfigureAwait(false);
        }

        return await _httpClient
            .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
    }

    private HttpContent? BuildContent(RequestInformation request)
    {
        if (request.RawContent is not null)
        {
            return request.RawContent;
        }

        if (request.Content is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(request.Content, request.Content.GetType(), SerializerOptions);
        var contentType = string.IsNullOrEmpty(request.ContentType) ? "application/json" : request.ContentType;
        var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return content;
    }

    private Uri BuildUri(RequestInformation request)
    {
        var baseUri = _hostResolver.Resolve(request.Host);
        var path = SubstitutePath(request.PathTemplate, request.PathParameters);

        var builder = new StringBuilder(baseUri);
        builder.Append(path);

        var first = true;
        foreach (var pair in request.QueryParameters)
        {
            var formatted = RequestValueFormatter.Format(pair.Value);
            if (formatted is null)
            {
                continue;
            }

            builder.Append(first ? '?' : '&');
            builder.Append(Uri.EscapeDataString(pair.Key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(formatted));
            first = false;
        }

        return new Uri(builder.ToString(), UriKind.Absolute);
    }

    private static string SubstitutePath(string template, IDictionary<string, object?> pathParameters)
    {
        var result = new StringBuilder(template.Length);
        var i = 0;
        while (i < template.Length)
        {
            var c = template[i];
            if (c == '{')
            {
                var end = template.IndexOf('}', i + 1);
                if (end < 0)
                {
                    result.Append(template, i, template.Length - i);
                    break;
                }

                var name = template.Substring(i + 1, end - i - 1);
                if (!pathParameters.TryGetValue(name, out var value) || value is null)
                {
                    throw new InvalidOperationException(
                        $"The path parameter '{name}' is required for this request but was not supplied. " +
                        "If this is the organization or project, ensure it is set on the client or via WithProject/WithOrganization.");
                }

                var formatted = RequestValueFormatter.Format(value) ?? string.Empty;
                result.Append(Uri.EscapeDataString(formatted));
                i = end + 1;
            }
            else
            {
                result.Append(c);
                i++;
            }
        }

        return result.ToString();
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? body = null;
        VssServiceError? error = null;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(body) &&
                (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) ?? body.TrimStart().StartsWith('{')))
            {
                error = JsonSerializer.Deserialize<VssServiceError>(body, SerializerOptions);
            }
        }
        catch
        {
            // Non-JSON or unreadable error body; fall back to status text.
        }

        var message = error?.Message
            ?? $"Azure DevOps request failed with status {(int)response.StatusCode} ({response.StatusCode}).";

        throw new VssServiceException(response.StatusCode, error?.TypeKey, message, error, body);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
