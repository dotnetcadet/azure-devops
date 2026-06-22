using System.Net;

namespace Azure.DevOps.Sdk.Http;

/// <summary>
/// Retries transient failures with exponential backoff, honoring the <c>Retry-After</c> header
/// that Azure DevOps returns when a request is throttled (HTTP 429) or the service is briefly
/// unavailable (HTTP 503).
/// </summary>
public sealed class RetryHandler : DelegatingHandler
{
    private readonly int _maxRetries;
    private readonly TimeSpan _baseDelay;

    public RetryHandler(int maxRetries = 5, TimeSpan? baseDelay = null)
    {
        _maxRetries = Math.Max(0, maxRetries);
        _baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(500);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage? response = null;

        // Buffer the request body up front so it can be replayed on a retry. Without this, a
        // non-seekable upload stream would already be consumed by the time we resend.
        if (request.Content is not null)
        {
            try
            {
                await request.Content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // If the content cannot be buffered we simply won't retry a body request.
            }
        }

        for (var attempt = 0; ; attempt++)
        {
            response?.Dispose();
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (attempt >= _maxRetries || !IsTransient(response.StatusCode))
            {
                return response;
            }

            var delay = GetRetryAfter(response) ?? ExponentialBackoff(attempt);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.TooManyRequests ||
        statusCode == HttpStatusCode.ServiceUnavailable ||
        statusCode == HttpStatusCode.BadGateway ||
        statusCode == HttpStatusCode.GatewayTimeout;

    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
    }

    private TimeSpan ExponentialBackoff(int attempt) =>
        TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
}
