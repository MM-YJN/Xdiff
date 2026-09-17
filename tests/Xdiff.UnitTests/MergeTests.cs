using System.Text;

namespace Xdiff.UnitTests;

public class MergeTests
{
    [Fact]
    public void Merge_NoOp_BothSidesIdenticalToAncestor_ReturnsAncestor()
    {
        byte[] data = "a\nb\nc\n"u8.ToArray();
        MergeResult result = Merger.Merge(data, data, data);

        Assert.Equal(0, result.ConflictCount);
        Assert.False(result.HasConflicts);
        Assert.Equal("a\nb\nc\n", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Merge_OursOnlyChanged_ReturnsOurs()
    {
        MergeResult result = Merger.Merge(
            "a\nb\nc\n"u8.ToArray(),
            "a\nX\nc\n"u8.ToArray(),
            "a\nb\nc\n"u8.ToArray());

        Assert.Equal(0, result.ConflictCount);
        Assert.Equal("a\nX\nc\n", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Merge_TheirsOnlyChanged_ReturnsTheirs()
    {
        MergeResult result = Merger.Merge(
            "a\nb\nc\n"u8.ToArray(),
            "a\nb\nc\n"u8.ToArray(),
            "a\nY\nc\n"u8.ToArray());

        Assert.Equal(0, result.ConflictCount);
        Assert.Equal("a\nY\nc\n", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Merge_NonOverlappingChanges_CleanMerge()
    {
        MergeResult result = Merger.Merge(
            "a\nb\nc\nd\ne\n"u8.ToArray(),
            "a\nX\nc\nd\ne\n"u8.ToArray(),
            "a\nb\nc\nY\ne\n"u8.ToArray());

        Assert.Equal(0, result.ConflictCount);
        Assert.Equal("a\nX\nc\nY\ne\n", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Merge_OverlappingConflict_ProducesConflictMarkers()
    {
        MergeResult result = Merger.Merge(
            "a\nb\nc\n"u8.ToArray(),
            "a\nX\nc\n"u8.ToArray(),
            "a\nY\nc\n"u8.ToArray());

        Assert.Equal(1, result.ConflictCount);
        Assert.True(result.HasConflicts);
        string text = Encoding.UTF8.GetString(result.Content);
        Assert.Equal("a\n<<<<<<<\nX\n=======\nY\n>>>>>>>\nc\n", text);
    }

    [Fact]
    public void Merge_BothSidesMakeSameChange_NoConflict()
    {
        MergeResult result = Merger.Merge(
            "a\nb\nc\n"u8.ToArray(),
            "a\nX\nc\n"u8.ToArray(),
            "a\nX\nc\n"u8.ToArray());

        Assert.Equal(0, result.ConflictCount);
        Assert.Equal("a\nX\nc\n", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Merge_FavorOurs_AutoResolvesConflict()
    {
        var opts = new MergeOptions { Favor = MergeFavor.Ours };
        MergeResult result = Merger.Merge(
            "a\nb\nc\n"u8.ToArray(),
            "a\nX\nc\n"u8.ToArray(),
            "a\nY\nc\n"u8.ToArray(),
            opts);

        Assert.Equal(0, result.ConflictCount);
        Assert.Equal("a\nX\nc\n", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Merge_FavorTheirs_AutoResolvesConflict()
    {
        var opts = new MergeOptions { Favor = MergeFavor.Theirs };
        MergeResult result = Merger.Merge(
            "a\nb\nc\n"u8.ToArray(),
            "a\nX\nc\n"u8.ToArray(),
            "a\nY\nc\n"u8.ToArray(),
            opts);

        Assert.Equal(0, result.ConflictCount);
        Assert.Equal("a\nY\nc\n", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Merge_FavorUnion_ConcatenatesBothSides()
    {
        var opts = new MergeOptions { Favor = MergeFavor.Union };
        MergeResult result = Merger.Merge(
            "a\nb\nc\n"u8.ToArray(),
            "a\nX\nc\n"u8.ToArray(),
            "a\nY\nc\n"u8.ToArray(),
            opts);

        Assert.Equal(0, result.ConflictCount);
        Assert.Equal("a\nX\nY\nc\n", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Merge_Diff3Style_ShowsAncestorInConflict()
    {
        var opts = new MergeOptions { Style = MergeStyle.Diff3 };
        MergeResult result = Merger.Merge(
            "a\nb\nc\n"u8.ToArray(),
            "a\nX\nc\n"u8.ToArray(),
            "a\nY\nc\n"u8.ToArray(),
            opts);

        Assert.Equal(1, result.ConflictCount);
        string text = Encoding.UTF8.GetString(result.Content);
        Assert.Equal("a\n<<<<<<<\nX\n|||||||\nb\n=======\nY\n>>>>>>>\nc\n", text);
    }

    [Fact]
    public void Merge_Labels_AppearInConflictMarkers()
    {
        var opts = new MergeOptions
        {
            OurLabel = "ours",
            TheirLabel = "theirs",
            AncestorLabel = "base"
        };
        MergeResult result = Merger.Merge(
            "a\nb\nc\n"u8.ToArray(),
            "a\nX\nc\n"u8.ToArray(),
            "a\nY\nc\n"u8.ToArray(),
            opts);

        string text = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("<<<<<<< ours\n", text);
        Assert.Contains(">>>>>>> theirs\n", text);
    }

    [Fact]
    public void Merge_Diff3StyleWithLabels_ShowsAncestorLabel()
    {
        var opts = new MergeOptions
        {
            Style = MergeStyle.Diff3,
            OurLabel = "ours",
            TheirLabel = "theirs",
            AncestorLabel = "base"
        };
        MergeResult result = Merger.Merge(
            "a\nb\nc\n"u8.ToArray(),
            "a\nX\nc\n"u8.ToArray(),
            "a\nY\nc\n"u8.ToArray(),
            opts);

        string text = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("||||||| base\n", text);
    }

    [Fact]
    public void Merge_EmptyAncestor_BothSidesAddDifferentContent_Conflict()
    {
        MergeResult result = Merger.Merge(
            (byte[])[],
            "a\n"u8.ToArray(),
            "b\n"u8.ToArray());

        Assert.Equal(1, result.ConflictCount);
        string text = Encoding.UTF8.GetString(result.Content);
        Assert.Equal("<<<<<<<\na\n=======\nb\n>>>>>>>\n", text);
    }

    [Fact]
    public void Merge_EmptyAncestor_BothSidesAddSameContent_NoConflict()
    {
        MergeResult result = Merger.Merge(
            (byte[])[],
            "a\n"u8.ToArray(),
            "a\n"u8.ToArray());

        Assert.Equal(0, result.ConflictCount);
        Assert.Equal("a\n", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Merge_StringOverload_ReturnsMergedString()
    {
        string merged = Merger.Merge("a\nb\nc\n", "a\nX\nc\n", "a\nb\nc\n");

        Assert.Equal("a\nX\nc\n", merged);
    }

    [Fact]
    public void Merge_StringOverload_ConflictReturnsMarkers()
    {
        string merged = Merger.Merge("a\nb\nc\n", "a\nX\nc\n", "a\nY\nc\n");

        Assert.Equal("a\n<<<<<<<\nX\n=======\nY\n>>>>>>>\nc\n", merged);
    }

    [Fact]
    public void Merge_CustomMarkerSize_ProducesWiderMarkers()
    {
        var opts = new MergeOptions { MarkerSize = 9 };
        MergeResult result = Merger.Merge(
            "a\nb\nc\n"u8.ToArray(),
            "a\nX\nc\n"u8.ToArray(),
            "a\nY\nc\n"u8.ToArray(),
            opts);

        string text = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("<<<<<<<<<\n", text);
        Assert.Contains(">>>>>>>>>\n", text);
    }

    [Fact]
    public void Merge_NoNewlineAtEof_HandledInConflict()
    {
        MergeResult result = Merger.Merge(
            "a\nb"u8.ToArray(),
            "a\nX"u8.ToArray(),
            "a\nY"u8.ToArray());

        Assert.Equal(1, result.ConflictCount);
        string text = Encoding.UTF8.GetString(result.Content);
        Assert.Equal("a\n<<<<<<<\nX\n=======\nY\n>>>>>>>\n", text);
    }

    [Fact]
    public void Merge_LevelMinimal_IdenticalChangesStillConflict()
    {
        var opts = new MergeOptions { Level = MergeLevel.Minimal };
        MergeResult result = Merger.Merge(
            "a\nb\nc\n"u8.ToArray(),
            "a\nX\nc\n"u8.ToArray(),
            "a\nX\nc\n"u8.ToArray(),
            opts);

        Assert.Equal(1, result.ConflictCount);
    }

    [Fact]
    public void Merge_TwoConflicts_BothReported()
    {
        var opts = new MergeOptions { Level = MergeLevel.Eager };
        MergeResult result = Merger.Merge(
            "a\nb\nc\nd\ne\n"u8.ToArray(),
            "a\nX\nc\nY\ne\n"u8.ToArray(),
            "a\nZ\nc\nW\ne\n"u8.ToArray(),
            opts);

        Assert.Equal(2, result.ConflictCount);
    }

    [Fact]
    public void Merge_Zealous_SimplifiesAdjacentConflicts()
    {
        MergeResult result = Merger.Merge(
            "a\nb\nc\nd\ne\n"u8.ToArray(),
            "a\nX\nc\nY\ne\n"u8.ToArray(),
            "a\nZ\nc\nW\ne\n"u8.ToArray());

        Assert.Equal(1, result.ConflictCount);
    }

    [Fact]
    public void Merge_SpanOverload_ReturnsByteArray()
    {
        var ancestor = (ReadOnlySpan<byte>)"a\nb\nc\n"u8;
        var ours = (ReadOnlySpan<byte>)"a\nX\nc\n"u8;
        var theirs = (ReadOnlySpan<byte>)"a\nY\nc\n"u8;

        MergeResult result = Merger.Merge(ancestor, ours, theirs);

        Assert.Equal(1, result.ConflictCount);
        Assert.IsType<byte[]>(result.Content);
    }

    [Fact]
    public void Merge_TheirsChangeBeforeOurs_CleanMerge()
    {
        // Theirs-only change precedes the ours-only change in ancestor coords,
        // driving ThreeWayMerger's xscr2-before-xscr1 branch (mode 2 first).
        MergeResult result = Merger.Merge(
            "a\nb\nc\nd\ne\n"u8.ToArray(),
            "a\nb\nc\nX\ne\n"u8.ToArray(),
            "a\nY\nc\nd\ne\n"u8.ToArray());

        Assert.Equal(0, result.ConflictCount);
        Assert.Equal("a\nY\nc\nX\ne\n", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public void Merge_Conflict_OursStartsLater_OffPositive()
    {
        // Overlapping conflict where ours' change starts after theirs'
        // (off = xscr1.I1 - xscr2.I1 > 0), exercising the i0/i1 back-extension.
        MergeResult result = Merger.Merge(
            "a\nb\nc\nd\n"u8.ToArray(),
            "a\nb\nC\nd\n"u8.ToArray(),
            "a\nB\nc\nd\n"u8.ToArray());

        Assert.Equal(1, result.ConflictCount);
        Assert.True(result.HasConflicts);
    }

    [Fact]
    public void Merge_Conflict_TheirsExtendsFurther_FfoNegative()
    {
        // Overlapping conflict where theirs changes more ancestor lines than ours
        // (ffo = off + xscr1.Chg1 - xscr2.Chg1 < 0), exercising chg0/chg1 growth.
        MergeResult result = Merger.Merge(
            "a\nb\nc\nd\n"u8.ToArray(),
            "a\nX\nc\nd\n"u8.ToArray(),
            "a\nP\nQ\nd\n"u8.ToArray());

        Assert.Equal(1, result.ConflictCount);
    }

    [Fact]
    public void Merge_MarkerSizeZero_FallsBackToDefault()
    {
        // MarkerSize <= 0 must fall back to the default (7), not crash.
        var opts = new MergeOptions { MarkerSize = 0 };
        MergeResult result = Merger.Merge(
            "a\nb\nc\n"u8.ToArray(),
            "a\nX\nc\n"u8.ToArray(),
            "a\nY\nc\n"u8.ToArray(),
            opts);

        Assert.Equal(1, result.ConflictCount);
        string text = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("<<<<<<<\n", text);
        Assert.Contains(">>>>>>>\n", text);
    }

    [Fact]
    public void Merge_CrlfConflict_NoNewlineAtEof_AddsCrBeforeNl()
    {
        // CRLF content with a no-newline conflict line forces CopyRecs to emit
        // a \r before the synthesized \n (needsCr path).
        MergeResult result = Merger.Merge(
            "a\r\nb"u8.ToArray(),
            "a\r\nX"u8.ToArray(),
            "a\r\nY"u8.ToArray());

        Assert.Equal(1, result.ConflictCount);
        string text = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("<<<<<<<\r\n", text);
        Assert.Contains("=======\r\n", text);
    }

    [Fact]
    public void Merge_EmptyAncestorCrlf_BothSidesAddDifferent_Conflict()
    {
        // Empty ancestor exercises IsEolCrlf's empty-file branch (Nrec == 0).
        MergeResult result = Merger.Merge(
            (byte[])[],
            "a\r\n"u8.ToArray(),
            "b\r\n"u8.ToArray());

        Assert.Equal(1, result.ConflictCount);
    }

    [Fact]
    public void Merge_NoNewlineSingleLine_IsEolCrlfReturnsNegative()
    {
        // Single-line buffers with no newline anywhere exercise IsEolCrlf's
        // i == 0 / no-newline-last-record branch (returns -1).
        MergeResult result = Merger.Merge(
            "b"u8.ToArray(),
            "X"u8.ToArray(),
            "Y"u8.ToArray());

        Assert.Equal(1, result.ConflictCount);
        string text = Encoding.UTF8.GetString(result.Content);
        Assert.Equal("<<<<<<<\nX\n=======\nY\n>>>>>>>\n", text);
    }
}
