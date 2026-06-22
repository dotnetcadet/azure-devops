namespace Azure.DevOps.Sdk.Authentication;

/// <summary>
/// Represents a credential capable of authenticating an outgoing Azure DevOps
/// REST request. Implementations apply whatever headers (or other mutations) are
/// required for their authentication scheme.
/// </summary>
/// <remarks>
/// PAT authentication is provided out of the box via <see cref="PatCredential"/>.
/// The abstraction is deliberately minimal so that additional flows — OAuth 2.0,
/// Microsoft Entra ID (<c>TokenCredential</c>), device code, managed identity —
/// can be layered in later without changing the request pipeline. A
/// <see cref="BearerTokenCredential"/> already covers any flow that can produce a
/// bearer access token.
/// </remarks>
public interface IAzureDevOpsCredential
{
    /// <summary>
    /// Applies this credential to the supplied request immediately before it is sent.
    /// Called once per attempt, allowing implementations to refresh short-lived tokens.
    /// </summary>
    /// <param name="request">The outgoing request to authenticate.</param>
    /// <param name="cancellationToken">A token to observe while awaiting any token acquisition.</param>
    ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}
