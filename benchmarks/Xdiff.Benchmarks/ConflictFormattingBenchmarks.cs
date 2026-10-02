using BenchmarkDotNet.Attributes;

namespace Xdiff.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("merge")]
public class ConflictFormattingBenchmarks
{
    private byte[] _ancestor = null!;
    private byte[] _ours = null!;
    private byte[] _theirs = null!;
    private MergeOptions _options = null!;

    [Params(50, 2_000)]
    public int LineCount { get; set; }

    [Params(MergeStyle.Merge, MergeStyle.Diff3, MergeStyle.ZealousDiff3)]
    public MergeStyle Style { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        (_ancestor, _ours, _theirs, byte[] expected) = FixtureFactory.MergeInputs(LineCount, "Conflicting");
        _options = new MergeOptions { Style = Style };
        FixtureFactory.ValidateMerge(Merge(), expected, true, Style);
    }

    [Benchmark]
    public MergeResult Merge() => Merger.Merge(_ancestor, _ours, _theirs, _options);
}
