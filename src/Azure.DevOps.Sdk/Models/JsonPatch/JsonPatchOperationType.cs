using System.Text.Json.Serialization;

namespace Azure.DevOps.Sdk.Models;

/// <summary>
/// The set of operations supported by a JSON Patch document (RFC 6902), as used by
/// Azure DevOps work item and other patch-based endpoints.
/// </summary>
public enum JsonPatchOperationType
{
    [JsonStringEnumMemberName("add")]
    Add,

    [JsonStringEnumMemberName("copy")]
    Copy,

    [JsonStringEnumMemberName("move")]
    Move,

    [JsonStringEnumMemberName("remove")]
    Remove,

    [JsonStringEnumMemberName("replace")]
    Replace,

    [JsonStringEnumMemberName("test")]
    Test,
}
