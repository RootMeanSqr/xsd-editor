namespace XsdEditor.Core.Tests;

public class SourceSpanTests
{
    [Fact]
    public void FromBounds_produces_a_span_covering_the_range()
    {
        var span = SourceSpan.FromBounds(4, 9);

        Assert.Equal(4, span.Start);
        Assert.Equal(5, span.Length);
        Assert.Equal(9, span.End);
    }

    [Fact]
    public void FromBounds_allows_an_empty_range()
    {
        var span = SourceSpan.FromBounds(7, 7);

        Assert.True(span.IsEmpty);
    }

    [Fact]
    public void FromBounds_rejects_an_end_before_the_start()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SourceSpan.FromBounds(9, 4));
    }

    [Fact]
    public void Contains_is_true_for_a_nested_span_and_false_for_an_overhanging_one()
    {
        var outer = SourceSpan.FromBounds(0, 10);

        Assert.True(outer.Contains(SourceSpan.FromBounds(2, 8)));
        Assert.True(outer.Contains(outer));
        Assert.False(outer.Contains(SourceSpan.FromBounds(5, 11)));
    }

    [Fact]
    public void TextIn_returns_the_covered_characters()
    {
        const string Source = "<xs:element name=\"Foo\" />";
        var span = SourceSpan.FromBounds(17, 20);

        Assert.Equal("Foo", span.TextIn(Source).ToString());
    }
}
