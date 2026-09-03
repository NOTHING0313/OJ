using System.Text;
using System.Text.RegularExpressions;

namespace OnlineJudge.Infrastructure.Judging.Function;

internal static partial class FunctionUserCodeGuard
{
    public static bool ContainsCStyleMain(string sourceCode)
    {
        return CStyleMainRegex().IsMatch(MaskCommentsAndLiterals(sourceCode));
    }

    public static bool ContainsCSharpEntryPoint(string sourceCode)
    {
        return CSharpEntryPointRegex().IsMatch(MaskCommentsAndLiterals(sourceCode));
    }

    internal static string MaskCommentsAndLiterals(string sourceCode)
    {
        var result = new StringBuilder(sourceCode.Length);
        var index = 0;
        while (index < sourceCode.Length)
        {
            if (StartsWith(sourceCode, index, "//"))
            {
                MaskUntilLineEnd(sourceCode, result, ref index);
            }
            else if (StartsWith(sourceCode, index, "/*"))
            {
                MaskBlockComment(sourceCode, result, ref index);
            }
            else if (sourceCode[index] == '@' && index + 1 < sourceCode.Length && sourceCode[index + 1] == '"')
            {
                MaskVerbatimString(sourceCode, result, ref index, prefixLength: 2);
            }
            else if (sourceCode[index] == '@'
                && index + 2 < sourceCode.Length
                && sourceCode[index + 1] == '$'
                && sourceCode[index + 2] == '"')
            {
                MaskVerbatimString(sourceCode, result, ref index, prefixLength: 3);
            }
            else if (sourceCode[index] == '"')
            {
                var quoteCount = CountConsecutive(sourceCode, index, '"');
                if (quoteCount >= 3)
                {
                    MaskRawString(sourceCode, result, ref index, quoteCount);
                }
                else
                {
                    MaskEscapedLiteral(sourceCode, result, ref index, '"');
                }
            }
            else if (sourceCode[index] == '\'')
            {
                MaskEscapedLiteral(sourceCode, result, ref index, '\'');
            }
            else
            {
                result.Append(sourceCode[index]);
                index++;
            }
        }

        return result.ToString();
    }

    private static void MaskUntilLineEnd(string source, StringBuilder result, ref int index)
    {
        while (index < source.Length && source[index] != '\n')
        {
            result.Append(' ');
            index++;
        }
    }

    private static void MaskBlockComment(string source, StringBuilder result, ref int index)
    {
        AppendMasked(result, source[index++]);
        AppendMasked(result, source[index++]);
        while (index < source.Length)
        {
            if (StartsWith(source, index, "*/"))
            {
                AppendMasked(result, source[index++]);
                AppendMasked(result, source[index++]);
                return;
            }

            AppendMasked(result, source[index++]);
        }
    }

    private static void MaskVerbatimString(string source, StringBuilder result, ref int index, int prefixLength)
    {
        for (var count = 0; count < prefixLength; count++)
        {
            AppendMasked(result, source[index++]);
        }
        while (index < source.Length)
        {
            var character = source[index++];
            AppendMasked(result, character);
            if (character != '"')
            {
                continue;
            }

            if (index < source.Length && source[index] == '"')
            {
                AppendMasked(result, source[index++]);
                continue;
            }

            return;
        }
    }

    private static void MaskRawString(string source, StringBuilder result, ref int index, int quoteCount)
    {
        for (var count = 0; count < quoteCount; count++)
        {
            AppendMasked(result, source[index++]);
        }

        while (index < source.Length)
        {
            if (CountConsecutive(source, index, '"') >= quoteCount)
            {
                for (var count = 0; count < quoteCount; count++)
                {
                    AppendMasked(result, source[index++]);
                }

                return;
            }

            AppendMasked(result, source[index++]);
        }
    }

    private static void MaskEscapedLiteral(string source, StringBuilder result, ref int index, char quote)
    {
        AppendMasked(result, source[index++]);
        var escaped = false;
        while (index < source.Length)
        {
            var character = source[index++];
            AppendMasked(result, character);
            if (escaped)
            {
                escaped = false;
            }
            else if (character == '\\')
            {
                escaped = true;
            }
            else if (character == quote)
            {
                return;
            }
        }
    }

    private static void AppendMasked(StringBuilder result, char character)
    {
        result.Append(character is '\r' or '\n' ? character : ' ');
    }

    private static int CountConsecutive(string value, int index, char character)
    {
        var count = 0;
        while (index + count < value.Length && value[index + count] == character)
        {
            count++;
        }

        return count;
    }

    private static bool StartsWith(string value, int index, string candidate)
    {
        return index + candidate.Length <= value.Length
            && value.AsSpan(index, candidate.Length).SequenceEqual(candidate);
    }

    [GeneratedRegex(@"\bmain\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex CStyleMainRegex();

    [GeneratedRegex(@"\bclass\s+Program\b|\bMain\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex CSharpEntryPointRegex();
}
