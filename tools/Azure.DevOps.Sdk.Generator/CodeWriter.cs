using System.Text;

namespace Azure.DevOps.Sdk.Generator;

/// <summary>A tiny indentation-aware source builder.</summary>
internal sealed class CodeWriter
{
    private readonly StringBuilder _builder = new();
    private int _indent;

    public void Line(string text = "")
    {
        if (text.Length == 0)
        {
            _builder.Append('\n');
            return;
        }

        _builder.Append(new string(' ', _indent * 4));
        _builder.Append(text);
        _builder.Append('\n');
    }

    public IDisposable Block(string header)
    {
        Line(header);
        Line("{");
        _indent++;
        return new Closer(this);
    }

    public void Open(string header)
    {
        Line(header);
        Line("{");
        _indent++;
    }

    public void Close(string suffix = "")
    {
        _indent--;
        Line("}" + suffix);
    }

    /// <summary>Emits a <c>///</c> XML doc summary, escaping and collapsing the text.</summary>
    public void Doc(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return;
        }

        var text = summary.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        text = text.Replace("\r", " ").Replace("\n", " ").Trim();
        while (text.Contains("  "))
        {
            text = text.Replace("  ", " ");
        }

        Line("/// <summary>");
        Line("/// " + text);
        Line("/// </summary>");
    }

    public override string ToString() => _builder.ToString();

    private sealed class Closer(CodeWriter writer) : IDisposable
    {
        public void Dispose() => writer.Close();
    }
}
