using System.Text.Json;

namespace Azure.DevOps.Sdk.Generator;

/// <summary>A collected enum: its C# name, members (wire value + identifier), and flags-ness.</summary>
internal sealed class EnumDef
{
    public required string CSharpName { get; init; }
    public bool IsFlags { get; init; }
    public List<(string Wire, string Identifier, string? Description)> Members { get; } = new();
}

/// <summary>
/// Per-area type resolution: maps swagger schemas to C# types, owns the definition→class-name
/// and x-ms-enum→enum-name registries, and special-cases the shared JSON Patch types so they
/// resolve to the hand-written runtime models rather than per-area duplicates.
/// </summary>
internal sealed class TypeContext
{
    private const string JsonPatchDocumentType = "global::Azure.DevOps.Sdk.Models.JsonPatchDocument";
    private const string JsonPatchOperationType = "global::Azure.DevOps.Sdk.Models.JsonPatchOperation";

    private readonly Dictionary<string, string> _classNames = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EnumDef> _enums = new(StringComparer.Ordinal);
    private readonly HashSet<string> _takenTypeNames = new(StringComparer.Ordinal);
    private string _modelsNamespace = "Azure.DevOps.Sdk.Models";

    public IReadOnlyDictionary<string, string> ClassNames => _classNames;
    public IReadOnlyDictionary<string, EnumDef> Enums => _enums;

    /// <summary>Fully qualifies a simple model/enum type name to avoid type/namespace collisions in emitted code.</summary>
    public string Qualify(string simpleTypeName) => $"global::{_modelsNamespace}.{simpleTypeName}";

    public static TypeContext Build(AreaModel area)
    {
        var context = new TypeContext { _modelsNamespace = $"Azure.DevOps.Sdk.{area.AreaName}.Models" };

        // Pass 1: register class names for all object definitions (skip the shared JSON Patch types).
        foreach (var def in area.Definitions)
        {
            if (def.Key is "JsonPatchDocument" or "JsonPatchOperation")
            {
                continue;
            }

            if (IsEnumSchema(def.Value))
            {
                continue; // handled as an enum in pass 2
            }

            var name = NameUtil.Unique(NameUtil.Pascal(def.Key), context._takenTypeNames);
            context._classNames[def.Key] = name;
        }

        // Pass 2: collect every inline enum across definitions and operation parameters.
        foreach (var def in area.Definitions.Values)
        {
            context.CollectEnums(def);
        }

        foreach (var op in area.Operations)
        {
            foreach (var param in op.Parameters)
            {
                context.CollectEnums(param.Schema);
            }

            if (op.SuccessSchema is { } success)
            {
                context.CollectEnums(success);
            }
        }

        return context;
    }

    private void CollectEnums(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (TryGetEnumName(schema, out var enumName) &&
            schema.TryGetProperty("enum", out var enumValues) && enumValues.ValueKind == JsonValueKind.Array)
        {
            RegisterEnum(enumName, schema, enumValues);
        }

        if (schema.TryGetProperty("items", out var items))
        {
            CollectEnums(items);
        }

        if (schema.TryGetProperty("additionalProperties", out var ap) && ap.ValueKind == JsonValueKind.Object)
        {
            CollectEnums(ap);
        }

        if (schema.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in props.EnumerateObject())
            {
                CollectEnums(p.Value);
            }
        }

        if (schema.TryGetProperty("allOf", out var allOf) && allOf.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in allOf.EnumerateArray())
            {
                CollectEnums(entry);
            }
        }
    }

    private void RegisterEnum(string xmsName, JsonElement schema, JsonElement enumValues)
    {
        if (_enums.ContainsKey(xmsName))
        {
            return;
        }

        var isFlags = schema.TryGetProperty("x-ms-enum", out var xms) &&
                      xms.TryGetProperty("isFlags", out var f) && f.ValueKind == JsonValueKind.True;

        var csName = NameUtil.Unique(NameUtil.Pascal(xmsName), _takenTypeNames);
        var def = new EnumDef { CSharpName = csName, IsFlags = isFlags };

        // Prefer x-ms-enum.values (carries descriptions); fall back to the raw enum array.
        var descriptions = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (xms.ValueKind == JsonValueKind.Object && xms.TryGetProperty("values", out var values) &&
            values.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in values.EnumerateArray())
            {
                var val = v.TryGetProperty("value", out var ve) ? ScalarToString(ve) : null;
                if (val is not null)
                {
                    descriptions[val] = v.TryGetProperty("description", out var de) ? de.GetString() : null;
                }
            }
        }

        var memberNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in enumValues.EnumerateArray())
        {
            var wire = ScalarToString(value);
            if (wire is null)
            {
                continue;
            }

            var identifier = NameUtil.Unique(NameUtil.Pascal(wire), memberNames);
            descriptions.TryGetValue(wire, out var description);
            def.Members.Add((wire, identifier, description));
        }

        if (def.Members.Count > 0)
        {
            _enums[xmsName] = def;
        }
    }

    /// <summary>Maps a schema to its base C# type name (the caller appends nullability as needed).</summary>
    public string MapType(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return "object";
        }

        if (schema.TryGetProperty("$ref", out var refEl))
        {
            return ResolveRef(refEl.GetString());
        }

        if (TryGetEnumName(schema, out var enumName) && _enums.TryGetValue(enumName, out var enumDef))
        {
            return Qualify(enumDef.CSharpName);
        }

        // allOf with a single $ref behaves like that ref (e.g. a refined alias).
        if (schema.TryGetProperty("allOf", out var allOf) && allOf.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in allOf.EnumerateArray())
            {
                if (entry.TryGetProperty("$ref", out var innerRef))
                {
                    return ResolveRef(innerRef.GetString());
                }
            }
        }

        var type = schema.TryGetProperty("type", out var t) ? t.GetString() : null;
        var format = schema.TryGetProperty("format", out var f) ? f.GetString() : null;

        switch (type)
        {
            case "array":
                var itemType = schema.TryGetProperty("items", out var items) ? MapType(items) : "object";
                return $"List<{itemType}>";
            case "integer":
                return format == "int64" ? "long" : "int";
            case "number":
                return format == "float" ? "float" : "double";
            case "boolean":
                return "bool";
            case "string":
                return format switch
                {
                    "date-time" => "DateTimeOffset",
                    "date" => "DateTimeOffset",
                    "uuid" => "Guid",
                    _ => "string",
                };
            case "object":
            case null:
                if (schema.TryGetProperty("additionalProperties", out var ap))
                {
                    var valueType = ap.ValueKind == JsonValueKind.Object && ap.TryGetProperty("type", out _) || ap.ValueKind == JsonValueKind.Object && ap.TryGetProperty("$ref", out _)
                        ? MapType(ap)
                        : "object";
                    return $"Dictionary<string, {valueType}>";
                }

                return "object";
            default:
                return "object";
        }
    }

    /// <summary>True when the mapped type is a C# value type (so the emitter knows to append <c>?</c> for nullability).</summary>
    public bool IsValueType(string mappedType) =>
        mappedType is "int" or "long" or "double" or "float" or "bool" or "DateTimeOffset" or "Guid"
        || _enums.Values.Any(e => Qualify(e.CSharpName) == mappedType);

    private string ResolveRef(string? refName)
    {
        if (refName is null)
        {
            return "object";
        }

        var defName = refName.Split('/').Last();
        return defName switch
        {
            "JsonPatchDocument" => JsonPatchDocumentType,
            "JsonPatchOperation" => JsonPatchOperationType,
            _ => _classNames.TryGetValue(defName, out var name) ? Qualify(name) : "object",
        };
    }

    private static bool IsEnumSchema(JsonElement schema) =>
        schema.ValueKind == JsonValueKind.Object &&
        schema.TryGetProperty("enum", out var e) && e.ValueKind == JsonValueKind.Array &&
        schema.TryGetProperty("x-ms-enum", out _);

    private static bool TryGetEnumName(JsonElement schema, out string name)
    {
        name = string.Empty;
        if (schema.TryGetProperty("enum", out var e) && e.ValueKind == JsonValueKind.Array &&
            schema.TryGetProperty("x-ms-enum", out var xms) && xms.ValueKind == JsonValueKind.Object &&
            xms.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } enumName)
        {
            name = enumName;
            return true;
        }

        return false;
    }

    private static string? ScalarToString(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null,
    };
}
