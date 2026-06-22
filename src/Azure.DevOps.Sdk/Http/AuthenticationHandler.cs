using Azure.DevOps.Sdk.Authentication;

namespace Azure.DevOps.Sdk.Http;

/// <summary>
/// A <see cref="DelegatingHandler"/> that applies the configured
/// <see cref="IAzureDevOpsCredential"/> to every outgoing request. Running as a handler (rather
/// than baking the header in once) means token-based credentials can refresh per attempt.
/// </summary>
public sealed class AuthenticationHandler : DelegatingHandler
{
    private readonly IAzureDevOpsCredential _credential;

    public AuthenticationHandler(IAzureDevOpsCredential credential)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
    }

    public AuthenticationHandler(IAzureDevOpsCredential credential, HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await _credential.AuthenticateAsync(request, cancellationToken).ConfigureAwait(false);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
