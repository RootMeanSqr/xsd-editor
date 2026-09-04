using XsdEditor.Core;
using XsdEditor.Core.Syntax;
using Xunit.Abstractions;

namespace XsdEditor.Core.Tests.Syntax;

/// <summary>
/// The syntax layer's round trip over the reference corpus.
/// </summary>
/// <remarks>
/// <para>
/// <c>0005</c> calls the lexer the single highest-risk piece of Phase 1 and says the
/// round-trip test runs against the whole reference corpus from the first commit. This is
/// that test for the syntax layer: parse each corpus file and assert the tree hands back
/// the identical bytes.
/// </para>
/// <para>
/// The corpus is not in the repository (<c>0004</c>) and is located through
/// <c>XSDEDITOR_CORPUS</c> — a semicolon-separated list of paths or URLs, the first being
/// the entry point. Without it these skip rather than pass, because a suite that reports
/// green without having run is worse than one that reports nothing.
/// </para>
/// </remarks>
public class CorpusRoundTripTests
{
    [SkippableFact]
    public void Every_corpus_file_round_trips_byte_for_byte()
    {
        var files = CorpusFiles();
        SkipIfCorpusAbsent(
            files,
            "The corpus round-trip suite is the acceptance test for XE-069 and XE-083.");

        foreach (var file in files)
        {
            // Read without transcoding line endings: XE-086 pins the round-trip suite to
            // keep-source precisely so it cannot pass on one CI runner and fail on another,
            // and a normalising read here would defeat that. SourceFile rather than
            // File.ReadAllText because the latter discards a byte-order mark, which would
            // make this an assertion about decoded text rather than about bytes.
            var sourceFile = SourceFile.Read(file);
            var tree = SyntaxTree.Parse(sourceFile.Text);

            Assert.True(
                sourceFile.ToBytes(tree.Root.ToFullString()).AsSpan()
                    .SequenceEqual(File.ReadAllBytes(file)),
                $"{Path.GetFileName(file)} did not round trip byte for byte.");

            foreach (var node in tree.Root.DescendantNodesAndSelf())
            {
                if (node.IsToken)
                {
                    continue;
                }

                Assert.True(
                    node.Width == node.ChildNodes().Sum(child => child.Width),
                    $"{Path.GetFileName(file)}: {node.Kind} at {node.Position} breaks the width-sum invariant.");
            }
        }
    }

    [SkippableFact]
    public void The_corpus_parses_without_diagnostics()
    {
        var files = CorpusFiles();
        SkipIfCorpusAbsent(files, "The corpus well-formedness check needs the corpus.");

        foreach (var file in files)
        {
            var tree = SyntaxTree.Parse(SourceFile.Read(file).Text);

            Assert.True(
                tree.IsWellFormed,
                $"{Path.GetFileName(file)}: {string.Join("; ", tree.Diagnostics.Take(5))}");
        }
    }

    /// <summary>
    /// Resolves the corpus entries to files on disk.
    /// </summary>
    /// <returns>
    /// The readable corpus files, or an empty list when the corpus is not available.
    /// </returns>
    /// <remarks>
    /// An entry is either a path, used directly, or a URL, in which case
    /// <c>scripts/verify-corpus.sh</c> has already fetched it into a <c>corpus</c>
    /// directory under its basename. Resolving both here means the suite runs the same way
    /// locally and in CI.
    /// </remarks>
    private static List<string> CorpusFiles()
    {
        var setting = Environment.GetEnvironmentVariable("XSDEDITOR_CORPUS");
        if (string.IsNullOrWhiteSpace(setting))
        {
            return [];
        }

        var files = new List<string>();
        foreach (var entry in setting.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (File.Exists(entry))
            {
                files.Add(entry);
                continue;
            }

            var fetched = FindFetched(Path.GetFileName(entry.TrimEnd('/')));
            if (fetched is not null)
            {
                files.Add(fetched);
            }
        }

        return files;
    }

    /// <summary>
    /// Skips, rather than passes, when the corpus is not available.
    /// </summary>
    /// <param name="files">The resolved corpus files. Empty means unavailable.</param>
    /// <param name="consequence">What did not get tested, for the skip reason.</param>
    /// <remarks>
    /// A suite that reports green without having run is worse than one that reports
    /// nothing, which is why <c>AGENTS.md</c> says these skip — loudly. xUnit 2.9 has no
    /// runtime skip of its own (<c>Assert.Skip</c> arrived in v3), so
    /// <c>Xunit.SkippableFact</c> supplies it; the package is test-only and never reaches
    /// the artifact, which is what puts its MS-PL licence inside <c>AGENTS.md</c>'s rules.
    /// </remarks>
    private static void SkipIfCorpusAbsent(List<string> files, string consequence)
    {
        Skip.If(
            files.Count == 0,
            "XSDEDITOR_CORPUS is not set, or names no file present on disk. "
            + consequence
            + " See docs/decisions/0004-build-and-security-tooling.md.");
    }

    private static string? FindFetched(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        // The test binary sits several directories below the repository root, and CI runs
        // verify-corpus.sh from that root, so walk up looking for the directory it wrote.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "corpus", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
