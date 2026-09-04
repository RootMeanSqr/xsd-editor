using XsdEditor.Core.Syntax;

namespace XsdEditor.Core.Tests.Syntax;

/// <summary>
/// The two invariants <c>0005</c> records, plus the round trip they exist to guarantee.
/// </summary>
/// <remarks>
/// <c>0005</c> says these are tests from the first commit rather than comments, because
/// they are what turns "lossless" from an intention into something CI can fail on. They
/// run over every fixture, malformed input included — a tree that dropped a character
/// while recovering would still look right in every structural assertion.
/// </remarks>
public class SyntaxInvariantTests
{
    [Theory]
    [MemberData(nameof(SyntaxFixtures.AllRows), MemberType = typeof(SyntaxFixtures))]
    public void A_nodes_width_is_the_sum_of_its_childrens_widths(string name, string source)
    {
        var tree = SyntaxTree.Parse(source);

        foreach (var node in tree.Root.DescendantNodesAndSelf())
        {
            if (node.IsToken)
            {
                continue;
            }

            var sum = node.ChildNodes().Sum(child => child.Width);

            Assert.True(
                node.Width == sum,
                $"{name}: {node.Kind} at {node.Position} has width {node.Width} "
                + $"but its children sum to {sum}. Some character is claimed twice or dropped.");
        }
    }

    [Theory]
    [MemberData(nameof(SyntaxFixtures.AllRows), MemberType = typeof(SyntaxFixtures))]
    public void The_tree_covers_every_character_of_the_source(string name, string source)
    {
        var tree = SyntaxTree.Parse(source);

        Assert.True(
            tree.Root.Width == source.Length,
            $"{name}: the root covers {tree.Root.Width} code units of a {source.Length}-unit buffer.");
        Assert.Equal(source, tree.Root.ToFullString());
    }

    [Theory]
    [MemberData(nameof(SyntaxFixtures.AllRows), MemberType = typeof(SyntaxFixtures))]
    public void Every_token_reports_the_span_it_was_parsed_from(string name, string source)
    {
        var tree = SyntaxTree.Parse(source);

        foreach (var token in tree.Root.DescendantNodesAndSelf().OfType<SyntaxToken>())
        {
            Assert.True(
                token.Span.End <= source.Length,
                $"{name}: {token.Kind} runs past the end of the buffer.");
            Assert.Equal(token.Text, token.Span.TextIn(source).ToString());
        }
    }

    [Theory]
    [MemberData(nameof(SyntaxFixtures.AllRows), MemberType = typeof(SyntaxFixtures))]
    public void Tokens_tile_the_buffer_in_order_without_overlap_or_gap(string name, string source)
    {
        var tree = SyntaxTree.Parse(source);

        var next = 0;
        foreach (var token in tree.Root.DescendantNodesAndSelf().OfType<SyntaxToken>())
        {
            Assert.True(
                token.Position == next,
                $"{name}: {token.Kind} starts at {token.Position}, expected {next}.");
            next = token.Span.End;
        }

        Assert.Equal(source.Length, next);
    }

    [Fact]
    public void Width_is_counted_in_UTF16_code_units_not_bytes_or_scalar_values()
    {
        // A CRLF is two code units; an emoji outside the BMP is two, being a surrogate
        // pair; and "é" is one code unit but two bytes in UTF-8. A width in bytes or in
        // scalar values would disagree with at least one of these, and would do so only in
        // files containing non-ASCII — which is why 0005 fixes the unit rather than leaving
        // it to whichever call site is written first.
        const string Source = "<a>\r\n\U0001F600é</a>";
        var tree = SyntaxTree.Parse(Source);

        Assert.Equal(Source.Length, tree.Root.Width);

        var text = Assert.Single(
            tree.Root.DescendantNodes().OfType<SyntaxToken>(),
            token => token.Kind == SyntaxKind.TextToken);

        Assert.Equal("\r\n\U0001F600é", text.Text);
        Assert.Equal(5, text.Width);
    }

    [Theory]
    [MemberData(nameof(SyntaxFixtures.AllRows), MemberType = typeof(SyntaxFixtures))]
    public void A_childs_position_is_its_parents_plus_the_widths_before_it(string name, string source)
    {
        var tree = SyntaxTree.Parse(source);

        foreach (var node in tree.Root.DescendantNodesAndSelf())
        {
            var expected = node.Position;
            foreach (var child in node.ChildNodes())
            {
                Assert.True(
                    child.Position == expected,
                    $"{name}: {child.Kind} under {node.Kind} is at {child.Position}, expected {expected}.");
                Assert.True(
                    node.Span.Contains(child.Span),
                    $"{name}: {child.Kind} is not contained by its parent {node.Kind}.");
                expected += child.Width;
            }
        }
    }

    [Fact]
    public void Red_nodes_are_created_on_descent_rather_than_cached()
    {
        // 0005 makes the red layer a throwaway façade. Asking twice yields two equal but
        // distinct objects; if this ever starts returning the same instance, the layer has
        // grown a cache and the sharing the green layer exists for is being given away.
        var tree = SyntaxTree.Parse("<a><b/></a>");

        var first = tree.Root.ChildNodes().First();
        var second = tree.Root.ChildNodes().First();

        Assert.NotSame(first, second);
        Assert.Equal(first.Kind, second.Kind);
        Assert.Equal(first.Span, second.Span);
    }

    [Theory]
    [MemberData(nameof(SyntaxFixtures.MalformedRows), MemberType = typeof(SyntaxFixtures))]
    public void Malformed_input_parses_to_a_tree_and_reports_rather_than_throwing(string name, string source)
    {
        // XE-031: a best-effort partial render, not a cleared canvas and not an exception.
        var tree = SyntaxTree.Parse(source);

        Assert.Equal(source, tree.Root.ToFullString());
        Assert.False(tree.IsWellFormed, $"{name}: expected at least one diagnostic.");
        Assert.All(tree.Diagnostics, diagnostic =>
        {
            Assert.NotEqual(SyntaxDiagnosticCode.None, diagnostic.Code);
            Assert.True(
                diagnostic.Span.End <= source.Length,
                $"{name}: a diagnostic runs past the end of the buffer.");
        });
    }

    [Theory]
    [MemberData(nameof(SyntaxFixtures.WellFormedRows), MemberType = typeof(SyntaxFixtures))]
    public void Well_formed_input_reports_no_diagnostics(string name, string source)
    {
        var tree = SyntaxTree.Parse(source);

        Assert.True(
            tree.IsWellFormed,
            $"{name}: unexpected diagnostics — {string.Join("; ", tree.Diagnostics)}");
    }
}
