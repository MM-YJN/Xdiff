using Xdiff.Prepare;
using Xdiff.Util;

namespace Xdiff.UnitTests;

public class XdClassifierTests
{
    [Fact]
    public void ClassifyRecord_SameLineSameHash_ReturnsSameClass()
    {
        var cf = new XdClassifier(WhitespaceMode.None, 16);
        byte[] line = "hello\n"u8.ToArray();
        ulong rawHash = HashLine(line, WhitespaceMode.None);

        XdClass cls1 = cf.ClassifyRecord(1, line, rawHash);
        XdClass cls2 = cf.ClassifyRecord(1, line, rawHash);

        Assert.Equal(cls1.Idx, cls2.Idx);
        Assert.Equal(0, cls1.Idx);
    }

    [Fact]
    public void ClassifyRecord_DifferentLines_CreatesSeparateClasses()
    {
        var cf = new XdClassifier(WhitespaceMode.None, 16);
        byte[] lineA = "hello\n"u8.ToArray();
        byte[] lineB = "world\n"u8.ToArray();
        ulong hashA = HashLine(lineA, WhitespaceMode.None);
        ulong hashB = HashLine(lineB, WhitespaceMode.None);

        XdClass clsA = cf.ClassifyRecord(1, lineA, hashA);
        XdClass clsB = cf.ClassifyRecord(1, lineB, hashB);

        Assert.NotEqual(clsA.Idx, clsB.Idx);
        Assert.Equal(2, cf.Count);
    }

    [Fact]
    public void ClassifyRecord_IgnoreAll_GroupsWhitespaceVariants()
    {
        var cf = new XdClassifier(WhitespaceMode.IgnoreAll, 16);
        byte[] spaced = "a b\n"u8.ToArray();
        byte[] tight = "ab\n"u8.ToArray();
        ulong hashSpaced = HashLine(spaced, WhitespaceMode.IgnoreAll);
        ulong hashTight = HashLine(tight, WhitespaceMode.IgnoreAll);

        XdClass clsSpaced = cf.ClassifyRecord(1, spaced, hashSpaced);
        XdClass clsTight = cf.ClassifyRecord(1, tight, hashTight);

        Assert.Equal(clsSpaced.Idx, clsTight.Idx);
    }

    [Fact]
    public void ClassifyRecord_AssignmentOrder_File1FirstThenFile2New()
    {
        var cf = new XdClassifier(WhitespaceMode.None, 16);
        byte[] a = "a\n"u8.ToArray();
        byte[] b = "b\n"u8.ToArray();
        byte[] c = "c\n"u8.ToArray();
        ulong hashA = HashLine(a, WhitespaceMode.None);
        ulong hashB = HashLine(b, WhitespaceMode.None);
        ulong hashC = HashLine(c, WhitespaceMode.None);

        XdClass clsA = cf.ClassifyRecord(1, a, hashA);
        XdClass clsB = cf.ClassifyRecord(1, b, hashB);
        XdClass clsC = cf.ClassifyRecord(2, c, hashC);
        XdClass clsA2 = cf.ClassifyRecord(2, a, hashA);

        Assert.Equal(0, clsA.Idx);
        Assert.Equal(1, clsB.Idx);
        Assert.Equal(2, clsC.Idx);
        Assert.Equal(0, clsA2.Idx);
        Assert.Equal(clsA.Idx, clsA2.Idx);
    }

    [Fact]
    public void ClassifyRecord_LenCounts_TrackPerFileOccurrences()
    {
        var cf = new XdClassifier(WhitespaceMode.None, 16);
        byte[] line = "x\n"u8.ToArray();
        ulong hash = HashLine(line, WhitespaceMode.None);

        XdClass cls = cf.ClassifyRecord(1, line, hash);
        cf.ClassifyRecord(1, line, hash);
        cf.ClassifyRecord(1, line, hash);
        cf.ClassifyRecord(2, line, hash);

        // XdClass is a struct: the first return value is a copy, so re-fetch
        // the record slot to observe the accumulated counts.
        Assert.Equal(3, cf.GetClass(cls.Idx).Len1);
        Assert.Equal(1, cf.GetClass(cls.Idx).Len2);
    }

    [Fact]
    public void ClassifyRecord_HashCollision_DifferentLinesSameHash_CreatesSeparateClasses()
    {
        var cf = new XdClassifier(WhitespaceMode.None, 16);
        byte[] lineA = "a\n"u8.ToArray();
        byte[] lineB = "b\n"u8.ToArray();

        XdClass clsA = cf.ClassifyRecord(1, lineA, 42UL);
        XdClass clsB = cf.ClassifyRecord(1, lineB, 42UL);

        Assert.NotEqual(clsA.Idx, clsB.Idx);
        Assert.Equal(2, cf.Count);
    }

    [Fact]
    public void GetClass_ReturnsByIndex()
    {
        var cf = new XdClassifier(WhitespaceMode.None, 16);
        byte[] a = "a\n"u8.ToArray();
        byte[] b = "b\n"u8.ToArray();

        XdClass clsA = cf.ClassifyRecord(1, a, HashLine(a, WhitespaceMode.None));
        cf.ClassifyRecord(1, b, HashLine(b, WhitespaceMode.None));

        Assert.Equal(clsA.Idx, cf.GetClass(0).Idx);
    }

    private static ulong HashLine(byte[] line, WhitespaceMode flags)
    {
        int offset = 0;
        return Hashing.HashRecord(line, ref offset, line.Length, flags);
    }
}
