using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Azure.DevOps.Sdk.Serialization;

/// <summary>
/// Serializes enums as their Azure DevOps wire string (honoring
/// <see cref="JsonStringEnumMemberNameAttribute"/>), and tolerates values the SDK does not know
/// about — which Azure DevOps adds over time.
/// </summary>
/// <remarks>
/// Tolerance is handled without silently corrupting data on round-trip:
/// <list type="bullet">
/// <item>For a <b>nullable</b> enum property (the shape of every generated model property), an
/// unrecognized wire value deserializes to <c>null</c>. Because the SDK omits nulls when writing,
/// an unknown value read from the service is simply not echoed back on a subsequent update, so it
/// is never overwritten with a wrong member.</item>
/// <item>Unknown <b>integer</b> values are preserved verbatim and written back as numbers.</item>
/// <item>For a non-nullable enum, an unknown value falls back to the default member (these are
/// outbound-only values the caller sets, so this case does not arise from service responses).</item>
/// </list>
/// </remarks>
public sealed class TolerantStringEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsEnum || (Nullable.GetUnderlyingType(typeToConvert)?.IsEnum ?? false);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var underlying = Nullable.GetUnderlyingType(typeToConvert);
        if (underlying is not null)
        {
            var nullableType = typeof(NullableTolerantStringEnumConverter<>).MakeGenericType(underlying);
            return (JsonConverter)Activator.CreateInstance(nullableType)!;
        }

        var converterType = typeof(TolerantStringEnumConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <summary>Shared wire-name maps and (de)serialization logic for an enum type.</summary>
internal static class EnumWireMap<TEnum>
    where TEnum : struct, Enum
{
    public static readonly bool IsFlags = typeof(TEnum).IsDefined(typeof(FlagsAttribute), inherit: false);

    private static readonly Dictionary<string, TEnum> NameToValue = BuildNameToValue();
    private static readonly List<(TEnum Value, string Name)> ValueToName = BuildValueToName();

    private static Dictionary<string, TEnum> BuildNameToValue()
    {
        var map = new Dictionary<string, TEnum>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = (TEnum)field.GetValue(null)!;
            var wire = field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? field.Name;
            map[wire] = value;
            map[field.Name] = value;
        }

        return map;
    }

    private static List<(TEnum, string)> BuildValueToName()
    {
        var list = new List<(TEnum, string)>();
        var seen = new HashSet<TEnum>();
        foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = (TEnum)field.GetValue(null)!;
            if (seen.Add(value))
            {
                var wire = field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? field.Name;
                list.Add((value, wire));
            }
        }

        return list;
    }

    /// <summary>Attempts to parse a wire string. Returns false when the value is unrecognized.</summary>
    public static bool TryParseString(string? text, out TEnum value)
    {
        value = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (NameToValue.TryGetValue(text, out value))
        {
            return true;
        }

        if (IsFlags && text.Contains(','))
        {
            long accumulator = 0;
            var matchedAny = false;
            foreach (var part in text.Split(','))
            {
                if (NameToValue.TryGetValue(part.Trim(), out var flag))
                {
                    accumulator |= Convert.ToInt64(flag);
                    matchedAny = true;
                }
            }

            if (matchedAny)
            {
                value = (TEnum)Enum.ToObject(typeof(TEnum), accumulator);
                return true;
            }
        }

        return false;
    }

    public static TEnum FromNumber(long number) => (TEnum)Enum.ToObject(typeof(TEnum), number);

    public static void Write(Utf8JsonWriter writer, TEnum value)
    {
        foreach (var (candidate, name) in ValueToName)
        {
            if (EqualityComparer<TEnum>.Default.Equals(candidate, value))
            {
                writer.WriteStringValue(name);
                return;
            }
        }

        if (IsFlags)
        {
            var remaining = Convert.ToInt64(value);
            var parts = new List<string>();
            foreach (var (candidate, name) in ValueToName)
            {
                var bits = Convert.ToInt64(candidate);
                // Only single-bit members participate in decomposition; subtract as consumed.
                if (bits != 0 && (bits & (bits - 1)) == 0 && (remaining & bits) == bits)
                {
                    parts.Add(name);
                    remaining &= ~bits;
                }
            }

            if (remaining == 0 && parts.Count > 0)
            {
                writer.WriteStringValue(string.Join(",", parts));
                return;
            }
        }

        // No (complete) name match: write the underlying number so the value round-trips.
        writer.WriteNumberValue(Convert.ToInt64(value));
    }
}

/// <summary>Converter for a non-nullable enum (unknown string → default member).</summary>
public sealed class TolerantStringEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => EnumWireMap<TEnum>.TryParseString(reader.GetString(), out var v) ? v : default,
            JsonTokenType.Number when reader.TryGetInt64(out var n) => EnumWireMap<TEnum>.FromNumber(n),
            _ => default,
        };

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
        EnumWireMap<TEnum>.Write(writer, value);
}

/// <summary>
/// Converter for a nullable enum: an unrecognized wire value becomes <c>null</c> (and is then
/// omitted on write), preserving the service's value across a read-modify-write round-trip.
/// </summary>
public sealed class NullableTolerantStringEnumConverter<TEnum> : JsonConverter<TEnum?>
    where TEnum : struct, Enum
{
    public override bool HandleNull => true;

    public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => EnumWireMap<TEnum>.TryParseString(reader.GetString(), out var v) ? v : null,
            JsonTokenType.Number when reader.TryGetInt64(out var n) => EnumWireMap<TEnum>.FromNumber(n),
            _ => null,
        };

    public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            EnumWireMap<TEnum>.Write(writer, value.Value);
        }
    }
}
