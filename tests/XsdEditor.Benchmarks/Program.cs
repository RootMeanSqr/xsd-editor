// Performance fixtures for XE-076. 0003 requires the corpus file to become a permanent
// performance fixture, re-measured rather than measured once.

using BenchmarkDotNet.Running;

namespace XsdEditor.Benchmarks;

internal static class Program
{
    private static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
