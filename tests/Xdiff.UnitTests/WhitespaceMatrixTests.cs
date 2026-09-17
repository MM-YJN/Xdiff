using System.Text;

namespace Xdiff.UnitTests;

public class WhitespaceMatrixTests
{
    [Fact]
    public void Eol_IgnoreWhitespaceEol_CleanMerge_MatchesUpstream()
    {
        byte[] ancestor = FixtureLoader.LoadBytes("Fixtures/merge_whitespace/ancestor_eol.txt");
        byte[] ours = FixtureLoader.LoadBytes("Fixtures/merge_whitespace/branch_a_eol.txt");
        byte[] theirs = FixtureLoader.LoadBytes("Fixtures/merge_whitespace/branch_b_eol.txt");
        string expected = FixtureLoader.LoadText("Fixtures/merge_whitespace/expected_eol.txt");

        var opts = new MergeOptions { Whitespace = WhitespaceMode.IgnoreAtEol };
        MergeResult result = Merger.Merge(ancestor, ours, theirs, opts);

        Assert.Equal(0, result.ConflictCount);
        Assert.Equal(expected, Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Change_IgnoreWhitespaceChange_CleanMerge_MatchesUpstream()
    {
        byte[] ancestor = FixtureLoader.LoadBytes("Fixtures/merge_whitespace/ancestor_change.txt");
        byte[] ours = FixtureLoader.LoadBytes("Fixtures/merge_whitespace/branch_a_change.txt");
        byte[] theirs = FixtureLoader.LoadBytes("Fixtures/merge_whitespace/branch_b_change.txt");
        string expected = FixtureLoader.LoadText("Fixtures/merge_whitespace/expected_change.txt");

        var opts = new MergeOptions { Whitespace = WhitespaceMode.IgnoreChanges };
        MergeResult result = Merger.Merge(ancestor, ours, theirs, opts);

        Assert.Equal(0, result.ConflictCount);
        Assert.Equal(expected, Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Eol_NoWhitespaceFlags_Conflict_MatchesUpstream()
    {
        byte[] ancestor = FixtureLoader.LoadBytes("Fixtures/merge_whitespace/ancestor_eol.txt");
        byte[] ours = FixtureLoader.LoadBytes("Fixtures/merge_whitespace/branch_a_eol.txt");
        byte[] theirs = FixtureLoader.LoadBytes("Fixtures/merge_whitespace/branch_b_eol.txt");
        string expected = FixtureLoader.LoadText("Fixtures/merge_whitespace/expected_conflict.txt");

        var opts = new MergeOptions
        {
            OurLabel = "HEAD",
            TheirLabel = "branch_b_eol",
        };
        MergeResult result = Merger.Merge(ancestor, ours, theirs, opts);

        Assert.True(result.ConflictCount >= 1);
        Assert.Equal(expected, Encoding.UTF8.GetString(result.Content));
    }
}
