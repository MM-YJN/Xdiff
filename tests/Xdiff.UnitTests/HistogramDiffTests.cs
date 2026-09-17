using Xdiff.Algorithms;
using Xdiff.Core;
using Xdiff.Prepare;

namespace Xdiff.UnitTests;

public class HistogramDiffTests
{
    private static List<XdChange> RunDiff(byte[] file1, byte[] file2, bool indentHeuristic = false)
    {
        var env = new XdfEnv();
        FilePreparer.PrepareEnv(file1, file2, WhitespaceMode.None, DiffAlgorithm.Histogram, env);
        XdChange? script = MyersDiff.Run(env, DiffAlgorithm.Histogram, indentHeuristic);

        var changes = new List<XdChange>();
        for (XdChange? c = script; c != null; c = c.Next)
        {
            changes.Add(c);
        }

        return changes;
    }

    [Fact]
    public void Run_IdenticalFiles_ReturnsNull()
    {
        var env = new XdfEnv();
        FilePreparer.PrepareEnv(
            "a\nb\nc\n"u8.ToArray(), "a\nb\nc\n"u8.ToArray(),
            WhitespaceMode.None, DiffAlgorithm.Histogram, env);

        Assert.Null(MyersDiff.Run(env, DiffAlgorithm.Histogram, false));
    }

    [Fact]
    public void Run_InsertAtEnd_ProducesOneAddChange()
    {
        List<XdChange> changes = RunDiff("a\nb\n"u8.ToArray(), "a\nb\nc\n"u8.ToArray());

        XdChange change = Assert.Single(changes);
        Assert.Equal(0, change.Chg1);
        Assert.Equal(1, change.Chg2);
    }

    [Fact]
    public void Run_DeleteFromMiddle_ProducesOneDeleteChange()
    {
        List<XdChange> changes = RunDiff("a\nb\nc\n"u8.ToArray(), "a\nc\n"u8.ToArray());

        XdChange change = Assert.Single(changes);
        Assert.Equal(1, change.Chg1);
        Assert.Equal(0, change.Chg2);
        Assert.Equal(1, change.I1);
        Assert.Equal(1, change.I2);
    }

    [Fact]
    public void Run_ReplaceMiddleLine_ProducesOneReplaceChange()
    {
        List<XdChange> changes = RunDiff("a\nb\nc\n"u8.ToArray(), "a\nB\nc\n"u8.ToArray());

        XdChange change = Assert.Single(changes);
        Assert.Equal(1, change.Chg1);
        Assert.Equal(1, change.Chg2);
        Assert.Equal(1, change.I1);
        Assert.Equal(1, change.I2);
    }

    [Fact]
    public void Run_TwoSeparatedChanges_ProducesTwoHunks()
    {
        List<XdChange> changes = RunDiff(
            "a\nb\nc\nd\ne\n"u8.ToArray(),
            "X\nb\nc\nY\ne\n"u8.ToArray());

        Assert.Equal(2, changes.Count);
        Assert.Equal(0, changes[0].I1);
        Assert.Equal(3, changes[1].I1);
    }

    [Fact]
    public void Run_EmptyOldFile_AllInsertions()
    {
        List<XdChange> changes = RunDiff([], "a\nb\n"u8.ToArray());

        XdChange change = Assert.Single(changes);
        Assert.Equal(0, change.Chg1);
        Assert.Equal(2, change.Chg2);
    }

    [Fact]
    public void Run_EmptyNewFile_AllDeletions()
    {
        List<XdChange> changes = RunDiff("a\nb\n"u8.ToArray(), []);

        XdChange change = Assert.Single(changes);
        Assert.Equal(2, change.Chg1);
        Assert.Equal(0, change.Chg2);
    }

    [Fact]
    public void Run_DuplicateLines_CorrectDiff()
    {
        List<XdChange> changes = RunDiff(
            "a\na\nb\n"u8.ToArray(),
            "a\nb\nb\n"u8.ToArray());

        Assert.NotNull(changes);
        Assert.True(changes.Count >= 1);
    }

    [Fact]
    public void Run_ChangesAreInFileOrder()
    {
        List<XdChange> changes = RunDiff(
            "1\n2\n3\n4\n5\n"u8.ToArray(),
            "A\n2\n3\n4\nB\n"u8.ToArray());

        Assert.Equal(2, changes.Count);
        Assert.True(changes[0].I1 < changes[1].I1);
    }

    [Fact]
    public void Run_LineRepeatedBeyondChainLimit_FallsBackToMyers()
    {
        // A line repeated more than ChainLimit (64) times in file1 has a record
        // count exceeding the histogram threshold, so FindLcs returns the
        // fallback signal and the region is deferred to Myers (FallBackDiff).
        byte[] file1 = BuildRepeated("same\n", 70);
        List<XdChange> changes = RunDiff(file1, "same\n"u8.ToArray());

        Assert.NotEmpty(changes);
        Assert.Equal(69, changes.Sum(c => c.Chg1));
    }

    [Fact]
    public void Run_DuplicateLinesInLcs_TracksMinCount()
    {
        // A line appearing several times in file1 (q x2) that is rarer than a
        // more frequent common predecessor (p) is matched first, then the
        // backward LCS extension steps over a duplicate with rc > 1.
        List<XdChange> changes = RunDiff(
            "p\np\nq\nq\np\n"u8.ToArray(),
            "p\nq\nq\np\n"u8.ToArray());

        Assert.NotEmpty(changes);
        Assert.Equal(1, changes.Sum(c => c.Chg1));
    }

    [Fact]
    public void Run_AlternatingDuplicates_FollowsNextPtrsChain()
    {
        // Many repeated alternating pairs in file1 force TryLcs to walk the
        // NextPtrs linked list past positions consumed by LCS extension,
        // including reaching the chain end (np == 0).
        List<XdChange> changes = RunDiff(
            "a\nb\na\nb\na\nb\na\nb\n"u8.ToArray(),
            "a\nb\na\nb\n"u8.ToArray());

        Assert.NotEmpty(changes);
        Assert.Equal(4, changes.Sum(c => c.Chg1));
    }

    private static byte[] BuildRepeated(string line, int count)
    {
        var sb = new System.Text.StringBuilder(line.Length * count);
        for (int i = 0; i < count; i++)
        {
            sb.Append(line);
        }

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }
}
