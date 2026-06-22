namespace Azure.DevOps.Sdk.Generator;

/// <summary>A node in the per-area request-builder trie (one node per path segment).</summary>
internal sealed class TrieNode
{
    public bool IsParam { get; init; }

    /// <summary>
    /// All path-parameter names that map to this slot across operations. Multiple distinct names
    /// occur when different operations name the same positional parameter differently (e.g.
    /// <c>workitems/${type}</c> for create vs <c>workitems/{id}</c> for update); the indexer sets
    /// every one of them so each operation's template substitutes correctly.
    /// </summary>
    public List<string> ParamNames { get; } = new();

    public string? ParamName => ParamNames.Count > 0 ? ParamNames[0] : null;

    public void AddParamName(string name)
    {
        if (!ParamNames.Contains(name))
        {
            ParamNames.Add(name);
        }
    }

    public string CanonicalSegment { get; set; } = string.Empty;
    public Dictionary<string, TrieNode> LiteralChildren { get; } = new(StringComparer.Ordinal);
    public TrieNode? ParamChild { get; set; }
    public List<SpecOperation> Operations { get; } = new();
    public HashSet<string> ParamTypeCandidates { get; } = new(StringComparer.Ordinal);

    public string BuilderClassName { get; set; } = string.Empty;
}
