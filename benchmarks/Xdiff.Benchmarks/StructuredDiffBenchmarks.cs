using BenchmarkDotNet.Attributes;

namespace Xdiff.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("diff")]
public class StructuredDiffBenchmarks
{
    private byte[] _old = null!;
    private byte[] _new = null!;
    private DiffOptions _options = null!;

    [Params("SmallChurn", "LargeChurn", "DuplicateHeavy")]
    public string Input { get; set; } = null!;

    [Params(DiffAlgorithm.Myers, DiffAlgorithm.Minimal, DiffAlgorithm.Patience, DiffAlgorithm.Histogram)]
    public DiffAlgorithm Algorithm { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        int count = Input == "SmallChurn" ? 50 : Input == "LargeChurn" ? 2_000 : 1_000;
        (_old, _new) = FixtureFactory.Churn(count, Input == "DuplicateHeavy");
        _options = new DiffOptions { Algorithm = Algorithm };
        if (Compute().IsEmpty)
        {
            throw new InvalidOperationException("Changed inputs must produce diff hunks.");
        }
    }

    [Benchmark]
    public DiffResult Compute() => Diff.Compute(_old, _new, _options);
}
