using System.Text;

namespace Xdiff.UnitTests;

public class ConflictRefinerMergeTests
{
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void ZealousAlnum_MergesAdjacentConflicts_WhenGapHasNoAlnum()
    {
        MergeResult result = Merger.Merge(
            B("a\n---\n===\n+++\n!!!\n$$$\nbbb\nend\n"),
            B("a\nXXX\n===\n+++\n!!!\n$$$\nYYY\nend\n"),
            B("a\nZZZ\n===\n+++\n!!!\n$$$\nWWW\nend\n"),
            new MergeOptions { Level = MergeLevel.ZealousAlnum });

        Assert.Equal(1, result.ConflictCount);
    }

    [Fact]
    public void ZealousAlnum_KeepsConflictsSeparate_WhenGapHasAlnum()
    {
        MergeResult result = Merger.Merge(
            B("a\n---\nc\nd\ne\nf\nbbb\nend\n"),
            B("a\nXXX\nc\nd\ne\nf\nYYY\nend\n"),
            B("a\nZZZ\nc\nd\ne\nf\nWWW\nend\n"),
            new MergeOptions { Level = MergeLevel.ZealousAlnum });

        Assert.Equal(2, result.ConflictCount);
    }

    [Fact]
    public void Zealous_MergesAdjacentConflicts_WhenGapHasAlnum()
    {
        MergeResult result = Merger.Merge(
            B("a\n---\nc\nd\nbbb\nend\n"),
            B("a\nXXX\nc\nd\nYYY\nend\n"),
            B("a\nZZZ\nc\nd\nWWW\nend\n"),
            new MergeOptions { Level = MergeLevel.Zealous });

        Assert.Equal(1, result.ConflictCount);
    }

    [Fact]
    public void Zealous_KeepsConflictsSeparate_WhenGapIsMoreThan3()
    {
        MergeResult result = Merger.Merge(
            B("a\n---\nc\nd\ne\nf\nbbb\nend\n"),
            B("a\nXXX\nc\nd\ne\nf\nYYY\nend\n"),
            B("a\nZZZ\nc\nd\ne\nf\nWWW\nend\n"),
            new MergeOptions { Level = MergeLevel.Zealous });

        // Zealous: gap(4) > 3 → skip merge → 2 separate conflicts
        Assert.Equal(2, result.ConflictCount);
    }

    [Fact]
    public void ZealousDiff3_SkipsNonConflictEntries()
    {
        var opts = new MergeOptions
        {
            Style = MergeStyle.ZealousDiff3,
            OurLabel = "ours",
            TheirLabel = "theirs",
            AncestorLabel = "base",
        };
        MergeResult result = Merger.Merge(
            B("a\nb\nc\nd\ne\nf\ng\nh\n"),
            B("a\nX\nc\nd\ne\nf\nY\nh\n"),
            B("a\nZ\nc\nd\ne\nf\ng\nh\n"),
            opts);

        Assert.Equal(1, result.ConflictCount);
        Assert.Contains("<<<<<<< ours", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Zealous_RefinesConflictWithMultiHunkSubDiff()
    {
        // Both sides replace a single line with 6 lines containing a block of
        // 4 common lines in the middle. Sub-diff produces 2 hunks (X↔Z and
        // Y↔W) separated by the 4-line common block. Gap > 3 prevents
        // SimplifyNonConflicts from re-merging them.
        MergeResult result = Merger.Merge(
            B("a\nb\nc\n"),
            B("a\nX\ncommon1\ncommon2\ncommon3\ncommon4\nY\nc\n"),
            B("a\nZ\ncommon1\ncommon2\ncommon3\ncommon4\nW\nc\n"),
            new MergeOptions { Level = MergeLevel.Zealous });

        Assert.Equal(2, result.ConflictCount);
        string text = Encoding.UTF8.GetString(result.Content);
        // Common lines should be outside conflict markers
        Assert.Contains("common1\ncommon2\ncommon3\ncommon4\n", text);
    }

    [Fact]
    public void ZealousDiff3_TrimsMatchingLinesAtConflictEdges()
    {
        var opts = new MergeOptions
        {
            Style = MergeStyle.ZealousDiff3,
            OurLabel = "f.txt",
            TheirLabel = "f.txt",
            AncestorLabel = "f.txt",
        };
        MergeResult result = Merger.Merge(
            B("1,\n# add more here\n3,\n4,\n"),
            B("1,\nfoo,\nbar,\nbaz,\n3,\n4,\n"),
            B("1,\nfoo,\nbar,\nquux,\nwoot,\nbaz,\n3,\n4,\n"),
            opts);

        Assert.Equal(1, result.ConflictCount);
        string text = Encoding.UTF8.GetString(result.Content);
        Assert.DoesNotContain("<<<<<<<\n1,", text);
        Assert.DoesNotContain("3,\n>>>>>>>", text);
    }
}
