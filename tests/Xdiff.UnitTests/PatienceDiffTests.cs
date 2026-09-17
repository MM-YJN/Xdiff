using Xdiff.Algorithms;
using Xdiff.Core;
using Xdiff.Prepare;

namespace Xdiff.UnitTests;

public class PatienceDiffTests
{
    private static List<XdChange> RunDiff(byte[] file1, byte[] file2, bool indentHeuristic = false)
    {
        var env = new XdfEnv();
        FilePreparer.PrepareEnv(file1, file2, WhitespaceMode.None, DiffAlgorithm.Patience, env);
        XdChange? script = MyersDiff.Run(env, DiffAlgorithm.Patience, indentHeuristic);

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
            WhitespaceMode.None, DiffAlgorithm.Patience, env);

        Assert.Null(MyersDiff.Run(env, DiffAlgorithm.Patience, false));
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
    public void Run_UniqueAnchorLines_CorrectDiff()
    {
        List<XdChange> changes = RunDiff(
            "header\nold1\nold2\nfooter\n"u8.ToArray(),
            "header\nnew1\nnew2\nfooter\n"u8.ToArray());

        XdChange change = Assert.Single(changes);
        Assert.Equal(2, change.Chg1);
        Assert.Equal(2, change.Chg2);
        Assert.Equal(1, change.I1);
        Assert.Equal(1, change.I2);
    }

    [Fact]
    public void Run_DuplicateLines_FallsBackToMyers()
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
}
