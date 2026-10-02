using BenchmarkDotNet.Attributes;

namespace Xdiff.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("merge")]
public class MergeBenchmarks
{
    private byte[] _ancestor = null!;
    private byte[] _ours = null!;
    private byte[] _theirs = null!;
    private MergeOptions _options = null!;

    [Params(50, 2_000)]
    public int LineCount { get; set; }

    [Params("UnchangedSide", "Disjoint", "Conflicting")]
    public string Scenario { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        (_ancestor, _ours, _theirs, byte[] expected) = FixtureFactory.MergeInputs(LineCount, Scenario);
        _options = new MergeOptions();
        FixtureFactory.ValidateMerge(Merge(), expected, Scenario == "Conflicting", _options.Style);
    }

    [Benchmark]
    public MergeResult Merge() => Merger.Merge(_ancestor, _ours, _theirs, _options);
}
