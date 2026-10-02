using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;

namespace Xdiff.Benchmarks;

/// <summary>
/// Default benchmark configuration: the default job (full rigor),
/// Markdown/CSV exports, and time + allocation measurement via the
/// per-class <c>[MemoryDiagnoser]</c> attributes. Artifacts are pinned to
/// <c>artifacts/benchmarks/xdiff/</c> under the repository root.
/// </summary>
public sealed class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddExporter(MarkdownExporter.GitHub, CsvExporter.Default);
        WithArtifactsPath(ResolveArtifactsPath());
    }

    private static string ResolveArtifactsPath()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "Xdiff.slnx")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        return directory is null
            ? Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "benchmarks", "xdiff")
            : Path.Combine(directory, "artifacts", "benchmarks", "xdiff");
    }
}
