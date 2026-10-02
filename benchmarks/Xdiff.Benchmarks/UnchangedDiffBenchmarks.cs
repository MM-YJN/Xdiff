using BenchmarkDotNet.Attributes;

namespace Xdiff.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("diff")]
public class UnchangedDiffBenchmarks
{
    private byte[] _old = null!;
    private byte[] _new = null!;
    private DiffOptions _options = null!;

    [GlobalSetup]
    public void Setup()
    {
        _old = FixtureFactory.Encode(FixtureFactory.Lines(2_000));
        _new = (byte[])_old.Clone();
        _options = new DiffOptions();
        if (!Compute().IsEmpty)
        {
            throw new InvalidOperationException("Identical inputs must produce an empty diff.");
        }
    }

    [Benchmark]
    public DiffResult Compute() => Diff.Compute(_old, _new, _options);
}
