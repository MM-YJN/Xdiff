using Xdiff.Util;

namespace Xdiff.UnitTests;

public class NumericTests
{
    [Theory]
    [InlineData(0L, 1L)]
    [InlineData(1L, 2L)]
    [InlineData(2L, 2L)]
    [InlineData(3L, 2L)]
    [InlineData(4L, 4L)]
    [InlineData(15L, 4L)]
    [InlineData(16L, 8L)]
    [InlineData(17L, 8L)]
    [InlineData(1024L, 64L)]
    public void BogoSqrt_ReturnsClassicalShiftApprox(long input, long expected)
    {
        Assert.Equal(expected, Numeric.BogoSqrt(input));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(15, 4)]
    [InlineData(16, 4)]
    [InlineData(17, 5)]
    [InlineData(1024, 10)]
    public void HashBits_ReturnsSmallestPowerCoveringSize(int size, int expected)
    {
        Assert.Equal(expected, Numeric.HashBits(size));
    }

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(1L, "1")]
    [InlineData(42L, "42")]
    [InlineData(-1L, "-1")]
    [InlineData(-42L, "-42")]
    [InlineData(long.MaxValue, "9223372036854775807")]
    [InlineData(long.MinValue, "-9223372036854775808")]
    public void NumOut_WritesDecimal(long value, string expected)
    {
        Span<char> buffer = stackalloc char[32];

        int written = Numeric.NumOut(buffer, value);

        Assert.Equal(expected, new string(buffer[..written]));
    }
}
