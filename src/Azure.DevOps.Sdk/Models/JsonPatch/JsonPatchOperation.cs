using System.Text.Json.Serialization;

namespace Azure.DevOps.Sdk.Models;

/// <summary>
/// A single operation within a <see cref="JsonPatchDocument"/>.
/// </summary>
public sealed class JsonPatchOperation
{
    /// <summary>The patch operation to perform.</summary>
    [JsonPropertyName("op")]
    public JsonPatchOperationType Op { get; set; }

    /// <summary>The path to the target field (e.g. <c>/fields/System.Title</c>).</summary>
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    /// <summary>The source path for <c>move</c> and <c>copy</c> operations.</summary>
    [JsonPropertyName("from")]
    public string? From { get; set; }

    /// <summary>The value to set. May be any JSON-serializable value.</summary>
    [JsonPropertyName("value")]
    public object? Value { get; set; }

    public JsonPatchOperation()
    {
    }

    public JsonPatchOperation(JsonPatchOperationType op, string path, object? value)
    {
        Op = op;
        Path = path;
        Value = value;
    }
}
