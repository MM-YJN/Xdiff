using BenchmarkDotNet.Running;

using Xdiff.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(BenchmarkConfig).Assembly).Run(args, new BenchmarkConfig());
