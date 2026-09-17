namespace Xdiff.UnitTests;

public class AlgorithmEquivalenceTests
{
    [Theory]
    [InlineData("html")]
    [InlineData("javascript")]
    [InlineData("php")]
    public void AllAlgorithms_ProduceIdenticalOutput_OnGoldenInputs(string ext)
    {
        byte[] before = FixtureLoader.LoadBytes($"Fixtures/diff/before/file.{ext}");
        byte[] after = FixtureLoader.LoadBytes($"Fixtures/diff/after/file.{ext}");

        var opts = new DiffOptions
        {
            ContextLines = 1,
            InterHunkLines = 1,
            IncludeFunctionNames = true,
        };

        byte[] myers = Diff.UnifiedDiff(before, after, opts with { Algorithm = DiffAlgorithm.Myers });
        byte[] minimal = Diff.UnifiedDiff(before, after, opts with { Algorithm = DiffAlgorithm.Minimal });
        byte[] patience = Diff.UnifiedDiff(before, after, opts with { Algorithm = DiffAlgorithm.Patience });
        byte[] histogram = Diff.UnifiedDiff(before, after, opts with { Algorithm = DiffAlgorithm.Histogram });

        Assert.Equal(myers, minimal);
        Assert.Equal(myers, patience);
        Assert.Equal(myers, histogram);
    }
}
