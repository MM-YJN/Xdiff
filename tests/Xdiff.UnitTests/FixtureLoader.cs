using System.Text;

namespace Xdiff.UnitTests;

internal static class FixtureLoader
{
    public static byte[] LoadBytes(string relativePath)
    {
        string name = $"Xdiff.UnitTests.{relativePath.Replace('/', '.').Replace('\\', '.')}";
        using Stream stream = typeof(FixtureLoader).Assembly.GetManifestResourceStream(name)
            ?? throw new FileNotFoundException($"Embedded fixture not found: {name}");
        byte[] buffer = new byte[stream.Length];
        int read = stream.Read(buffer, 0, buffer.Length);
        if (read != buffer.Length)
        {
            throw new IOException($"Short read for fixture {name}: {read}/{buffer.Length}");
        }

        return buffer;
    }

    public static string LoadText(string relativePath)
    {
        return Encoding.UTF8.GetString(LoadBytes(relativePath));
    }

    public static string StripFramingHeader(string golden)
    {
        int idx = golden.AsSpan().IndexOf("@@");
        return idx < 0 ? golden : golden[idx..];
    }
}
