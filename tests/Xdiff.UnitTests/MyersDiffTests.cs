using Xdiff.Algorithms;
using Xdiff.Core;
using Xdiff.Prepare;

namespace Xdiff.UnitTests;

public class MyersDiffTests
{
    private static List<XdChange> RunDiff(
        byte[] file1, byte[] file2,
        DiffAlgorithm alg = DiffAlgorithm.Myers,
        bool indentHeuristic = false)
    {
        var env = new XdfEnv();
        FilePreparer.PrepareEnv(file1, file2, WhitespaceMode.None, alg, env);
        XdChange? script = MyersDiff.Run(env, alg, indentHeuristic);

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
            WhitespaceMode.None, DiffAlgorithm.Myers, env);

        Assert.Null(MyersDiff.Run(env, DiffAlgorithm.Myers, false));
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
    public void Run_MinimalAlgorithm_ProducesSameResultAsMyers()
    {
        List<XdChange> myersChanges = RunDiff(
            "a\nb\nc\nd\n"u8.ToArray(),
            "a\nB\nc\nD\n"u8.ToArray(),
            DiffAlgorithm.Myers);

        List<XdChange> minimalChanges = RunDiff(
            "a\nb\nc\nd\n"u8.ToArray(),
            "a\nB\nc\nD\n"u8.ToArray(),
            DiffAlgorithm.Minimal);

        Assert.Equal(myersChanges.Count, minimalChanges.Count);
    }

    [Fact]
    public void Run_IndentHeuristic_DoesNotCrash()
    {
        List<XdChange> changes = RunDiff(
            "def foo():\n"u8.ToArray(),
            "def foo():\n    pass\n"u8.ToArray(),
            DiffAlgorithm.Myers,
            indentHeuristic: true);

        Assert.Single(changes);
    }

    [Fact]
    public void Run_ChangesAreInFileOrder()
    {
        List<XdChange> changes = RunDiff(
            "1\n2\n3\n4\n5\n"u8.ToArray(),
            "A\n2\n3\n4\nB\n"u8.ToArray());

        Assert.Equal(2, changes.Count);
        Assert.True(changes[0].I1 < changes[1].I1);
        Assert.True(changes[0].I2 < changes[1].I2);
    }

    [Fact]
    public void Run_SlideMergeConsecutiveGroups_ConsumesAdjacentChanges()
    {
        // Two change groups (A,A->C,C and B,B->D,D) separated by a duplicate 'q'
        // (which the rare-line classifier marks Ha=0). The matching zero hashes
        // let GroupSlideUp/Down slide one group into the other, exercising the
        // consecutive-change consume loops.
        List<XdChange> changes = RunDiff(
            "q\nA\nA\nq\nB\nB\n"u8.ToArray(),
            "q\nC\nC\nq\nD\nD\n"u8.ToArray());

        Assert.NotEmpty(changes);
    }

    [Fact]
    public void UnifiedDiff_LargeInput_ScatteredChanges_MatchesGolden()
    {
        // ~3000 lines with ~400 scattered edits: edit cost exceeds HeurMinCost
        // (256) while long matching runs provide snakes, exercising the Myers
        // heuristic best-split scan. Golden captured from xdiff.
        string before = FixtureLoader.LoadText("Fixtures/myers_heuristic/before.txt");
        string after = FixtureLoader.LoadText("Fixtures/myers_heuristic/after.txt");
        string golden = FixtureLoader.LoadText("Fixtures/myers_heuristic/expected.txt");

        string output = Diff.UnifiedDiff(before, after, new DiffOptions { Algorithm = DiffAlgorithm.Myers });

        Assert.Equal(golden, output);
    }

    [Fact]
    public void UnifiedDiff_LargeInput_NearDisjoint_MatchesGolden()
    {
        // ~3000 near-disjoint lines: edit cost climbs to Mxcost without an early
        // midpoint, exercising the cost-cap fallback (fbest/bbest clamp).
        // Golden captured from xdiff.
        string before = FixtureLoader.LoadText("Fixtures/myers_mxcost/before.txt");
        string after = FixtureLoader.LoadText("Fixtures/myers_mxcost/after.txt");
        string golden = FixtureLoader.LoadText("Fixtures/myers_mxcost/expected.txt");

        string output = Diff.UnifiedDiff(before, after, new DiffOptions { Algorithm = DiffAlgorithm.Myers });

        Assert.Equal(golden, output);
    }
}
