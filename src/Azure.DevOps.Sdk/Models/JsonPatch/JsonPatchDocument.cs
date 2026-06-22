namespace Azure.DevOps.Sdk.Models;

/// <summary>
/// A JSON Patch document: an ordered list of <see cref="JsonPatchOperation"/>. Sent with
/// the <c>application/json-patch+json</c> content type. Because it derives from
/// <see cref="List{T}"/>, it serializes to a bare JSON array as the service expects.
/// </summary>
/// <remarks>
/// Fluent helpers make building work-item updates concise:
/// <code>
/// var patch = new JsonPatchDocument()
///     .Add("/fields/System.Title", "New bug")
///     .Add("/fields/System.State", "Active");
/// </code>
/// </remarks>
public sealed class JsonPatchDocument : List<JsonPatchOperation>
{
    public JsonPatchDocument()
    {
    }

    public JsonPatchDocument(IEnumerable<JsonPatchOperation> operations)
        : base(operations)
    {
    }

    /// <summary>The content type required by Azure DevOps for JSON Patch payloads.</summary>
    public const string ContentType = "application/json-patch+json";

    public JsonPatchDocument Add(string path, object? value)
    {
        base.Add(new JsonPatchOperation(JsonPatchOperationType.Add, path, value));
        return this;
    }

    public JsonPatchDocument Replace(string path, object? value)
    {
        base.Add(new JsonPatchOperation(JsonPatchOperationType.Replace, path, value));
        return this;
    }

    public JsonPatchDocument Remove(string path)
    {
        base.Add(new JsonPatchOperation(JsonPatchOperationType.Remove, path, null));
        return this;
    }

    public JsonPatchDocument Test(string path, object? value)
    {
        base.Add(new JsonPatchOperation(JsonPatchOperationType.Test, path, value));
        return this;
    }

    public JsonPatchDocument Move(string from, string path)
    {
        base.Add(new JsonPatchOperation { Op = JsonPatchOperationType.Move, From = from, Path = path });
        return this;
    }

    public JsonPatchDocument Copy(string from, string path)
    {
        base.Add(new JsonPatchOperation { Op = JsonPatchOperationType.Copy, From = from, Path = path });
        return this;
    }
}
