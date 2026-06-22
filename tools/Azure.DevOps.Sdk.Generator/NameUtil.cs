using System.Text;

namespace Azure.DevOps.Sdk.Generator;

/// <summary>
/// Identifier sanitization helpers shared by every emitter: PascalCasing, keyword escaping,
/// and digit/empty handling so emitted names are always valid C#.
/// </summary>
internal static class NameUtil
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while",
    };

    /// <summary>Splits an arbitrary token on non-alphanumeric boundaries and PascalCases the parts.</summary>
    public static string Pascal(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return "Value";
        }

        var builder = new StringBuilder(raw.Length);
        var newWord = true;
        foreach (var ch in raw)
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (newWord)
                {
                    builder.Append(char.ToUpperInvariant(ch));
                    newWord = false;
                }
                else
                {
                    builder.Append(ch);
                }
            }
            else
            {
                newWord = true;
            }
        }

        var result = builder.ToString();
        if (result.Length == 0)
        {
            return "Value";
        }

        if (char.IsDigit(result[0]))
        {
            result = "_" + result;
        }

        return result;
    }

    /// <summary>camelCases a token (first letter lower) — used for nothing public but handy for locals.</summary>
    public static string Camel(string raw)
    {
        var pascal = Pascal(raw);
        if (pascal.Length == 0)
        {
            return pascal;
        }

        return char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    /// <summary>Escapes a C# keyword with a leading <c>@</c> so it can be used as an identifier.</summary>
    public static string Escape(string identifier)
    {
        return Keywords.Contains(identifier) ? "@" + identifier : identifier;
    }

    /// <summary>Produces a unique name within <paramref name="taken"/>, suffixing with 2, 3, … on collision.</summary>
    public static string Unique(string candidate, HashSet<string> taken)
    {
        if (taken.Add(candidate))
        {
            return candidate;
        }

        for (var i = 2; ; i++)
        {
            var next = candidate + i;
            if (taken.Add(next))
            {
                return next;
            }
        }
    }
}
