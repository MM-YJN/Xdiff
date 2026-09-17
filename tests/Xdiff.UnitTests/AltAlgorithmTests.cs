namespace Xdiff.UnitTests;

public class AltAlgorithmTests
{
    [Fact]
    public void Compute_WithPatience_ProducesStructuredResult()
    {
        DiffResult result = Diff.Compute(
            "a\nb\nc\n"u8.ToArray(),
            "a\nB\nc\n"u8.ToArray(),
            new DiffOptions { Algorithm = DiffAlgorithm.Patience });

        DiffHunk hunk = Assert.Single(result.Hunks);
        Assert.Equal(4, hunk.Lines.Count);
        Assert.Equal(DiffLineKind.Context, hunk.Lines[0].Kind);
        Assert.Equal(DiffLineKind.Deletion, hunk.Lines[1].Kind);
        Assert.Equal(DiffLineKind.Addition, hunk.Lines[2].Kind);
        Assert.Equal(DiffLineKind.Context, hunk.Lines[3].Kind);
    }

    [Fact]
    public void Compute_WithHistogram_ProducesStructuredResult()
    {
        DiffResult result = Diff.Compute(
            "a\nb\nc\n"u8.ToArray(),
            "a\nB\nc\n"u8.ToArray(),
            new DiffOptions { Algorithm = DiffAlgorithm.Histogram });

        DiffHunk hunk = Assert.Single(result.Hunks);
        Assert.Equal(4, hunk.Lines.Count);
        Assert.Equal(DiffLineKind.Context, hunk.Lines[0].Kind);
        Assert.Equal(DiffLineKind.Deletion, hunk.Lines[1].Kind);
        Assert.Equal(DiffLineKind.Addition, hunk.Lines[2].Kind);
        Assert.Equal(DiffLineKind.Context, hunk.Lines[3].Kind);
    }

    [Fact]
    public void UnifiedDiff_WithPatience_MatchesMyers()
    {
        byte[] old = "a\nb\nc\nd\ne\n"u8.ToArray();
        byte[] newer = "a\nB\nc\nD\ne\n"u8.ToArray();

        byte[] myersOutput = Diff.UnifiedDiff(old, newer, new DiffOptions { Algorithm = DiffAlgorithm.Myers });
        byte[] patienceOutput = Diff.UnifiedDiff(old, newer, new DiffOptions { Algorithm = DiffAlgorithm.Patience });

        Assert.Equal(myersOutput, patienceOutput);
    }

    [Fact]
    public void UnifiedDiff_WithHistogram_MatchesMyers()
    {
        byte[] old = "a\nb\nc\nd\ne\n"u8.ToArray();
        byte[] newer = "a\nB\nc\nD\ne\n"u8.ToArray();

        byte[] myersOutput = Diff.UnifiedDiff(old, newer, new DiffOptions { Algorithm = DiffAlgorithm.Myers });
        byte[] histogramOutput = Diff.UnifiedDiff(old, newer, new DiffOptions { Algorithm = DiffAlgorithm.Histogram });

        Assert.Equal(myersOutput, histogramOutput);
    }

    [Fact]
    public void AllAlgorithms_ProduceEquivalentOutput_OnSimpleInsert()
    {
        byte[] old = "a\nb\n"u8.ToArray();
        byte[] newer = "a\nb\nc\n"u8.ToArray();

        byte[][] outputs = new[]
        {
            Diff.UnifiedDiff(old, newer, new DiffOptions { Algorithm = DiffAlgorithm.Myers }),
            Diff.UnifiedDiff(old, newer, new DiffOptions { Algorithm = DiffAlgorithm.Minimal }),
            Diff.UnifiedDiff(old, newer, new DiffOptions { Algorithm = DiffAlgorithm.Patience }),
            Diff.UnifiedDiff(old, newer, new DiffOptions { Algorithm = DiffAlgorithm.Histogram }),
        };

        for (int i = 1; i < outputs.Length; i++)
        {
            Assert.Equal(outputs[0], outputs[i]);
        }
    }

    [Fact]
    public void Compute_WithPatienceAndIgnoreBlankLines_SuppressesBlankChange()
    {
        DiffResult result = Diff.Compute(
            "a\n\nb\n"u8.ToArray(),
            "a\n\n\nb\n"u8.ToArray(),
            new DiffOptions { Algorithm = DiffAlgorithm.Patience, IgnoreBlankLines = true });

        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Compute_WithHistogramAndIgnoreBlankLines_SuppressesBlankChange()
    {
        DiffResult result = Diff.Compute(
            "a\n\nb\n"u8.ToArray(),
            "a\n\n\nb\n"u8.ToArray(),
            new DiffOptions { Algorithm = DiffAlgorithm.Histogram, IgnoreBlankLines = true });

        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void UnifiedDiff_WithPatienceAndIndentHeuristic_DoesNotCrash()
    {
        string output = Diff.UnifiedDiff(
            "def foo():\n",
            "def foo():\n    pass\n",
            new DiffOptions { Algorithm = DiffAlgorithm.Patience, IndentHeuristic = true });

        Assert.Contains("pass", output);
    }

    [Fact]
    public void UnifiedDiff_WithHistogramAndIndentHeuristic_DoesNotCrash()
    {
        string output = Diff.UnifiedDiff(
            "def foo():\n",
            "def foo():\n    pass\n",
            new DiffOptions { Algorithm = DiffAlgorithm.Histogram, IndentHeuristic = true });

        Assert.Contains("pass", output);
    }

    [Fact]
    public void Compute_PatienceEmptyOld_AllInsertions()
    {
        DiffResult result = Diff.Compute(
            Array.Empty<byte>(),
            "a\nb\nc\n"u8.ToArray(),
            new DiffOptions { Algorithm = DiffAlgorithm.Patience });

        DiffHunk hunk = Assert.Single(result.Hunks);
        Assert.Equal(0, hunk.OldCount);
        Assert.Equal(3, hunk.NewCount);
    }

    [Fact]
    public void Compute_HistogramEmptyNew_AllDeletions()
    {
        DiffResult result = Diff.Compute(
            "a\nb\nc\n"u8.ToArray(),
            Array.Empty<byte>(),
            new DiffOptions { Algorithm = DiffAlgorithm.Histogram });

        DiffHunk hunk = Assert.Single(result.Hunks);
        Assert.Equal(3, hunk.OldCount);
        Assert.Equal(0, hunk.NewCount);
    }
}
