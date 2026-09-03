using System.Text;

namespace OnlineJudge.Infrastructure.Exports;

internal readonly record struct CsvCell(string? Value, bool IsTrusted)
{
    public static CsvCell Text(string? value) => new(value, false);
    public static CsvCell Trusted(string? value) => new(value, true);
}

internal static class SpreadsheetSafeCsvWriter
{
    public static void AppendRow(StringBuilder builder, IEnumerable<CsvCell> cells)
    {
        builder.AppendJoin(',', cells.Select(FormatCell));
        builder.AppendLine();
    }

    private static string FormatCell(CsvCell cell)
    {
        var value = cell.Value ?? string.Empty;
        if (!cell.IsTrusted && IsFormulaLike(value))
        {
            value = $"'{value}";
        }

        var escaped = value.Replace("\"", "\"\"");
        return escaped.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{escaped}\""
            : escaped;
    }

    private static bool IsFormulaLike(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        if (value[0] is '\t' or '\r' or '\n')
        {
            return true;
        }

        var index = 0;
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        return index < value.Length && value[index] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n';
    }
}
