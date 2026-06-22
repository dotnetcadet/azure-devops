using System.Text.Json.Serialization;

namespace Azure.DevOps.Sdk.Models;

/// <summary>
/// The standard error payload returned by Azure DevOps services on a failed request.
/// </summary>
public sealed class VssServiceError
{
    [JsonPropertyName("$id")]
    public string? Id { get; set; }

    [JsonPropertyName("innerException")]
    public VssServiceError? InnerException { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("typeName")]
    public string? TypeName { get; set; }

    [JsonPropertyName("typeKey")]
    public string? TypeKey { get; set; }

    [JsonPropertyName("errorCode")]
    public int? ErrorCode { get; set; }

    [JsonPropertyName("eventId")]
    public int? EventId { get; set; }
}
