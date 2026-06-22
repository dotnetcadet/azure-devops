using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.DevOps.Sdk.Serialization;

/// <summary>
/// Centralized <see cref="JsonSerializerOptions"/> configured for the Azure DevOps wire format:
/// camelCase property names, null omission on write, tolerant enum handling, and relaxed escaping
/// so query/WIQL payloads round-trip cleanly.
/// </summary>
public static class AzureDevOpsJson
{
    /// <summary>The shared, immutable options instance used by the request pipeline.</summary>
    public static JsonSerializerOptions Default { get; } = Create();

    /// <summary>
    /// Builds a fresh options instance. Useful when a caller needs to further customize
    /// serialization without mutating the shared <see cref="Default"/>.
    /// </summary>
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        options.Converters.Add(new TolerantStringEnumConverterFactory());
        return options;
    }
}
