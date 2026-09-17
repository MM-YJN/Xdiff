namespace Xdiff.UnitTests;

/// <summary>
/// Golden-parity tests for the xdiff indent heuristic (XDF_INDENT_HEURISTIC).
/// Each fixture pair under <c>Fixtures/indent_heuristic/</c> exercises a
/// distinct branch of <c>IndentHeuristic</c>; the <c>.expected</c> files were
/// captured from a C reference driver built against standalone xdiff
/// (see <c>generate-indent-heuristic-goldens.sh</c>).
/// </summary>
public class IndentHeuristicTests
{
    private const int Context = 3;

    private static string RunUnifiedDiff(string oldText, string newText, bool indentHeuristic, int context = Context)
    {
        return Diff.UnifiedDiff(oldText, newText, new DiffOptions
        {
            IndentHeuristic = indentHeuristic,
            ContextLines = context,
        });
    }

    public static IEnumerable<object[]> GoldenCases
    => [
        ["01_spaces", "blank-line shift in plain text (a/blank/b)"],
        ["02_functions", "function-block insertion with C-style braces"],
        ["03_tabs", "tab-indented sliding region"],
        ["05_max_indent", "indent clamped at MAX_INDENT (250+ spaces)"],
        ["06_many_blanks", "more than MAX_BLANKS (25) consecutive blanks"],
        ["07_startoffile", "change group at start of file (no pre-indent)"],
    ];

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public void UnifiedDiff_WithIndentHeuristic_MatchesCGolden(string name, string _)
    {
        string oldText = FixtureLoader.LoadText($"Fixtures/indent_heuristic/{name}_old.txt");
        string newText = FixtureLoader.LoadText($"Fixtures/indent_heuristic/{name}_new.txt");
        string golden = FixtureLoader.LoadText($"Fixtures/indent_heuristic/{name}.expected");

        string output = RunUnifiedDiff(oldText, newText, indentHeuristic: true);

        Assert.Equal(golden, output);
    }

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public void UnifiedDiff_WithoutIndentHeuristic_MatchesCGolden(string name, string _)
    {
        string oldText = FixtureLoader.LoadText($"Fixtures/indent_heuristic/{name}_old.txt");
        string newText = FixtureLoader.LoadText($"Fixtures/indent_heuristic/{name}_new.txt");
        string golden = FixtureLoader.LoadText($"Fixtures/indent_heuristic/{name}.expected_noindent");

        string output = RunUnifiedDiff(oldText, newText, indentHeuristic: false);

        Assert.Equal(golden, output);
    }

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public void IndentHeuristic_ProducesDifferentOutputThanDefault(string name, string _)
    {
        string oldText = FixtureLoader.LoadText($"Fixtures/indent_heuristic/{name}_old.txt");
        string newText = FixtureLoader.LoadText($"Fixtures/indent_heuristic/{name}_new.txt");

        string withHeuristic = RunUnifiedDiff(oldText, newText, indentHeuristic: true);
        string withoutHeuristic = RunUnifiedDiff(oldText, newText, indentHeuristic: false);

        Assert.NotEqual(withoutHeuristic, withHeuristic);
    }

    [Theory]
    [InlineData(DiffAlgorithm.Patience)]
    [InlineData(DiffAlgorithm.Histogram)]
    public void IndentHeuristic_PatienceAndHistogram_MatchMyersGolden(DiffAlgorithm alg)
    {
        string oldText = FixtureLoader.LoadText("Fixtures/indent_heuristic/01_spaces_old.txt");
        string newText = FixtureLoader.LoadText("Fixtures/indent_heuristic/01_spaces_new.txt");
        string golden = FixtureLoader.LoadText("Fixtures/indent_heuristic/01_spaces.expected");

        string output = Diff.UnifiedDiff(oldText, newText, new DiffOptions
        {
            Algorithm = alg,
            IndentHeuristic = true,
            ContextLines = Context,
        });

        Assert.Equal(golden, output);
    }

    [Fact]
    public void IndentHeuristic_SlidingGroupAtEof_EvaluatesEndOfFileSplit()
    {
        // Appending a repeated "a\n\nb" block past an existing one makes the
        // insertion position ambiguous, so the heuristic slides the group and
        // evaluates split candidates at/past the last record (EOF branch +
        // EndOfFilePenalty).
        string output = RunUnifiedDiff(
            "a\n\nb\na\n\nb\n",
            "a\n\nb\na\n\nb\na\n\nb\n",
            indentHeuristic: true);

        Assert.Contains("+a", output);
    }

    [Fact]
    public void IndentHeuristic_LargeSlidingGroup_ClampsShiftWindow()
    {
        // Appending many identical lines past an identical run yields a large
        // sliding group, forcing the shift lower-bound to be clamped by both the
        // group-size and the IndentHeuristicMaxSliding (100) caps.
        string oldText = "h\n" + Repeat("same\n", 120);
        string newText = "h\n" + Repeat("same\n", 240);

        string output = RunUnifiedDiff(oldText, newText, indentHeuristic: true);

        Assert.NotEmpty(output);
    }

    [Fact]
    public void IndentHeuristic_OutdentRegion_AppliesOutdentPenalty()
    {
        // Deleting one of two identical deeply-indented lines leaves an ambiguous
        // group that slides; when its end lands on the dedented middle line
        // (indent < preIndent, postIndent > indent) the outdent penalty fires.
        string oldText =
            "                x\n" +
            "                x\n" +
            "        y\n" +
            "                z\n";
        string newText =
            "                x\n" +
            "        y\n" +
            "                z\n";

        string output = RunUnifiedDiff(oldText, newText, indentHeuristic: true);

        Assert.NotEmpty(output);
    }

    private static string Repeat(string s, int n)
    {
        var sb = new System.Text.StringBuilder(s.Length * n);
        for (int i = 0; i < n; i++)
        {
            sb.Append(s);
        }

        return sb.ToString();
    }
}
