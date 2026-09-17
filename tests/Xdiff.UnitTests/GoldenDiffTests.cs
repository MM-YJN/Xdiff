using System.Text;

namespace Xdiff.UnitTests;

public class GoldenDiffTests
{
    [Theory]
    [InlineData("html")]
    [InlineData("javascript")]
    [InlineData("php")]
    public void UnifiedDiff_MatchesUpstreamNodriverGolden(string ext)
    {
        string before = FixtureLoader.LoadText($"Fixtures/diff/before/file.{ext}");
        string after = FixtureLoader.LoadText($"Fixtures/diff/after/file.{ext}");
        string goldenFull = FixtureLoader.LoadText($"Fixtures/diff/expected/nodriver/diff.{ext}");
        string golden = FixtureLoader.StripFramingHeader(goldenFull);

        string output = Diff.UnifiedDiff(before, after, new DiffOptions
        {
            ContextLines = 1,
            InterHunkLines = 1,
            IncludeFunctionNames = true,
        });

        Assert.Equal(golden, output);
    }

    [Theory]
    [InlineData("html")]
    [InlineData("javascript")]
    [InlineData("php")]
    public void UnifiedDiff_MatchesUpstreamDriverGolden(string ext)
    {
        string driver = ext;
        Func<ReadOnlySpan<byte>, (bool IsMatch, Range NameRange)>? extractor =
            UserDiffDrivers.BuildExtractor(driver);
        Assert.NotNull(extractor);

        string before = FixtureLoader.LoadText($"Fixtures/diff/before/file.{ext}");
        string after = FixtureLoader.LoadText($"Fixtures/diff/after/file.{ext}");
        string goldenFull = FixtureLoader.LoadText($"Fixtures/diff/expected/driver/diff.{ext}");
        string golden = FixtureLoader.StripFramingHeader(goldenFull);

        string output = Diff.UnifiedDiff(before, after, new DiffOptions
        {
            ContextLines = 1,
            InterHunkLines = 1,
            IncludeFunctionNames = true,
            FunctionNameExtractor = extractor,
        });

        Assert.Equal(golden, output);
    }

    [Theory]
    [InlineData("html")]
    [InlineData("javascript")]
    [InlineData("php")]
    public void UnifiedDiff_FunctionContext_MatchesGitGolden(string ext)
    {
        string before = FixtureLoader.LoadText($"Fixtures/diff/before/file.{ext}");
        string after = FixtureLoader.LoadText($"Fixtures/diff/after/file.{ext}");
        string goldenFull = FixtureLoader.LoadText($"Fixtures/diff/expected/funcctx/diff.{ext}");
        string golden = FixtureLoader.StripFramingHeader(goldenFull);

        string output = Diff.UnifiedDiff(before, after, new DiffOptions
        {
            ContextLines = 1,
            InterHunkLines = 1,
            FunctionContext = true,
            IncludeFunctionNames = true,
        });

        Assert.Equal(golden, output);
    }

    [Fact]
    public void UnifiedDiff_FunctionNameTruncatedAtMaxFuncLen()
    {
        // Function record is 97 chars; xdiff caps the name buffer at
        // 80 bytes (struct func_line { char buf[80]; }), so the emitted hunk
        // header carries only the first 80 chars of the line.
        string before = FixtureLoader.LoadText("Fixtures/diff/before_long/file.txt");
        string after = FixtureLoader.LoadText("Fixtures/diff/after_long/file.txt");
        string golden = FixtureLoader.LoadText("Fixtures/diff/expected/longfunc/diff.txt");

        string output = Diff.UnifiedDiff(before, after, new DiffOptions
        {
            ContextLines = 1,
            InterHunkLines = 1,
            IncludeFunctionNames = true,
        });

        Assert.Equal(golden, output);
    }

    [Fact]
    public void UnifiedDiff_FunctionContext_CustomMatcher()
    {
        // Exercises the FunctionContext expansion with a non-default matcher
        // (IsFuncRec via FunctionMatcher branch, not the extractor branch) and
        // the backward GetFuncLine walk that grows the hunk start. A preceding
        // "section" line sits before the change so the hunk header carries a
        // function-name annotation.
        string before =
            "preamble not a section\n" +
            "section one\n" +
            "    a = 1\n" +
            "    b = 2\n" +
            "section two\n" +
            "    c = 3\n" +
            "    d = 4\n";
        string after =
            "preamble not a section\n" +
            "section one\n" +
            "    a = 1\n" +
            "    b = 9\n" +
            "section two\n" +
            "    c = 3\n" +
            "    d = 4\n";

        string output = Diff.UnifiedDiff(before, after, new DiffOptions
        {
            ContextLines = 0,
            InterHunkLines = 0,
            FunctionContext = true,
            IncludeFunctionNames = true,
            FunctionMatcher = static line => line.Length > 0 && line[0] == (byte)'s',
        });

        Assert.Equal(FixtureLoader.LoadBytes("Fixtures/coverage/function_custom.expected.bin"), Encoding.UTF8.GetBytes(output));
    }

    [Fact]
    public void UnifiedDiff_FunctionContext_EmptyLineBeforeFunctionLine()
    {
        // Exercises the IsEmptyRec backward-walk loop (lines that skip blank
        // records immediately preceding a function line during func-context
        // expansion) and the GetFuncLine forward scan in the end-block. A
        // non-matching preamble precedes the function line so the name lookup
        // at s1-1 can find a preceding function record.
        string before =
            "preamble\n" +
            "header alpha\n" +
            "\n" +
            "    body1 = 1\n" +
            "    body2 = 2\n" +
            "\n" +
            "header beta\n" +
            "    body3 = 3\n";
        string after =
            "preamble\n" +
            "header alpha\n" +
            "\n" +
            "    body1 = 1\n" +
            "    body2 = 9\n" +
            "\n" +
            "header beta\n" +
            "    body3 = 3\n";

        string output = Diff.UnifiedDiff(before, after, new DiffOptions
        {
            ContextLines = 0,
            InterHunkLines = 0,
            FunctionContext = true,
            IncludeFunctionNames = true,
            FunctionMatcher = static line => line.Length > 0 && line[0] == (byte)'h',
        });

        Assert.Equal(FixtureLoader.LoadBytes("Fixtures/coverage/function_blank.expected.bin"), Encoding.UTF8.GetBytes(output));
    }

    [Fact]
    public void UnifiedDiff_FunctionContext_AppendsWholeTrailingFunction()
    {
        // Exercises the i1 >= xdf1.Nrec branch (change at end of old file) that
        // scans forward into xdf2 for a function record and, when found,
        // appends the whole function to the hunk end.
        string before =
            "header alpha\n" +
            "    body1 = 1\n" +
            "    body2 = 2\n";
        string after =
            "header alpha\n" +
            "    body1 = 1\n" +
            "    body2 = 2\n" +
            "header beta\n" +
            "    body3 = 3\n";

        string output = Diff.UnifiedDiff(before, after, new DiffOptions
        {
            ContextLines = 0,
            InterHunkLines = 0,
            FunctionContext = true,
            IncludeFunctionNames = true,
            FunctionMatcher = static line => line.Length > 0 && line[0] == (byte)'h',
        });

        Assert.Equal(FixtureLoader.LoadBytes("Fixtures/coverage/function_append.expected.bin"), Encoding.UTF8.GetBytes(output));
    }
}
