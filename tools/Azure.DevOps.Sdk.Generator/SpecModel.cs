using System.Text.Json;

namespace Azure.DevOps.Sdk.Generator;

/// <summary>A single REST operation pulled from a swagger path.</summary>
internal sealed class SpecOperation
{
    public required string HttpMethod { get; init; }       // GET, POST, ...
    public required string RawPath { get; init; }          // /{organization}/{project}/_apis/git/...
    public required string Host { get; init; }
    public required string ApiVersion { get; init; }       // e.g. 7.2-preview.1
    public required string OperationId { get; init; }      // Resource_Method
    public required string ResourcePart { get; init; }     // text before the first underscore
    public required string MethodPart { get; init; }       // text after the first underscore
    public string? Description { get; init; }
    public List<SpecParameter> Parameters { get; } = new();
    public List<string> Consumes { get; } = new();
    public List<string> Produces { get; } = new();
    public JsonElement? SuccessSchema { get; init; }       // schema of the chosen 2xx response (if any)
}

/// <summary>A resolved operation parameter (path/query/body/header/formData).</summary>
internal sealed class SpecParameter
{
    public required string In { get; init; }
    public required string Name { get; init; }
    public bool Required { get; init; }
    public string? Description { get; init; }
    public string? CollectionFormat { get; init; }
    public JsonElement Schema { get; init; }               // for body: the body schema; otherwise the param element itself
}

/// <summary>All operations and model definitions belonging to one Azure DevOps area.</summary>
internal sealed class AreaModel
{
    public required string Folder { get; init; }
    public required string AreaName { get; init; }         // C# area name / namespace leaf
    public Dictionary<string, JsonElement> Definitions { get; } = new(StringComparer.Ordinal);
    public List<SpecOperation> Operations { get; } = new();
}

/// <summary>Loads the vendored swagger specs and groups them into <see cref="AreaModel"/>s.</summary>
internal sealed class SpecLoader
{
    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "patch", "delete", "head",
    };

    // folder name -> C# area name. Default is PascalCase(folder); these are explicit overrides.
    private static readonly Dictionary<string, string> AreaNameOverrides = new(StringComparer.Ordinal)
    {
        ["wit"] = "WorkItemTracking",
        ["ims"] = "Identities",
        ["hooks"] = "ServiceHooks",
        ["processadmin"] = "ProcessAdmin",
        ["processes"] = "Processes",
        ["tfvc"] = "Tfvc",
        ["delegatedAuth"] = "DelegatedAuthorization",
        ["artifactsPackageTypes"] = "ArtifactsPackageTypes",
    };

    private readonly List<JsonDocument> _documents = new();

    public List<AreaModel> Load(string specsDirectory)
    {
        var areas = new Dictionary<string, AreaModel>(StringComparer.Ordinal);

        foreach (var file in Directory.GetFiles(specsDirectory, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            var fileName = Path.GetFileName(file);
            var folder = fileName.Split("__", 2)[0];
            var areaName = AreaNameOverrides.TryGetValue(folder, out var o) ? o : NameUtil.Pascal(folder);

            if (!areas.TryGetValue(folder, out var area))
            {
                area = new AreaModel { Folder = folder, AreaName = areaName };
                areas[folder] = area;
            }

            var text = File.ReadAllText(file);
            var document = JsonDocument.Parse(text);
            _documents.Add(document);
            var root = document.RootElement;

            var host = root.TryGetProperty("host", out var hostEl) ? hostEl.GetString() ?? "dev.azure.com" : "dev.azure.com";
            host = NormalizeHost(host);

            var globalParameters = root.TryGetProperty("parameters", out var gp) ? gp : default;

            // Merge definitions (first occurrence wins).
            if (root.TryGetProperty("definitions", out var defs) && defs.ValueKind == JsonValueKind.Object)
            {
                foreach (var def in defs.EnumerateObject())
                {
                    area.Definitions.TryAdd(def.Name, def.Value);
                }
            }

            if (!root.TryGetProperty("paths", out var paths) || paths.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var pathProp in paths.EnumerateObject())
            {
                foreach (var methodProp in pathProp.Value.EnumerateObject())
                {
                    if (!HttpMethods.Contains(methodProp.Name))
                    {
                        continue;
                    }

                    var op = BuildOperation(pathProp.Name, methodProp.Name, methodProp.Value, host, globalParameters);
                    if (op is not null)
                    {
                        area.Operations.Add(op);
                    }
                }
            }
        }

        return areas.Values.OrderBy(a => a.AreaName, StringComparer.Ordinal).ToList();
    }

    private static SpecOperation? BuildOperation(string path, string method, JsonElement op, string host, JsonElement globalParameters)
    {
        var operationId = op.TryGetProperty("operationId", out var oid) ? oid.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(operationId))
        {
            return null;
        }

        var underscore = operationId.IndexOf('_');
        var resourcePart = underscore >= 0 ? operationId[..underscore] : operationId;
        var methodPart = underscore >= 0 ? operationId[(underscore + 1)..] : operationId;

        var apiVersion = op.TryGetProperty("x-ms-docs-override-version", out var av)
            ? av.GetString() ?? "7.2-preview.1"
            : "7.2-preview.1";

        var result = new SpecOperation
        {
            HttpMethod = method.ToUpperInvariant(),
            RawPath = path,
            Host = host,
            ApiVersion = apiVersion,
            OperationId = operationId,
            ResourcePart = resourcePart,
            MethodPart = methodPart,
            Description = op.TryGetProperty("description", out var d) ? d.GetString() : null,
            SuccessSchema = ResolveSuccessSchema(op),
        };

        if (op.TryGetProperty("consumes", out var consumes) && consumes.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in consumes.EnumerateArray())
            {
                if (c.GetString() is { } cs)
                {
                    result.Consumes.Add(cs);
                }
            }
        }

        if (op.TryGetProperty("produces", out var produces) && produces.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in produces.EnumerateArray())
            {
                if (p.GetString() is { } ps)
                {
                    result.Produces.Add(ps);
                }
            }
        }

        if (op.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Array)
        {
            foreach (var param in parameters.EnumerateArray())
            {
                var resolved = ResolveParameter(param, globalParameters);
                if (resolved is null)
                {
                    continue;
                }

                // Skip the api-version parameter; the generator injects the exact version itself.
                if (string.Equals(resolved.Name, "api-version", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Parameters.Add(resolved);
            }
        }

        return result;
    }

    private static SpecParameter? ResolveParameter(JsonElement param, JsonElement globalParameters)
    {
        if (param.TryGetProperty("$ref", out var refEl))
        {
            var refName = refEl.GetString();
            if (refName is null || globalParameters.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var key = refName.Split('/').Last();
            if (!globalParameters.TryGetProperty(key, out var target))
            {
                return null;
            }

            param = target;
        }

        var name = param.TryGetProperty("name", out var n) ? n.GetString() : null;
        var inValue = param.TryGetProperty("in", out var i) ? i.GetString() : null;
        if (name is null || inValue is null)
        {
            return null;
        }

        var schema = inValue == "body" && param.TryGetProperty("schema", out var bodySchema)
            ? bodySchema
            : param;

        return new SpecParameter
        {
            In = inValue,
            Name = name,
            Required = param.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.True,
            Description = param.TryGetProperty("description", out var desc) ? desc.GetString() : null,
            CollectionFormat = param.TryGetProperty("collectionFormat", out var cf) ? cf.GetString() : null,
            Schema = schema,
        };
    }

    private static JsonElement? ResolveSuccessSchema(JsonElement op)
    {
        if (!op.TryGetProperty("responses", out var responses) || responses.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var code in new[] { "200", "201", "202", "203", "204", "206" })
        {
            if (responses.TryGetProperty(code, out var response) &&
                response.TryGetProperty("schema", out var schema))
            {
                return schema;
            }
        }

        return null;
    }

    private static string NormalizeHost(string host)
    {
        // The notification spec uses a templated host that resolves to dev.azure.com.
        return host.Replace("{service}", string.Empty, StringComparison.Ordinal);
    }
}
