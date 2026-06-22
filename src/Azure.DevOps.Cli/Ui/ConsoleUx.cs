using System.Text;

namespace Azure.DevOps.Cli.Ui;

/// <summary>Small console presentation helpers: banners, status lines, and simple tables.</summary>
public static class ConsoleUx
{
    public static void Banner()
    {
        Write(ConsoleColor.Cyan,
            """
              __ _ ____  ___  ___
             / _` |_  / / _ \/ _ \    Azure DevOps CLI
             \__,_/___|\___/\___/     fluent SDK • multi-org
            """);
        Console.WriteLine();
    }

    public static void Success(string message) => Write(ConsoleColor.Green, "✔ " + message);

    public static void Info(string message) => Write(ConsoleColor.Gray, message);

    public static void Warn(string message) => Write(ConsoleColor.Yellow, "! " + message);

    public static void Error(string message) => Write(ConsoleColor.Red, "✘ " + message);

    public static void Heading(string message)
    {
        Console.WriteLine();
        Write(ConsoleColor.White, message);
        Write(ConsoleColor.DarkGray, new string('─', Math.Min(message.Length, 60)));
    }

    public static void Write(ConsoleColor color, string text)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = previous;
    }

    /// <summary>Renders rows as a left-aligned, column-padded table with a header.</summary>
    public static void Table(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        var widths = new int[headers.Count];
        for (var c = 0; c < headers.Count; c++)
        {
            widths[c] = headers[c].Length;
        }

        foreach (var row in rows)
        {
            for (var c = 0; c < headers.Count && c < row.Count; c++)
            {
                widths[c] = Math.Max(widths[c], (row[c] ?? string.Empty).Length);
            }
        }

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(BuildRow(headers, widths));
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(string.Join("─┼─", widths.Select(w => new string('─', w))));
        Console.ForegroundColor = previous;

        foreach (var row in rows)
        {
            Console.WriteLine(BuildRow(row, widths));
        }

        if (rows.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("(no items)");
            Console.ForegroundColor = previous;
        }
    }

    private static string BuildRow(IReadOnlyList<string?> cells, int[] widths)
    {
        var builder = new StringBuilder();
        for (var c = 0; c < widths.Length; c++)
        {
            if (c > 0)
            {
                builder.Append(" │ ");
            }

            var value = c < cells.Count ? cells[c] ?? string.Empty : string.Empty;
            if (value.Length > widths[c])
            {
                value = value[..widths[c]];
            }

            builder.Append(value.PadRight(widths[c]));
        }

        return builder.ToString();
    }
}
