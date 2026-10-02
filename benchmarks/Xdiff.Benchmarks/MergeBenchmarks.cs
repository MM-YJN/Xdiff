using System.Text;

using BenchmarkDotNet.Attributes;

namespace Xdiff.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("merge")]
public class MergeBenchmarks
{
    private byte[] _ancestor = null!;
    private byte[] _ours = null!;
    private byte[] _theirs = null!;
    private string _ancestorText = null!;
    private string _oursText = null!;
    private string _theirsText = null!;
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
        _ancestorText = Encoding.UTF8.GetString(_ancestor);
        _oursText = Encoding.UTF8.GetString(_ours);
        _theirsText = Encoding.UTF8.GetString(_theirs);
        if (MergeString() != Encoding.UTF8.GetString(Merge().Content))
        {
            throw new InvalidOperationException("String merge must match byte merge output.");
        }

        FixtureFactory.ValidateMerge(Merge(), expected, Scenario == "Conflicting", _options.Style);
    }

    [Benchmark]
    public string MergeString() => Merger.Merge(_ancestorText, _oursText, _theirsText, _options);

    [Benchmark]
    public MergeResult Merge() => Merger.Merge(_ancestor, _ours, _theirs, _options);
}
