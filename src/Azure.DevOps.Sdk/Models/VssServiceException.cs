using System.Net;

namespace Azure.DevOps.Sdk.Models;

/// <summary>
/// Thrown when an Azure DevOps REST request returns a non-success status code.
/// Carries the HTTP status, the parsed <see cref="VssServiceError"/> (when available),
/// and the raw response body for diagnostics.
/// </summary>
public sealed class VssServiceException : Exception
{
    public VssServiceException(
        HttpStatusCode statusCode,
        string? typeKey,
        string message,
        VssServiceError? error,
        string? rawBody)
        : base(message)
    {
        StatusCode = statusCode;
        TypeKey = typeKey;
        Error = error;
        RawBody = rawBody;
    }

    /// <summary>The HTTP status code returned by the service.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>The Azure DevOps exception type key (e.g. <c>GitRepositoryNotFoundException</c>), when present.</summary>
    public string? TypeKey { get; }

    /// <summary>The parsed error payload, when the body was a recognizable VSS error.</summary>
    public VssServiceError? Error { get; }

    /// <summary>The raw response body, useful when the payload was not a standard error.</summary>
    public string? RawBody { get; }
}
