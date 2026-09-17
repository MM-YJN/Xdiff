using System.Text;

namespace Xdiff.UnitTests;

public class GoldenMergeTests
{
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void AutomergeFromBufs_MatchesUpstream()
    {
        MergeResult result = Merger.Merge(
            B("0\n1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n"),
            B("Zero\n1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n"),
            B("0\n1\n2\n3\n4\n5\n6\n7\n8\n9\nTen\n"));

        Assert.Equal(0, result.ConflictCount);
        Assert.Equal("Zero\n1\n2\n3\n4\n5\n6\n7\n8\n9\nTen\n", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void ConflictFromBufs_MatchesUpstream()
    {
        var opts = new MergeOptions
        {
            OurLabel = "testfile.txt",
            TheirLabel = "theirs.txt",
        };
        MergeResult result = Merger.Merge(
            B("Hello!\nAncestor!\n"),
            B("Aloha!\nOurs.\n"),
            B("Hi!\nTheirs.\n"),
            opts);

        Assert.Equal(1, result.ConflictCount);
        string expected = "<<<<<<< testfile.txt\nAloha!\nOurs.\n=======\nHi!\nTheirs.\n>>>>>>> theirs.txt\n";
        Assert.Equal(expected, Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void AutomergeWhitespaceEol_MatchesUpstream()
    {
        var opts = new MergeOptions { Whitespace = WhitespaceMode.IgnoreAtEol };
        MergeResult result = Merger.Merge(
            B("0 \n1\n2\n3\n4\n5\n6\n7\n8\n9\n10 \n"),
            B("Zero\n1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n"),
            B("0\n1\n2\n3\n4\n5\n6\n7\n8\n9\nTen\n"),
            opts);

        Assert.Equal(0, result.ConflictCount);
        Assert.Equal("Zero\n1\n2\n3\n4\n5\n6\n7\n8\n9\nTen\n", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void AutomergeWhitespaceChange_MatchesUpstream()
    {
        var opts = new MergeOptions { Whitespace = WhitespaceMode.IgnoreChanges };
        MergeResult result = Merger.Merge(
            B("0\n1\n2\n3\n4\n5 XXX\n6YYY\n7\n8\n9\n10\n"),
            B("Zero\n1\n2\n3\n4\n5 XXX\n6 YYY\n7\n8\n9\n10\n"),
            B("0\n1\n2\n3\n4\n5 XXX\n6  YYY\n7\n8\n9\nTen\n"),
            opts);

        Assert.Equal(0, result.ConflictCount);
        Assert.Equal("Zero\n1\n2\n3\n4\n5 XXX\n6 YYY\n7\n8\n9\nTen\n", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void DoesntAddNewline_MatchesUpstream()
    {
        var opts = new MergeOptions { Whitespace = WhitespaceMode.IgnoreChanges };
        MergeResult result = Merger.Merge(
            B("0\n1\n2\n3\n4\n5 XXX\n6YYY\n7\n8\n9\n10"),
            B("Zero\n1\n2\n3\n4\n5 XXX\n6 YYY\n7\n8\n9\n10"),
            B("0\n1\n2\n3\n4\n5 XXX\n6  YYY\n7\n8\n9\nTen"),
            opts);

        Assert.Equal(0, result.ConflictCount);
        Assert.Equal("Zero\n1\n2\n3\n4\n5 XXX\n6 YYY\n7\n8\n9\nTen", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void CrlfConflictMarkers_MatchesUpstream()
    {
        var opts = new MergeOptions
        {
            OurLabel = "file.txt",
            TheirLabel = "file.txt",
            AncestorLabel = "file.txt",
        };
        MergeResult result = Merger.Merge(
            B("This file has\r\nCRLF line endings.\r\n"),
            B("This file\r\ndoes, too.\r\n"),
            B("And so does\r\nthis one.\r\n"),
            opts);

        Assert.Equal(1, result.ConflictCount);
        string expected = "<<<<<<< file.txt\r\nThis file\r\ndoes, too.\r\n=======\r\nAnd so does\r\nthis one.\r\n>>>>>>> file.txt\r\n";
        Assert.Equal(expected, Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void CrlfConflictMarkers_Diff3_MatchesUpstream()
    {
        var opts = new MergeOptions
        {
            Style = MergeStyle.Diff3,
            OurLabel = "file.txt",
            TheirLabel = "file.txt",
            AncestorLabel = "file.txt",
        };
        MergeResult result = Merger.Merge(
            B("This file has\r\nCRLF line endings.\r\n"),
            B("This file\r\ndoes, too.\r\n"),
            B("And so does\r\nthis one.\r\n"),
            opts);

        Assert.Equal(1, result.ConflictCount);
        string expected = "<<<<<<< file.txt\r\nThis file\r\ndoes, too.\r\n||||||| file.txt\r\nThis file has\r\nCRLF line endings.\r\n=======\r\nAnd so does\r\nthis one.\r\n>>>>>>> file.txt\r\n";
        Assert.Equal(expected, Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void ConflictsInZdiff3_MatchesUpstream()
    {
        var opts = new MergeOptions
        {
            Style = MergeStyle.ZealousDiff3,
            OurLabel = "file.txt",
            TheirLabel = "file.txt",
            AncestorLabel = "file.txt",
        };
        MergeResult result = Merger.Merge(
            B("1,\n# add more here\n3,\n"),
            B("1,\nfoo,\nbar,\nbaz,\n3,\n"),
            B("1,\nfoo,\nbar,\nquux,\nwoot,\nbaz,\n3,\n"),
            opts);

        Assert.Equal(1, result.ConflictCount);
        string expected = "1,\nfoo,\nbar,\n<<<<<<< file.txt\n||||||| file.txt\n# add more here\n=======\nquux,\nwoot,\n>>>>>>> file.txt\nbaz,\n3,\n";
        Assert.Equal(expected, Encoding.UTF8.GetString(result.Content));
    }
}
