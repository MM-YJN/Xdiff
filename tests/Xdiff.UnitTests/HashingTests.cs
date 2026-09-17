using Xdiff.Util;

namespace Xdiff.UnitTests;

public class HashingTests
{
    [Fact]
    public void HashRecord_OfEmptyBuffer_ReturnsSeed()
    {
        byte[] buffer = Array.Empty<byte>();
        int offset = 0;

        ulong hash = Hashing.HashRecord(buffer, ref offset, buffer.Length, WhitespaceMode.None);

        Assert.Equal(Hashing.Seed, hash);
        Assert.Equal(0, offset);
    }

    [Fact]
    public void HashRecord_OfSingleByte_ReturnsKnownDjb2aValue()
    {
        byte[] buffer = "a\n"u8.ToArray();
        int offset = 0;

        ulong hash = Hashing.HashRecord(buffer, ref offset, buffer.Length, WhitespaceMode.None);

        Assert.Equal(177604UL, hash);
        Assert.Equal(2, offset);
    }

    [Fact]
    public void HashRecord_OfBareByteWithoutNewline_AdvancesToEnd()
    {
        byte[] buffer = "abc"u8.ToArray();
        int offset = 0;

        ulong hash = Hashing.HashRecord(buffer, ref offset, buffer.Length, WhitespaceMode.None);

        Assert.NotEqual(Hashing.Seed, hash);
        Assert.Equal(3, offset);
    }

    [Fact]
    public void HashRecord_OfTwoLines_HashesIndependently()
    {
        byte[] buffer = "a\nb\n"u8.ToArray();
        int offset = 0;

        ulong hash1 = Hashing.HashRecord(buffer, ref offset, buffer.Length, WhitespaceMode.None);
        int midOffset = offset;
        ulong hash2 = Hashing.HashRecord(buffer, ref offset, buffer.Length, WhitespaceMode.None);

        Assert.Equal(2, midOffset);
        Assert.Equal(4, offset);
        Assert.NotEqual(hash1, hash2);
        Assert.Equal(177604UL, hash1);
    }

    [Fact]
    public void HashRecord_StopsAtEmbeddedNewline()
    {
        byte[] buffer = "ab\ncd\n"u8.ToArray();
        int offset = 0;

        ulong hash = Hashing.HashRecord(buffer, ref offset, buffer.Length, WhitespaceMode.None);

        Assert.Equal(3, offset);
        ulong expected = ComputeDjb2aPlain("ab"u8);
        Assert.Equal(expected, hash);
    }

    [Theory]
    [InlineData(WhitespaceMode.IgnoreAll)]
    [InlineData(WhitespaceMode.IgnoreChanges)]
    [InlineData(WhitespaceMode.IgnoreAtEol)]
    [InlineData(WhitespaceMode.IgnoreCrAtEol)]
    public void HashRecord_WhitespaceModes_StaysConsistentWithRecordMatch(WhitespaceMode mode)
    {
        IEnumerable<(byte[] Left, byte[] Right)> pairs = WhitespaceEquivalentPairs(mode);
        foreach ((byte[]? left, byte[]? right) in pairs)
        {
            ulong leftHash = HashOnce(left, mode);
            ulong rightHash = HashOnce(right, mode);

            Assert.True(
                RecordMatch.RecordsEqual(left, right, mode),
                $"records should match under {mode}: {Show(left)} vs {Show(right)}");
            Assert.True(
                leftHash == rightHash,
                $"hashes should match under {mode}: {Show(left)} ({leftHash}) vs {Show(right)} ({rightHash})");
        }
    }

    [Fact]
    public void HashRecord_IgnoreAll_StripsAllWhitespace()
    {
        ulong withoutWs = HashOnce("ab"u8, WhitespaceMode.IgnoreAll);
        ulong withWs = HashOnce("a b"u8, WhitespaceMode.IgnoreAll);

        Assert.Equal(withoutWs, withWs);
    }

    [Fact]
    public void HashRecord_IgnoreChanges_CollapsesWhitespaceRuns()
    {
        ulong single = HashOnce("a b"u8, WhitespaceMode.IgnoreChanges);
        ulong doubled = HashOnce("a  b"u8, WhitespaceMode.IgnoreChanges);

        Assert.Equal(single, doubled);
    }

    [Fact]
    public void HashRecord_IgnoreCrAtEol_KeepsIncompleteLineCr()
    {
        ulong withCr = HashOnce("ab\r"u8, WhitespaceMode.IgnoreCrAtEol);
        ulong withoutCr = HashOnce("ab"u8, WhitespaceMode.IgnoreCrAtEol);

        Assert.NotEqual(withCr, withoutCr);
    }

    [Fact]
    public void HashRecord_IgnoreAtEol_KeepsInterWordWhitespace()
    {
        // IgnoreAtEol drops only trailing whitespace; inter-word whitespace runs
        // are folded byte-for-byte into the hash (the !atEol branch).
        ulong single = HashOnce("a b"u8, WhitespaceMode.IgnoreAtEol);
        ulong doubled = HashOnce("a  b"u8, WhitespaceMode.IgnoreAtEol);
        ulong bare = HashOnce("ab"u8, WhitespaceMode.IgnoreAtEol);

        Assert.NotEqual(single, doubled);
        Assert.NotEqual(single, bare);

        // Trailing whitespace is still ignored at end-of-line.
        Assert.Equal(bare, HashOnce("ab "u8, WhitespaceMode.IgnoreAtEol));
    }

    private static ulong HashOnce(ReadOnlySpan<byte> line, WhitespaceMode mode)
    {
        int offset = 0;
        return Hashing.HashRecord(line, ref offset, line.Length, mode);
    }

    private static ulong ComputeDjb2aPlain(ReadOnlySpan<byte> bytes)
    {
        ulong ha = Hashing.Seed;
        foreach (byte b in bytes)
        {
            ha += ha << 5;
            ha ^= b;
        }

        return ha;
    }

    private static IEnumerable<(byte[] Left, byte[] Right)> WhitespaceEquivalentPairs(WhitespaceMode mode)
    {
        if (mode == WhitespaceMode.IgnoreAll)
        {
            yield return ("ab"u8.ToArray(), "a b"u8.ToArray());
            yield return ("ab"u8.ToArray(), "a\tb"u8.ToArray());
            yield return ("ab"u8.ToArray(), "  a b  "u8.ToArray());
            yield return ("a b"u8.ToArray(), "a  b"u8.ToArray());
        }
        else if (mode == WhitespaceMode.IgnoreChanges)
        {
            yield return ("a b"u8.ToArray(), "a  b"u8.ToArray());
            yield return ("a b"u8.ToArray(), "a\t b"u8.ToArray());
        }
        else if (mode == WhitespaceMode.IgnoreAtEol)
        {
            yield return ("ab"u8.ToArray(), "ab "u8.ToArray());
            yield return ("ab"u8.ToArray(), "ab\t "u8.ToArray());
            yield return ("ab "u8.ToArray(), "ab\t"u8.ToArray());
        }
        else if (mode == WhitespaceMode.IgnoreCrAtEol)
        {
            yield return ("ab\n"u8.ToArray(), "ab\r\n"u8.ToArray());
            yield return ("abc\n"u8.ToArray(), "abc\r\n"u8.ToArray());
        }
    }

    private static string Show(ReadOnlySpan<byte> bytes) => System.Text.Encoding.UTF8.GetString(bytes);
}
