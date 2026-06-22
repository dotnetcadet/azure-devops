using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Azure.DevOps.Sdk.Serialization;

namespace Azure.DevOps.Sdk.Http;

/// <summary>
/// Converts path and query parameter values into their Azure DevOps wire representation:
/// invariant numbers, ISO-8601 dates, lowercase booleans, enum wire names, and comma-joined
/// collections (the default <c>collectionFormat: csv</c> used across the API).
/// </summary>
internal static class RequestValueFormatter
{
    public static string? Format(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case string s:
                return s;
            case bool b:
                return b ? "true" : "false";
            case DateTime dt:
                return dt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            case DateTimeOffset dto:
                return dto.ToString("o", CultureInfo.InvariantCulture);
            case Guid g:
                return g.ToString("D");
            case Enum e:
                return FormatEnum(e);
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            case float f:
                return f.ToString("R", CultureInfo.InvariantCulture);
            case double d:
                return d.ToString("R", CultureInfo.InvariantCulture);
            case decimal m:
                return m.ToString(CultureInfo.InvariantCulture);
            case IEnumerable enumerable:
                return FormatCollection(enumerable);
            default:
                return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    private static string FormatEnum(Enum value)
    {
        // Reuse the tolerant enum converter so wire names (camelCase) are honored.
        var json = JsonSerializer.Serialize(value, value.GetType(), AzureDevOpsJson.Default);
        return json.Length >= 2 && json[0] == '"' ? json[1..^1] : json;
    }

    private static string FormatCollection(IEnumerable enumerable)
    {
        var builder = new StringBuilder();
        var first = true;
        foreach (var item in enumerable)
        {
            if (!first)
            {
                builder.Append(',');
            }

            builder.Append(Format(item));
            first = false;
        }

        return builder.ToString();
    }
}
