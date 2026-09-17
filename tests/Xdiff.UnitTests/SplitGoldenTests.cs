using Xdiff;

namespace Xdiff.UnitTests;

public class SplitGoldenTests
{
    public static TheoryData<string> SplitCases { get; } = new()
    {
        "diff_1_single_100",
        "diff_2_scattered_200",
        "diff_3_many_300",
        "diff_4_long_snakes_500",
        "diff_5_no_match_1000",
        "diff_7_shuffle_2000",
        "diff_8_shuffle_400_snake",
        "diff_9_shuffle_700_snake",
    };

    [Theory]
    [MemberData(nameof(SplitCases))]
    public void UnifiedDiff_MatchesCGolden(string name)
    {
        string oldText = FixtureLoader.LoadText($"Fixtures/split/{name}_old.txt");
        string newText = FixtureLoader.LoadText($"Fixtures/split/{name}_new.txt");
        string golden = FixtureLoader.LoadText($"Fixtures/split/{name}.expected");

        string output = Diff.UnifiedDiff(oldText, newText);

        Assert.Equal(golden, output);
    }

    public static TheoryData<string> MinimalCases { get; } = new()
    {
        "diff_6_minimal_200",
        "diff_10_minimal_shuffle_2000",
    };

    [Theory]
    [MemberData(nameof(MinimalCases))]
    public void MinimalAlgorithm_MatchesCGolden(string name)
    {
        string oldText = FixtureLoader.LoadText($"Fixtures/split/{name}_old.txt");
        string newText = FixtureLoader.LoadText($"Fixtures/split/{name}_new.txt");
        string golden = FixtureLoader.LoadText($"Fixtures/split/{name}.expected");

        string output = Diff.UnifiedDiff(oldText, newText, new DiffOptions
        {
            Algorithm = DiffAlgorithm.Minimal,
        });

        Assert.Equal(golden, output);
    }
}
