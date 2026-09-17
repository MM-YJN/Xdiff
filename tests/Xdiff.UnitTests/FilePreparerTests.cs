using Xdiff.Core;
using Xdiff.Prepare;

namespace Xdiff.UnitTests;

public class FilePreparerTests
{
    [Fact]
    public void PrepareEnv_LineSplitting_NoTrailingNewline_StillCountsLastLine()
    {
        var env = new XdfEnv();

        FilePreparer.PrepareEnv(
            "a\nb\nc"u8.ToArray(), "x\ny\nz"u8.ToArray(),
            WhitespaceMode.None, DiffAlgorithm.Myers, env);

        Assert.Equal(3, env.Xdf1.Nrec);
        Assert.Equal(3, env.Xdf2.Nrec);
    }

    [Fact]
    public void PrepareEnv_LineSplitting_TrailingNewline_DoesNotCreateExtraRecord()
    {
        var env = new XdfEnv();

        FilePreparer.PrepareEnv(
            "a\nb\n"u8.ToArray(), "x\ny\n"u8.ToArray(),
            WhitespaceMode.None, DiffAlgorithm.Myers, env);

        Assert.Equal(2, env.Xdf1.Nrec);
        Assert.Equal(2, env.Xdf2.Nrec);
    }

    [Fact]
    public void PrepareEnv_EmptyFile_ProducesZeroRecords()
    {
        var env = new XdfEnv();

        FilePreparer.PrepareEnv(
            Array.Empty<byte>(), "a\n"u8.ToArray(),
            WhitespaceMode.None, DiffAlgorithm.Myers, env);

        Assert.Equal(0, env.Xdf1.Nrec);
        Assert.Equal(1, env.Xdf2.Nrec);
    }

    [Fact]
    public void PrepareEnv_Rchg_LengthIsNrecPlusTwo_AllFalse()
    {
        var env = new XdfEnv();

        FilePreparer.PrepareEnv(
            "a\nb\nc\n"u8.ToArray(), "x\ny\nz\n"u8.ToArray(),
            WhitespaceMode.None, DiffAlgorithm.Patience, env);

        Assert.Equal(5, env.Xdf1.Rchg.Length);
        Assert.All(env.Xdf1.Rchg, Assert.False);
    }

    [Fact]
    public void PrepareEnv_IdenticalFiles_Myers_TrimsAllRecords()
    {
        var env = new XdfEnv();
        byte[] data = "a\nb\nc\n"u8.ToArray();

        FilePreparer.PrepareEnv(
            data, data, WhitespaceMode.None, DiffAlgorithm.Myers, env);

        Assert.True(env.Xdf1.Dstart > env.Xdf1.Dend);
        Assert.True(env.Xdf2.Dstart > env.Xdf2.Dend);
        Assert.Equal(0, env.Xdf1.Nreff);
        Assert.Equal(0, env.Xdf2.Nreff);
    }

    [Fact]
    public void PrepareEnv_IdenticalFiles_SameHaArray()
    {
        var env = new XdfEnv();
        byte[] data = "a\nb\nc\n"u8.ToArray();

        FilePreparer.PrepareEnv(
            data, data, WhitespaceMode.None, DiffAlgorithm.Patience, env);

        Assert.Equal(env.Xdf1.Ha, env.Xdf2.Ha);
    }

    [Fact]
    public void PrepareEnv_CommonPrefixSuffix_Myers_TrimsCorrectly()
    {
        var env = new XdfEnv();

        FilePreparer.PrepareEnv(
            "a\nb\nc\n"u8.ToArray(), "a\nx\nc\n"u8.ToArray(),
            WhitespaceMode.None, DiffAlgorithm.Myers, env);

        Assert.Equal(1, env.Xdf1.Dstart);
        Assert.Equal(1, env.Xdf1.Dend);
    }

    [Fact]
    public void PrepareEnv_DifferentFiles_Myers_MarksUnmatchedAsChanged()
    {
        var env = new XdfEnv();

        FilePreparer.PrepareEnv(
            "a\nb\n"u8.ToArray(), "x\ny\n"u8.ToArray(),
            WhitespaceMode.None, DiffAlgorithm.Myers, env);

        Assert.True(env.Xdf1.Rchg[1]);
        Assert.True(env.Xdf1.Rchg[2]);
        Assert.True(env.Xdf2.Rchg[1]);
        Assert.True(env.Xdf2.Rchg[2]);
        Assert.Equal(0, env.Xdf1.Nreff);
        Assert.Equal(0, env.Xdf2.Nreff);
    }

    [Fact]
    public void PrepareEnv_Myers_BuildsRindexWithoutCompactingHa()
    {
        var env = new XdfEnv();

        FilePreparer.PrepareEnv(
            "x\na\nb\ny\n"u8.ToArray(), "z\na\nb\nw\n"u8.ToArray(),
            WhitespaceMode.None, DiffAlgorithm.Myers, env);

        Assert.Equal(2, env.Xdf1.Nreff);
        Assert.Equal(2, env.Xdf2.Nreff);

        Assert.NotNull(env.Xdf1.Rindex);
        Assert.Equal(1, env.Xdf1.Rindex[0]);
        Assert.Equal(2, env.Xdf1.Rindex[1]);

        Assert.True(env.Xdf1.Rchg[1]);
        Assert.False(env.Xdf1.Rchg[2]);
        Assert.False(env.Xdf1.Rchg[3]);
        Assert.True(env.Xdf1.Rchg[4]);

        Assert.Equal(0, env.Xdf1.Ha[0]);
        Assert.Equal(1, env.Xdf1.Ha[1]);
        Assert.Equal(2, env.Xdf1.Ha[2]);
        Assert.Equal(3, env.Xdf1.Ha[3]);
    }

    [Fact]
    public void PrepareEnv_Patience_SkipsOptimization()
    {
        var env = new XdfEnv();

        FilePreparer.PrepareEnv(
            "a\nb\nc\n"u8.ToArray(), "a\nx\nc\n"u8.ToArray(),
            WhitespaceMode.None, DiffAlgorithm.Patience, env);

        Assert.Null(env.Xdf1.Rindex);
        Assert.Equal(0, env.Xdf1.Nreff);
        Assert.Equal(0, env.Xdf1.Dstart);
        Assert.Equal(2, env.Xdf1.Dend);
    }

    [Fact]
    public void PrepareEnv_Histogram_SkipsOptimization()
    {
        var env = new XdfEnv();

        FilePreparer.PrepareEnv(
            "a\nb\nc\n"u8.ToArray(), "a\nx\nc\n"u8.ToArray(),
            WhitespaceMode.None, DiffAlgorithm.Histogram, env);

        Assert.Null(env.Xdf1.Rindex);
        Assert.Equal(0, env.Xdf1.Nreff);
    }

    [Fact]
    public void PrepareEnv_Records_CarryCorrectOffsetAndLength()
    {
        var env = new XdfEnv();

        FilePreparer.PrepareEnv(
            "ab\ncde\n"u8.ToArray(), "x\n"u8.ToArray(),
            WhitespaceMode.None, DiffAlgorithm.Patience, env);

        Assert.Equal(0, env.Xdf1.Recs[0].Offset);
        Assert.Equal(3, env.Xdf1.Recs[0].Length);
        Assert.Equal(3, env.Xdf1.Recs[1].Offset);
        Assert.Equal(4, env.Xdf1.Recs[1].Length);
    }
}
