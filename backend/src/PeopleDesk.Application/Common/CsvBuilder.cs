using System.Text;

namespace PeopleDesk.Application.Common;

/// <summary>RFC 4180 CSV writer with Excel-friendly BOM.</summary>
public static class CsvBuilder
{
    public static string Build(IReadOnlyList<string> headers, IEnumerable<IEnumerable<object?>> rows)
    {
        var sb = new StringBuilder();
        AppendLine(sb, headers.Select(h => (object?)h));
        foreach (var row in rows) AppendLine(sb, row);
        return sb.ToString();
    }

    public static byte[] BuildBytes(IReadOnlyList<string> headers, IEnumerable<IEnumerable<object?>> rows) =>
        Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Build(headers, rows))).ToArray();

    private static void AppendLine(StringBuilder sb, IEnumerable<object?> cells)
    {
        var first = true;
        foreach (var cell in cells)
        {
            if (!first) sb.Append(',');
            sb.Append(Escape(cell?.ToString()));
            first = false;
        }
        sb.Append('\n');
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        return needsQuotes ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
