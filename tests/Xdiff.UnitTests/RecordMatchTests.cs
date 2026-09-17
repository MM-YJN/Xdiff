using Xdiff.Util;

namespace Xdiff.UnitTests;

public class RecordMatchTests
{
    [Fact]
    public void IsBlankLine_NoFlags_ReturnsTrueForEmptyOrNewlineOnly()
    {
        Assert.True(RecordMatch.IsBlankLine([], WhitespaceMode.None));
        Assert.True(RecordMatch.IsBlankLine("\n"u8.ToArray(), WhitespaceMode.None));
    }

    [Fact]
    public void IsBlankLine_NoFlags_ReturnsFalseForNonEmptyContent()
    {
        Assert.False(RecordMatch.IsBlankLine("a\n"u8.ToArray(), WhitespaceMode.None));
        Assert.False(RecordMatch.IsBlankLine(" \n"u8.ToArray(), WhitespaceMode.None));
    }

    [Fact]
    public void IsBlankLine_WithWhitespaceFlag_TreatsAllWhitespaceAsBlank()
    {
        Assert.True(RecordMatch.IsBlankLine("   "u8.ToArray(), WhitespaceMode.IgnoreAll));
        Assert.True(RecordMatch.IsBlankLine("\t\n"u8.ToArray(), WhitespaceMode.IgnoreAll));
        Assert.False(RecordMatch.IsBlankLine(" a "u8.ToArray(), WhitespaceMode.IgnoreAll));
    }

    [Fact]
    public void RecordsEqual_ByteEqual_AlwaysMatches()
    {
        byte[] line = "hello world\n"u8.ToArray();

        Assert.True(RecordMatch.RecordsEqual(line, line, WhitespaceMode.None));
        Assert.True(RecordMatch.RecordsEqual(line, line, WhitespaceMode.IgnoreAll));
        Assert.True(RecordMatch.RecordsEqual([], [], WhitespaceMode.None));
    }

    [Fact]
    public void RecordsEqual_ByteDifferentWithoutFlags_ReturnsFalse()
    {
        Assert.False(RecordMatch.RecordsEqual("abc\n"u8.ToArray(), "abd\n"u8.ToArray(), WhitespaceMode.None));
        Assert.False(RecordMatch.RecordsEqual("a b\n"u8.ToArray(), "ab\n"u8.ToArray(), WhitespaceMode.None));
    }

    [Fact]
    public void RecordsEqual_IgnoreAll_MatchesWhitespaceOnlyDifference()
    {
        Assert.True(RecordMatch.RecordsEqual("a b"u8.ToArray(), "ab"u8.ToArray(), WhitespaceMode.IgnoreAll));
        Assert.True(RecordMatch.RecordsEqual("a  b"u8.ToArray(), "a b"u8.ToArray(), WhitespaceMode.IgnoreAll));
        Assert.True(RecordMatch.RecordsEqual("  a b  "u8.ToArray(), "ab"u8.ToArray(), WhitespaceMode.IgnoreAll));
    }

    [Fact]
    public void RecordsEqual_IgnoreChanges_MatchesWhitespaceRunDifferences()
    {
        Assert.True(RecordMatch.RecordsEqual("a b"u8.ToArray(), "a  b"u8.ToArray(), WhitespaceMode.IgnoreChanges));
        Assert.True(RecordMatch.RecordsEqual("a\tb"u8.ToArray(), "a b"u8.ToArray(), WhitespaceMode.IgnoreChanges));
    }

    [Fact]
    public void RecordsEqual_IgnoreChanges_DoesNotMatchMissingWhitespace()
    {
        Assert.False(RecordMatch.RecordsEqual("a b"u8.ToArray(), "ab"u8.ToArray(), WhitespaceMode.IgnoreChanges));
    }

    [Fact]
    public void RecordsEqual_IgnoreAtEol_MatchesTrailingWhitespaceOnly()
    {
        Assert.True(RecordMatch.RecordsEqual("ab "u8.ToArray(), "ab"u8.ToArray(), WhitespaceMode.IgnoreAtEol));
        Assert.True(RecordMatch.RecordsEqual("ab"u8.ToArray(), "ab\t"u8.ToArray(), WhitespaceMode.IgnoreAtEol));
        Assert.False(RecordMatch.RecordsEqual("a b"u8.ToArray(), "ab"u8.ToArray(), WhitespaceMode.IgnoreAtEol));
    }

    [Fact]
    public void RecordsEqual_IgnoreCrAtEol_MatchesLfAndCRLF()
    {
        Assert.True(RecordMatch.RecordsEqual("ab\n"u8.ToArray(), "ab\r\n"u8.ToArray(), WhitespaceMode.IgnoreCrAtEol));
    }

    [Fact]
    public void RecordsEqual_IgnoreCrAtEol_KeepsIncompleteLineCr()
    {
        Assert.False(RecordMatch.RecordsEqual("ab\r"u8.ToArray(), "ab"u8.ToArray(), WhitespaceMode.IgnoreCrAtEol));
        Assert.False(RecordMatch.RecordsEqual("ab"u8.ToArray(), "ab\r"u8.ToArray(), WhitespaceMode.IgnoreCrAtEol));
    }

    [Fact]
    public void RecordsEqual_IgnoreAll_EmptyMatchesWhitespaceOnly()
    {
        Assert.True(RecordMatch.RecordsEqual([], " "u8.ToArray(), WhitespaceMode.IgnoreAll));
        Assert.True(RecordMatch.RecordsEqual("\t"u8.ToArray(), " "u8.ToArray(), WhitespaceMode.IgnoreAll));
    }
}
