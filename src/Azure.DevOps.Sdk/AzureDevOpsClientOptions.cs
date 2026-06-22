using System.Text.Json;
using Azure.DevOps.Sdk.Authentication;

namespace Azure.DevOps.Sdk;

/// <summary>
/// Configuration for an <see cref="AzureDevOpsClient"/>.
/// </summary>
public sealed class AzureDevOpsClientOptions
{
    /// <summary>
    /// The Azure DevOps organization name (the <c>{organization}</c> path segment), e.g. <c>contoso</c>.
    /// Required for the vast majority of endpoints.
    /// </summary>
    public string? Organization { get; set; }

    /// <summary>
    /// An optional default project (name or id). Endpoints that are project-scoped use this value
    /// unless overridden via <see cref="AzureDevOpsClient.WithProject"/>.
    /// </summary>
    public string? Project { get; set; }

    /// <summary>The credential used to authenticate requests. Required unless a custom <see cref="HttpClient"/> already authenticates.</summary>
    public IAzureDevOpsCredential? Credential { get; set; }

    /// <summary>
    /// Optional overrides from a declared service host (e.g. <c>dev.azure.com</c>) to a replacement
    /// base URI, enabling Azure DevOps Server (on-premises) or sovereign-cloud targeting.
    /// </summary>
    public IReadOnlyDictionary<string, string>? HostOverrides { get; set; }

    /// <summary>An optional caller-supplied <see cref="HttpClient"/>. When set, the SDK does not create or dispose it.</summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>Optional custom JSON serialization options. Defaults to the SDK's tuned options.</summary>
    public JsonSerializerOptions? SerializerOptions { get; set; }
}
