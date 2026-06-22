using System.Net.Http.Headers;

namespace Azure.DevOps.Sdk.Authentication;

/// <summary>
/// Authenticates requests using an OAuth 2.0 / Microsoft Entra ID bearer access token.
/// </summary>
/// <remarks>
/// This credential is the extension point for every token-based flow. Supply either a
/// static token or a delegate that produces (and refreshes) a token on demand — the
/// delegate is invoked on every request so callers can plug in
/// <c>Azure.Core.TokenCredential</c>, MSAL, device-code, or a custom refresh routine
/// without this library taking a dependency on any particular identity stack.
/// </remarks>
public sealed class BearerTokenCredential : IAzureDevOpsCredential
{
    private readonly Func<CancellationToken, ValueTask<string>> _tokenProvider;

    /// <summary>
    /// Initializes a credential backed by a static bearer token.
    /// </summary>
    /// <param name="accessToken">The bearer access token.</param>
    public BearerTokenCredential(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ArgumentException("An access token is required.", nameof(accessToken));
        }

        _tokenProvider = _ => ValueTask.FromResult(accessToken);
    }

    /// <summary>
    /// Initializes a credential backed by a token provider delegate, invoked per request.
    /// </summary>
    /// <param name="tokenProvider">A delegate that asynchronously produces a fresh bearer token.</param>
    public BearerTokenCredential(Func<CancellationToken, ValueTask<string>> tokenProvider)
    {
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
    }

    /// <inheritdoc />
    public async ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        var token = await _tokenProvider(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
