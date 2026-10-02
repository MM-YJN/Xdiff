using System.Globalization;
using System.Text;

namespace Xdiff.Benchmarks;

internal static class FixtureFactory
{
    public static string[] Lines(int count, bool duplicateHeavy = false)
    {
        string[] lines = new string[count];
        for (int i = 0; i < count; i++)
        {
            int identity = duplicateHeavy ? i % 8 : i;
            lines[i] = "line " + identity.ToString("D5", CultureInfo.InvariantCulture) + " café payload\n";
        }

        return lines;
    }

    public static byte[] Encode(IEnumerable<string> lines) => Encoding.UTF8.GetBytes(string.Concat(lines));

    public static (byte[] Old, byte[] New) Churn(int count, bool duplicateHeavy = false)
    {
        string[] original = Lines(count, duplicateHeavy);
        var changed = new List<string>();
        for (int i = 0; i < count; i++)
        {
            // Every 20-line block has an insertion, a deletion, and a replacement.
            if (i % 20 == 3)
            {
                changed.Add("inserted café\n");
            }

            if (i % 20 != 8)
            {
                changed.Add(i % 20 == 14 ? "replacement café\n" : original[i]);
            }
        }

        return (Encode(original), Encode(changed));
    }

    public static (byte[] Ancestor, byte[] Ours, byte[] Theirs, byte[] Expected) MergeInputs(int count, string scenario)
    {
        string[] ancestor = Lines(count);
        string[] ours = (string[])ancestor.Clone();
        string[] theirs = (string[])ancestor.Clone();
        string[] expected = (string[])ancestor.Clone();
        int first = count / 4;
        int second = count * 3 / 4;
        if (scenario != "UnchangedSide")
        {
            ours[first] = "ours café\n";
            expected[first] = ours[first];
        }

        if (scenario == "Conflicting")
        {
            // Shared edges inside a replacement exercise zdiff3 conflict refinement.
            ours[first] = "shared start\nours café\nshared end\n";
            theirs[first] = "shared start\ntheirs café\nshared end\n";
        }
        else
        {
            theirs[second] = "theirs café\n";
            expected[second] = theirs[second];
        }

        return (Encode(ancestor), Encode(ours), Encode(theirs), Encode(expected));
    }

    public static void ValidateMerge(MergeResult result, byte[] expected, bool conflicting, MergeStyle style)
    {
        if (!conflicting)
        {
            if (result.HasConflicts || !result.Content.AsSpan().SequenceEqual(expected))
            {
                throw new InvalidOperationException("Clean merge did not produce the expected bytes.");
            }

            return;
        }

        ReadOnlySpan<byte> content = result.Content;
        if (!result.HasConflicts || content.IndexOf("<<<<<<<"u8) < 0 || content.IndexOf("======="u8) < 0 || content.IndexOf(">>>>>>>"u8) < 0
            || (content.IndexOf("|||||||"u8) >= 0) != (style != MergeStyle.Merge))
        {
            throw new InvalidOperationException("Conflicting merge did not produce the expected conflict markers.");
        }
    }
}
