using System.Text;
using System.Text.RegularExpressions;

namespace Xdiff.UnitTests;

/// <summary>
/// Builds <see cref="DiffOptions.FunctionNameExtractor"/> delegates that mirror
/// the builtin userdiff driver patterns (html/javascript/php), so golden
/// tests can exercise the extractor path of <c>UnifiedDiffEmitter</c> against
/// the committed <c>expected/driver/diff.*</c> fixtures.
/// </summary>
/// <remarks>
/// The extraction algorithm right-trims the line, searches each pattern in order, uses
/// capture group 1 when it participated else group 0, trim leading/trailing
/// whitespace from the match, and return byte offsets into the (rtrim'd) line.
/// </remarks>
internal static class UserDiffDrivers
{
    /// <summary>
    /// Builds a function-name extractor for the named language driver.
    /// Returns <c>null</c> for unknown drivers.
    /// </summary>
    public static Func<ReadOnlySpan<byte>, (bool IsMatch, Range NameRange)>? BuildExtractor(string driver)
    {
        return driver switch
        {
            "html" => BuildExtractor(Patterns.Html),
            "javascript" => BuildExtractor(Patterns.Javascript),
            "php" => BuildExtractor(Patterns.Php),
            _ => null,
        };
    }

    private static Func<ReadOnlySpan<byte>, (bool IsMatch, Range NameRange)> BuildExtractor(string[] patterns)
    {
        // Compile each pattern once. The source patterns are POSIX-style; the
        // builtins here are plain (no character classes that differ from .NET).
        var regexes = new Regex[patterns.Length];
        for (int i = 0; i < patterns.Length; i++)
        {
            // None of the html/js/php builtins use negation ('!' prefix).
            regexes[i] = new Regex(patterns[i], RegexOptions.None, TimeSpan.FromSeconds(5));
        }

        return extractor;

        (bool, Range) extractor(ReadOnlySpan<byte> line)
        {
            // Right-trim the line before matching.
            ReadOnlySpan<byte> trimmed = TrimTrailingWhitespace(line);
            if (trimmed.IsEmpty)
            {
                return (false, default);
            }

            string text = Encoding.UTF8.GetString(trimmed);

            foreach (Regex re in regexes)
            {
                Match m = re.Match(text);
                if (!m.Success)
                {
                    continue;
                }

                // Use group 1 if it captured, else group 0 (full match).
                Group capture = m.Groups[1].Success ? m.Groups[1] : m.Groups[0];
                int start = capture.Index;
                int end = start + capture.Length;

                // Trim leading/trailing whitespace from the capture
                // (consume the prefix, truncate, then right-trim).
                while (start < end && char.IsWhiteSpace(text[start]))
                {
                    start++;
                }

                while (end > start && char.IsWhiteSpace(text[end - 1]))
                {
                    end--;
                }

                if (start >= end)
                {
                    return (false, default);
                }

                // Map character indices → byte offsets into `trimmed` (which is a
                // prefix of the original `line`, so the offsets are valid against
                // both). This matters when UTF-8 bytes precede the match.
                int byteStart = Encoding.UTF8.GetByteCount(text.AsSpan(0, start));
                int byteEnd = byteStart + Encoding.UTF8.GetByteCount(text.AsSpan(start, end - start));
                return (true, new Range(byteStart, byteEnd));
            }

            return (false, default);
        }
    }

    private static ReadOnlySpan<byte> TrimTrailingWhitespace(ReadOnlySpan<byte> span)
    {
        int end = span.Length;
        while (end > 0)
        {
            byte c = span[end - 1];
            if (c is not ((byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r' or (byte)'\f' or (byte)'\v'))
            {
                break;
            }

            end--;
        }

        return span[..end];
    }

    private static class Patterns
    {
        // Builtin language patterns.
        // javascript has three newline-separated patterns (tried in order).
        public static readonly string[] Html =
        [
            @"^[ \t]*(<[Hh][1-6][ \t].*>.*)$",
        ];

        public static readonly string[] Javascript =
        [
            @"([a-zA-Z_$][a-zA-Z0-9_$]*(\.[a-zA-Z0-9_$]+)*[ \t]*=[ \t]*function([ \t][a-zA-Z_$][a-zA-Z0-9_$]*)?[^\{]*)",
            @"([a-zA-Z_$][a-zA-Z0-9_$]*[ \t]*:[ \t]*function([ \t][a-zA-Z_$][a-zA-Z0-9_$]*)?[^\{]*)",
            @"[^a-zA-Z0-9_\$](function([ \t][a-zA-Z_$][a-zA-Z0-9_$]*)?[^\{]*)",
        ];

        public static readonly string[] Php =
        [
            @"^[ \t]*(((public|private|protected|static|final)[ \t]+)*((class|function)[ \t].*))$",
        ];
    }
}
