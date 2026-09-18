using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace Xdiff.UnitTests;

public class XDiffFacadeTests
{
    public static TheoryData<string, int, string, string> NegativeContextOptions
    {
        get
        {
            var data = new TheoryData<string, int, string, string>();
            foreach (string property in new[] { nameof(DiffOptions.ContextLines), nameof(DiffOptions.InterHunkLines) })
            {
                foreach (int value in new[] { -1, int.MinValue })
                {
                    data.Add(property, value, "a\n", "b\n");
                    data.Add(property, value, "a\n", "a\n");
                    data.Add(property, value, "", "");
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(NegativeContextOptions))]
    public void Diff_NegativeContextOption_Throws(string property, int value, string oldText, string newText)
    {
        DiffOptions options = property == nameof(DiffOptions.ContextLines)
            ? new DiffOptions { ContextLines = value }
            : new DiffOptions { InterHunkLines = value };

        AssertInvalidOptions(oldText, newText, options, property, value);
    }

    [Theory]
    [InlineData("a\n", "b\n")]
    [InlineData("a\n", "a\n")]
    [InlineData("", "")]
    public void Diff_BothContextOptionsNegative_ReportsContextLinesFirst(string oldText, string newText)
    {
        var options = new DiffOptions { ContextLines = -1, InterHunkLines = int.MinValue };
        AssertInvalidOptions(oldText, newText, options, nameof(DiffOptions.ContextLines), -1);
    }

    private static void AssertInvalidOptions(string oldText, string newText, DiffOptions options, string property, int value)
    {
        byte[] oldData = Encoding.UTF8.GetBytes(oldText);
        byte[] newData = Encoding.UTF8.GetBytes(newText);
        Action[] entryPoints =
        [
            () => Diff.Compute(oldData, newData, options),
            () => Diff.UnifiedDiff(oldText, newText, options),
            () => Diff.UnifiedDiff(oldData, newData, options),
        ];

        foreach (Action entryPoint in entryPoints)
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(property, entryPoint);
            Assert.Equal(value, Assert.IsType<int>(exception.ActualValue));
        }
    }

    [Theory]
    [InlineData(0, 0, "@@ -2 +2 @@\n-b\n+B\n@@ -5 +5 @@\n-e\n+E\n", 2, 1)]
    [InlineData(int.MaxValue, 0, "@@ -1,6 +1,6 @@\n a\n-b\n+B\n c\n d\n-e\n+E\n f\n", 1, 6)]
    [InlineData(0, int.MaxValue, "@@ -2,4 +2,4 @@\n-b\n+B\n c\n d\n-e\n+E\n", 2, 4)]
    [InlineData(int.MaxValue, int.MaxValue, "@@ -1,6 +1,6 @@\n a\n-b\n+B\n c\n d\n-e\n+E\n f\n", 1, 6)]
    public void Diff_ContextBoundaries_ProduceExpectedHunks(int contextLines, int interHunkLines, string expected, int start, int count)
    {
        const string OldText = "a\nb\nc\nd\ne\nf\n";
        const string NewText = "a\nB\nc\nd\nE\nf\n";
        byte[] oldData = Encoding.UTF8.GetBytes(OldText);
        byte[] newData = Encoding.UTF8.GetBytes(NewText);
        var options = new DiffOptions { ContextLines = contextLines, InterHunkLines = interHunkLines };

        Assert.Equal(expected, Diff.UnifiedDiff(OldText, NewText, options));
        Assert.Equal(Encoding.UTF8.GetBytes(expected), Diff.UnifiedDiff(oldData, newData, options));

        DiffResult result = Diff.Compute(oldData, newData, options);
        Assert.Equal(contextLines == 0 && interHunkLines == 0 ? 2 : 1, result.Hunks.Count);
        DiffHunk first = result.Hunks[0];
        Assert.Equal((start, count, start, count), (first.OldStart, first.OldCount, first.NewStart, first.NewCount));
        if (result.Hunks.Count == 2)
        {
            DiffHunk second = result.Hunks[1];
            Assert.Equal((5, 1, 5, 1), (second.OldStart, second.OldCount, second.NewStart, second.NewCount));
        }

        string[] changedLines = result.Hunks.SelectMany(h => h.Lines)
            .Where(line => line.Kind != DiffLineKind.Context)
            .Select(line => (line.Kind == DiffLineKind.Deletion ? "-" : "+") + Encoding.UTF8.GetString(line.Content.Span))
            .ToArray();
        Assert.Equal(["-b\n", "+B\n", "-e\n", "+E\n"], changedLines);
    }

    [Fact]
    public void Compute_IdenticalFiles_ReturnsEmpty()
    {
        byte[] data = "a\nb\nc\n"u8.ToArray();
        DiffResult result = Diff.Compute(data, data);
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void UnifiedDiff_IdenticalFiles_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, Diff.UnifiedDiff("a\nb\nc\n", "a\nb\nc\n"));
    }

    [Fact]
    public void Compute_SingleInsert_ProducesOneHunkWithAddition()
    {
        DiffResult result = Diff.Compute("a\nb\n"u8.ToArray(), "a\nb\nc\n"u8.ToArray());

        DiffHunk hunk = Assert.Single(result.Hunks);
        Assert.Equal(1, hunk.OldStart);
        Assert.Equal(2, hunk.OldCount);
        Assert.Equal(1, hunk.NewStart);
        Assert.Equal(3, hunk.NewCount);

        Assert.Equal(3, hunk.Lines.Count);
        Assert.Equal(DiffLineKind.Context, hunk.Lines[0].Kind);
        Assert.Equal(1, hunk.Lines[0].OldLine);
        Assert.Equal(1, hunk.Lines[0].NewLine);
        Assert.Equal(DiffLineKind.Context, hunk.Lines[1].Kind);
        Assert.Equal(2, hunk.Lines[1].OldLine);
        Assert.Equal(2, hunk.Lines[1].NewLine);
        Assert.Equal(DiffLineKind.Addition, hunk.Lines[2].Kind);
        Assert.Equal(0, hunk.Lines[2].OldLine);
        Assert.Equal(3, hunk.Lines[2].NewLine);
    }

    [Fact]
    public void UnifiedDiff_SingleInsert_ByteExact()
    {
        string output = Diff.UnifiedDiff("a\nb\n", "a\nb\nc\n");
        Assert.Equal("@@ -1,2 +1,3 @@\n a\n b\n+c\n", output);
    }

    [Fact]
    public void UnifiedDiff_SingleDelete_ByteExact()
    {
        string output = Diff.UnifiedDiff("a\nb\nc\n", "a\nc\n");
        Assert.Equal("@@ -1,3 +1,2 @@\n a\n-b\n c\n", output);
    }

    [Fact]
    public void UnifiedDiff_ReplaceOneLine_ByteExact()
    {
        string output = Diff.UnifiedDiff("a\nb\nc\n", "a\nB\nc\n");
        Assert.Equal("@@ -1,3 +1,3 @@\n a\n-b\n+B\n c\n", output);
    }

    [Fact]
    public void UnifiedDiff_EmptyOld_AllAdditions()
    {
        string output = Diff.UnifiedDiff("", "a\nb\nc\n");
        Assert.Equal("@@ -0,0 +1,3 @@\n+a\n+b\n+c\n", output);
    }

    [Fact]
    public void UnifiedDiff_EmptyNew_AllDeletions()
    {
        string output = Diff.UnifiedDiff("a\nb\n", "");
        Assert.Equal("@@ -1,2 +0,0 @@\n-a\n-b\n", output);
    }

    [Fact]
    public void UnifiedDiff_TwoFarChanges_ProduceTwoHunks()
    {
        string output = Diff.UnifiedDiff(
            "1\n2\n3\n4\n5\n6\n7\n8\n9\n",
            "A\n2\n3\n4\n5\n6\n7\n8\nB\n");
        Assert.Equal(
            "@@ -1,4 +1,4 @@\n-1\n+A\n 2\n 3\n 4\n" +
            "@@ -6,4 +6,4 @@\n 6\n 7\n 8\n-9\n+B\n",
            output);
    }

    [Fact]
    public void UnifiedDiff_TwoCloseChanges_MergeIntoOneHunk()
    {
        string output = Diff.UnifiedDiff(
            "a\nb\nc\nd\ne\n",
            "X\nb\nc\nY\ne\n");
        Assert.Equal("@@ -1,5 +1,5 @@\n-a\n+X\n b\n c\n-d\n+Y\n e\n", output);
    }

    [Fact]
    public void UnifiedDiff_NoNewlineAtEof_OldSide()
    {
        string output = Diff.UnifiedDiff("a\nb", "a\nb\nc\n");
        Assert.Equal(
            "@@ -1,2 +1,3 @@\n a\n-b\n\\ No newline at end of file\n+b\n+c\n",
            output);
    }

    [Fact]
    public void UnifiedDiff_NoNewlineAtEof_BothSides()
    {
        string output = Diff.UnifiedDiff("a", "b");
        Assert.Equal(
            "@@ -1 +1 @@\n-a\n\\ No newline at end of file\n+b\n\\ No newline at end of file\n",
            output);
    }

    [Fact]
    public void UnifiedDiff_ContextLinesOne_ReducesContext()
    {
        string output = Diff.UnifiedDiff(
            "a\nb\nc\nd\ne\n",
            "a\nb\nC\nd\ne\n",
            new DiffOptions { ContextLines = 1 });
        Assert.Equal("@@ -2,3 +2,3 @@\n b\n-c\n+C\n d\n", output);
    }

    [Fact]
    public void Compute_IgnoreBlankLines_SuppressesBlankOnlyChange()
    {
        var opts = new DiffOptions { IgnoreBlankLines = true };
        DiffResult result = Diff.Compute("a\n\nb\n"u8.ToArray(), "a\nb\n"u8.ToArray(), opts);
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Compute_WithoutIgnoreBlankLines_ShowsBlankChange()
    {
        DiffResult result = Diff.Compute("a\n\nb\n"u8.ToArray(), "a\nb\n"u8.ToArray());
        Assert.Single(result.Hunks);
    }

    [Fact]
    public void Compute_IncludeFunctionNames_DefaultMatcher()
    {
        byte[] oldData = "def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 1\n"u8.ToArray();
        byte[] newData = "def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 2\n"u8.ToArray();
        var opts = new DiffOptions { IncludeFunctionNames = true };

        DiffResult result = Diff.Compute(oldData, newData, opts);

        DiffHunk hunk = Assert.Single(result.Hunks);
        Assert.Equal("def foo():"u8.ToArray(), hunk.FunctionName.ToArray());
    }

    [Fact]
    public void UnifiedDiff_IncludeFunctionNames_DefaultMatcher()
    {
        var opts = new DiffOptions { IncludeFunctionNames = true };
        string output = Diff.UnifiedDiff(
            "def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 1\n",
            "def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 2\n",
            opts);
        Assert.StartsWith("@@ -2,4 +2,4 @@ def foo():\n", output);
    }

    [Fact]
    public void UnifiedDiff_FunctionContext_MatchesCGolden()
    {
        var opts = new DiffOptions { FunctionContext = true };
        string output = Diff.UnifiedDiff(
            "def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 1\n",
            "def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 2\n",
            opts);
        Assert.Equal(FixtureLoader.LoadBytes("Fixtures/coverage/function_smoke.expected.bin"), Encoding.UTF8.GetBytes(output));
    }

    [Fact]
    public void UnifiedDiff_CustomFunctionMatcher()
    {
        var opts = new DiffOptions
        {
            IncludeFunctionNames = true,
            FunctionMatcher = static line => line.Length > 0 && line[0] == (byte)'#',
        };
        string output = Diff.UnifiedDiff(
            "# header\n    a = 1\n    b = 2\n    c = 3\n    x = 1\n",
            "# header\n    a = 1\n    b = 2\n    c = 3\n    x = 2\n",
            opts);
        Assert.StartsWith("@@ -2,4 +2,4 @@ # header\n", output);
    }

    [Fact]
    public void UnifiedDiff_ByteOverload_ReturnsBytes()
    {
        byte[] bytes = Diff.UnifiedDiff("a\n"u8.ToArray(), "b\n"u8.ToArray());
        Assert.Equal(Encoding.UTF8.GetBytes("@@ -1 +1 @@\n-a\n+b\n"), bytes);
    }

    [Theory]
    [InlineData("a\nb\nc\n", "a\nB\nc\n")]
    [InlineData("a", "b")]
    [InlineData("", "a\nb\n")]
    [InlineData("a\nb\nc\n", "")]
    [InlineData("1\n2\n3\n4\n5\n6\n7\n8\n9\n", "A\n2\n3\n4\n5\n6\n7\n8\nB\n")]
    public void UnifiedDiff_WriterOverload_MatchesByteOverload(string oldText, string newText)
    {
        byte[] oldBytes = Encoding.UTF8.GetBytes(oldText);
        byte[] newBytes = Encoding.UTF8.GetBytes(newText);
        byte[] expected = Diff.UnifiedDiff(oldBytes, newBytes);

        var writer = new ArrayBufferWriter<byte>();
        Diff.UnifiedDiff(writer, oldBytes, newBytes);

        Assert.Equal(expected, writer.WrittenSpan.ToArray());
    }

    [Fact]
    public void UnifiedDiff_WriterOverload_FunctionNames_MatchesByteOverload()
    {
        var opts = new DiffOptions { IncludeFunctionNames = true };
        string oldText = "def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 1\n";
        string newText = "def foo():\n    a = 1\n    b = 2\n    c = 3\n    x = 2\n";
        byte[] oldBytes = Encoding.UTF8.GetBytes(oldText);
        byte[] newBytes = Encoding.UTF8.GetBytes(newText);
        byte[] expected = Diff.UnifiedDiff(oldBytes, newBytes, opts);

        var writer = new ArrayBufferWriter<byte>();
        Diff.UnifiedDiff(writer, oldBytes, newBytes, opts);

        Assert.Equal(expected, writer.WrittenSpan.ToArray());
    }

    [Fact]
    public void UnifiedDiff_WriterOverload_AppendsToExistingContent()
    {
        var writer = new ArrayBufferWriter<byte>();
        writer.Write("prefix"u8);

        Diff.UnifiedDiff(writer, "a\n"u8.ToArray(), "b\n"u8.ToArray());

        Assert.Equal("prefix@@ -1 +1 @@\n-a\n+b\n"u8.ToArray(), writer.WrittenSpan.ToArray());
    }

    [Fact]
    public void UnifiedDiff_WriterOverload_IdenticalInputs_WritesNothing()
    {
        var writer = new ArrayBufferWriter<byte>();

        Diff.UnifiedDiff(writer, "same\n"u8.ToArray(), "same\n"u8.ToArray());

        Assert.Empty(writer.WrittenSpan.ToArray());
    }

    [Fact]
    public void UnifiedDiff_WriterOverload_NullWriter_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => Diff.UnifiedDiff((IBufferWriter<byte>)null!, "a\n"u8.ToArray(), "b\n"u8.ToArray()));
    }

    [Fact]
    public void UnifiedDiff_StringOverload_NonAscii_MatchesByteOverload()
    {
        // Exercises the pooled-rent path in UnifiedDiff(string, string): the
        // UTF-8 byte count differs from the char count, so the encoded ranges
        // must be truncated to the number of bytes actually written.
        string oldText = "café 中文 🙂\n    a = 1\n";
        string newText = "café 中文 🙂\n    a = 2\n";

        byte[] expected = Diff.UnifiedDiff(
            Encoding.UTF8.GetBytes(oldText),
            Encoding.UTF8.GetBytes(newText));

        Assert.Equal(expected, Encoding.UTF8.GetBytes(Diff.UnifiedDiff(oldText, newText)));
    }

    [Fact]
    public void Compute_WithoutFunctionNames_FunctionNameIsEmpty()
    {
        DiffResult result = Diff.Compute("a\nb\n"u8.ToArray(), "a\nB\n"u8.ToArray());

        DiffHunk hunk = Assert.Single(result.Hunks);
        Assert.True(hunk.FunctionName.IsEmpty);
    }

    [Fact]
    public void UnifiedDiff_IncludeFunctionNames_NoMatchingLine_OmitsFuncname()
    {
        // No line matches the default matcher, so the header must end at "@@"
        // with no trailing space (the empty-funcname guard).
        var opts = new DiffOptions { IncludeFunctionNames = true };
        string output = Diff.UnifiedDiff("    a = 1\n    b = 2\n", "    a = 1\n    b = 3\n", opts);
        Assert.Equal("@@ -1,2 +1,2 @@\n     a = 1\n-    b = 2\n+    b = 3\n", output);
    }

    [Fact]
    public void Compute_DiffLineContent_BorrowsFromCallerBuffer()
    {
        byte[] oldData = "a\nb\n"u8.ToArray();
        byte[] newData = "a\nb\nc\n"u8.ToArray();
        DiffResult result = Diff.Compute(oldData, newData);

        DiffLine addedLine = result.Hunks[0].Lines[2];
        Assert.Equal(DiffLineKind.Addition, addedLine.Kind);
        Assert.Equal("c\n"u8.ToArray(), addedLine.Content.ToArray());

        // Lock in the zero-copy guarantee: the line aliases the caller's buffer.
        Assert.True(MemoryMarshal.TryGetArray(addedLine.Content, out ArraySegment<byte> seg));
        Assert.True(ReferenceEquals(seg.Array, newData));
    }

    [Fact]
    public void Compute_TwoFarChanges_ProducesTwoHunks()
    {
        // Exercises StructuredSink.HunkHeader's flush-on-second-hunk path: the
        // first hunk is materialized only when the second HunkHeader arrives.
        DiffResult result = Diff.Compute(
            "1\n2\n3\n4\n5\n6\n7\n8\n9\n"u8.ToArray(),
            "A\n2\n3\n4\n5\n6\n7\n8\nB\n"u8.ToArray());

        Assert.Equal(2, result.Hunks.Count);
    }

    [Fact]
    public void Compute_IgnoreAtEol_KeepsInterWordWhitespace()
    {
        // Diff-level IgnoreAtEol: inter-word whitespace is significant, so the two
        // buffers still differ (hits the Hashing IgnoreAtEol !atEol branch).
        var opts = new DiffOptions { Whitespace = WhitespaceMode.IgnoreAtEol };
        DiffResult result = Diff.Compute("a b\nc\n"u8.ToArray(), "a  b\nc\n"u8.ToArray(), opts);

        Assert.Single(result.Hunks);
    }

    [Fact]
    public void Compute_IgnoreBlankLines_BlankChangesBetweenRealChanges()
    {
        // Ignorable (blank-only) changes at distance == ContextLines (3) and then
        // distance 1 drive HunkGrouper's two ignorable-accumulation branches
        // (the far-ignorable else and the close-ignorable-after-lag branch).
        var opts = new DiffOptions { IgnoreBlankLines = true };
        DiffResult result = Diff.Compute(
            "x\na\nb\nc\n\nd\n\ne\n"u8.ToArray(),
            "X\na\nb\nc\nd\ne\n"u8.ToArray(),
            opts);

        Assert.NotEmpty(result.Hunks);
        Assert.Contains(result.Hunks, h => h.Lines.Any(l => l.Content.Span.StartsWith("X"u8)));
    }

    [Fact]
    public void Compute_IgnoreBlankLines_FarRealChangeBreaksHunk()
    {
        // A real change whose distance from the lagging anchor (past an ignorable
        // change) exceeds 2*ContextLines+InterHunkLines trips HunkGrouper's
        // lxch-anchored break, splitting into separate hunks.
        var opts = new DiffOptions { IgnoreBlankLines = true };
        DiffResult result = Diff.Compute(
            "x\na\nb\nc\n\nd\ne\nf\ny\ng\n"u8.ToArray(),
            "X\na\nb\nc\nd\ne\nf\nY\ng\n"u8.ToArray(),
            opts);

        Assert.Equal(2, result.Hunks.Count);
    }
}
