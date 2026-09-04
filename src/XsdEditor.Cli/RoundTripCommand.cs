using System.Globalization;
using XsdEditor.Core.Syntax;

namespace XsdEditor.Cli;

// `xsdedit roundtrip` — parse a file and re-serialise it, reporting any difference.
//
// This is the acceptance harness for XE-069 and XE-083 (0004): the syntax tree claims that
// serialising an unmodified node is a copy of its original characters, and this is what
// makes that claim falsifiable from CI without a display.
internal static class RoundTripCommand
{
    public static int Run(IReadOnlyList<string> files)
    {
        if (files.Count == 0)
        {
            Console.Error.WriteLine("xsdedit roundtrip: no file given.");
            return 2;
        }

        var failed = 0;
        foreach (var file in files)
        {
            if (!File.Exists(file))
            {
                Fail($"{file}: no such file.");
                failed++;
                continue;
            }

            // Read without transcoding line endings. XE-086 pins the round-trip suite to
            // keep-source so that it cannot pass on one CI runner and fail on another, and a
            // normalising read here would hide exactly the bug it exists to catch.
            var source = File.ReadAllText(file);
            var tree = SyntaxTree.Parse(source);
            var written = tree.Root.ToFullString();

            if (written == source)
            {
                var noun = tree.Diagnostics.Count == 1 ? "diagnostic" : "diagnostics";
                Console.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"ok    {file} ({source.Length} code units, {tree.Diagnostics.Count} {noun})"));
            }
            else
            {
                Fail($"{file}: round trip changed the file. {DescribeFirstDifference(source, written)}");
                failed++;
            }

            // A diagnostic is not a round-trip failure — a malformed file is still expected
            // to survive one (XE-031) — but on the reference corpus it means the parser is
            // wrong, so it is reported rather than swallowed.
            foreach (var diagnostic in tree.Diagnostics.Take(10))
            {
                Warn($"{file}: {diagnostic}");
            }
        }

        return failed == 0 ? 0 : 1;
    }

    private static string DescribeFirstDifference(string expected, string actual)
    {
        var limit = Math.Min(expected.Length, actual.Length);
        var index = 0;
        while (index < limit && expected[index] == actual[index])
        {
            index++;
        }

        if (index == limit)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Lengths differ: expected {expected.Length} code units, wrote {actual.Length}.");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"First difference at offset {index}: expected {Show(expected, index)}, wrote {Show(actual, index)}.");
    }

    private static string Show(string text, int index)
    {
        var start = Math.Max(0, index - 20);
        var end = Math.Min(text.Length, index + 20);
        return "\"" + text[start..end].Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
    }

    // scripts/verify-corpus.sh's convention: GitHub Actions renders ::error:: and
    // ::warning:: on the run summary, and outside CI they would be noise.
    private static bool InActions =>
        Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";

    private static void Fail(string message) =>
        Console.Error.WriteLine(InActions ? $"::error::{message}" : $"ERROR: {message}");

    private static void Warn(string message) =>
        Console.Error.WriteLine(InActions ? $"::warning::{message}" : $"WARNING: {message}");
}
