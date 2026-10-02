using BenchmarkDotNet.Attributes;

namespace Xdiff.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("unified")]
public class UnifiedDiffBenchmarks
{
    private byte[] _old = null!;
    private byte[] _new = null!;
    private DiffOptions _options = null!;

    [Params("Small", "Large", "LargeZeroContext")]
    public string Input { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        (_old, _new) = FixtureFactory.Churn(Input == "Small" ? 50 : 2_000);
        _options = new DiffOptions { ContextLines = Input == "LargeZeroContext" ? 0 : 3 };
        byte[] result = UnifiedDiff();
        if (!result.AsSpan().StartsWith("@@ "u8) || Diff.Compute(_old, _new, _options).IsEmpty)
        {
            throw new InvalidOperationException("Changed inputs must produce unified diff hunks.");
        }
    }

    [Benchmark]
    public byte[] UnifiedDiff() => Diff.UnifiedDiff(_old, _new, _options);
}
