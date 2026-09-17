using System.Text;

namespace Xdiff.UnitTests;

public class RepoMergeTests
{
    private const string ConflictLabel = "7cb63eed597130ba4abb87b3e544b85021905520";

    [Fact]
    public void Automergeable_CleanMerge_MatchesUpstream()
    {
        byte[] ancestor = FixtureLoader.LoadBytes("Fixtures/merge_resolve/ancestor/automergeable.txt");
        byte[] ours = FixtureLoader.LoadBytes("Fixtures/merge_resolve/master/automergeable.txt");
        byte[] theirs = FixtureLoader.LoadBytes("Fixtures/merge_resolve/branch/automergeable.txt");

        MergeResult result = Merger.Merge(ancestor, ours, theirs);

        Assert.Equal(0, result.ConflictCount);
        string expected = "this file is changed in master\n" +
                       "this file is automergeable\n" +
                       "this file is automergeable\n" +
                       "this file is automergeable\n" +
                       "this file is automergeable\n" +
                       "this file is automergeable\n" +
                       "this file is automergeable\n" +
                       "this file is automergeable\n" +
                       "this file is changed in branch\n";
        Assert.Equal(expected, Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Conflicting_OverlapConflict_MatchesUpstream()
    {
        byte[] ancestor = FixtureLoader.LoadBytes("Fixtures/merge_resolve/ancestor/conflicting.txt");
        byte[] ours = FixtureLoader.LoadBytes("Fixtures/merge_resolve/master/conflicting.txt");
        byte[] theirs = FixtureLoader.LoadBytes("Fixtures/merge_resolve/branch/conflicting.txt");

        var opts = new MergeOptions
        {
            OurLabel = "HEAD",
            TheirLabel = ConflictLabel,
        };
        MergeResult result = Merger.Merge(ancestor, ours, theirs, opts);

        Assert.Equal(1, result.ConflictCount);
        string expected = "<<<<<<< HEAD\n" +
                       "this file is changed in master and branch\n" +
                       "=======\n" +
                       "this file is changed in branch and master\n" +
                       $">>>>>>> {ConflictLabel}\n";
        Assert.Equal(expected, Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Conflicting_Diff3Style_MatchesUpstream()
    {
        byte[] ancestor = FixtureLoader.LoadBytes("Fixtures/merge_resolve/ancestor/conflicting.txt");
        byte[] ours = FixtureLoader.LoadBytes("Fixtures/merge_resolve/master/conflicting.txt");
        byte[] theirs = FixtureLoader.LoadBytes("Fixtures/merge_resolve/branch/conflicting.txt");

        var opts = new MergeOptions
        {
            Style = MergeStyle.Diff3,
            OurLabel = "HEAD",
            TheirLabel = ConflictLabel,
            AncestorLabel = "initial",
        };
        MergeResult result = Merger.Merge(ancestor, ours, theirs, opts);

        Assert.Equal(1, result.ConflictCount);
        string expected = "<<<<<<< HEAD\n" +
                       "this file is changed in master and branch\n" +
                       "||||||| initial\n" +
                       "this file is a conflict\n" +
                       "=======\n" +
                       "this file is changed in branch and master\n" +
                       $">>>>>>> {ConflictLabel}\n";
        Assert.Equal(expected, Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Conflicting_FavorOurs_MatchesUpstream()
    {
        byte[] ancestor = FixtureLoader.LoadBytes("Fixtures/merge_resolve/ancestor/conflicting.txt");
        byte[] ours = FixtureLoader.LoadBytes("Fixtures/merge_resolve/master/conflicting.txt");
        byte[] theirs = FixtureLoader.LoadBytes("Fixtures/merge_resolve/branch/conflicting.txt");

        var opts = new MergeOptions { Favor = MergeFavor.Ours };
        MergeResult result = Merger.Merge(ancestor, ours, theirs, opts);

        Assert.Equal(0, result.ConflictCount);
        Assert.Equal("this file is changed in master and branch\n", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Conflicting_FavorTheirs_MatchesUpstream()
    {
        byte[] ancestor = FixtureLoader.LoadBytes("Fixtures/merge_resolve/ancestor/conflicting.txt");
        byte[] ours = FixtureLoader.LoadBytes("Fixtures/merge_resolve/master/conflicting.txt");
        byte[] theirs = FixtureLoader.LoadBytes("Fixtures/merge_resolve/branch/conflicting.txt");

        var opts = new MergeOptions { Favor = MergeFavor.Theirs };
        MergeResult result = Merger.Merge(ancestor, ours, theirs, opts);

        Assert.Equal(0, result.ConflictCount);
        Assert.Equal("this file is changed in branch and master\n", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Conflicting_FavorUnion_MatchesUpstream()
    {
        byte[] ancestor = FixtureLoader.LoadBytes("Fixtures/merge_resolve/ancestor/conflicting.txt");
        byte[] ours = FixtureLoader.LoadBytes("Fixtures/merge_resolve/master/conflicting.txt");
        byte[] theirs = FixtureLoader.LoadBytes("Fixtures/merge_resolve/branch/conflicting.txt");

        var opts = new MergeOptions { Favor = MergeFavor.Union };
        MergeResult result = Merger.Merge(ancestor, ours, theirs, opts);

        Assert.Equal(0, result.ConflictCount);
        string expected = "this file is changed in master and branch\n" +
                       "this file is changed in branch and master\n";
        Assert.Equal(expected, Encoding.UTF8.GetString(result.Content));
    }
}
