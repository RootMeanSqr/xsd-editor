using XsdEditor.Core.Syntax;

namespace XsdEditor.Core.Tests.Syntax;

/// <summary>
/// What the tree says about a document, as distinct from the invariants that hold over
/// every tree.
/// </summary>
public class XmlParserTests
{
    [Fact]
    public void An_empty_element_tag_produces_one_element_with_no_end_tag()
    {
        var tree = SyntaxTree.Parse("<xs:schema/>");

        var element = Assert.Single(tree.Root.ChildNodes(), node => node.Kind == SyntaxKind.Element);
        var tag = Assert.Single(element.ChildNodes());

        Assert.Equal(SyntaxKind.EmptyElementTag, tag.Kind);
        Assert.Equal("xs:schema", NameOf(tag));
    }

    [Fact]
    public void A_start_tag_and_end_tag_bracket_the_elements_content()
    {
        var tree = SyntaxTree.Parse("<a>text<b/></a>");

        var element = FirstElement(tree);
        var kinds = element.ChildNodes().Select(node => node.Kind).ToArray();

        Assert.Equal(
            [SyntaxKind.StartTag, SyntaxKind.TextToken, SyntaxKind.Element, SyntaxKind.EndTag],
            kinds);
    }

    [Fact]
    public void Character_references_keep_their_original_spelling()
    {
        // XE-069. The corpus writes a space as &#x20; inside a character class deliberately,
        // because a literal space there cannot be seen. Resolving it and re-serialising —
        // which is what XmlReader would force — silently rewrites the schema's meaning to a
        // reader, and 0005 chose our own lexer precisely to avoid it.
        const string Source = "<xs:pattern value=\"[A-Z&#x20;a-z]&amp;\"/>";
        var tree = SyntaxTree.Parse(Source);

        var value = Assert.Single(
            tree.Root.DescendantNodes().OfType<SyntaxToken>(),
            token => token.Kind == SyntaxKind.AttributeValueToken);

        Assert.Equal("\"[A-Z&#x20;a-z]&amp;\"", value.Text);
        Assert.Equal(Source, tree.Root.ToFullString());
    }

    [Fact]
    public void An_attribute_value_token_carries_its_own_quotes()
    {
        // XE-068 preserves foreign attributes byte for byte, and which quote the source
        // chose is one of those bytes. Stripping quotes into the token's text would lose it.
        var tree = SyntaxTree.Parse("<a x='single' y=\"double\"/>");

        var values = tree.Root.DescendantNodes().OfType<SyntaxToken>()
            .Where(token => token.Kind == SyntaxKind.AttributeValueToken)
            .Select(token => token.Text)
            .ToArray();

        Assert.Equal(["'single'", "\"double\""], values);
    }

    [Fact]
    public void Whitespace_inside_a_tag_belongs_to_the_tag()
    {
        var tree = SyntaxTree.Parse("<a\n  name = \"v\" />");

        Assert.Equal("<a\n  name = \"v\" />", tree.Root.ToFullString());
        Assert.True(tree.IsWellFormed);

        var attribute = Assert.Single(
            tree.Root.DescendantNodes(),
            node => node.Kind == SyntaxKind.XmlAttribute);
        Assert.Equal("name = \"v\"", attribute.ToFullString());
    }

    [Fact]
    public void A_comment_is_one_token_and_survives_intact()
    {
        // XE-067.
        var tree = SyntaxTree.Parse("<a><!-- keep <this> & that --></a>");

        var comment = Assert.Single(
            tree.Root.DescendantNodes().OfType<SyntaxToken>(),
            token => token.Kind == SyntaxKind.CommentToken);

        Assert.Equal("<!-- keep <this> & that -->", comment.Text);
    }

    [Fact]
    public void The_xml_declaration_is_told_apart_from_an_ordinary_processing_instruction()
    {
        var tree = SyntaxTree.Parse("<?xml version=\"1.0\"?><?xml-stylesheet href=\"x\"?><a/>");

        var kinds = tree.Root.ChildNodes()
            .Select(node => node.Kind)
            .Where(kind => kind is SyntaxKind.XmlDeclarationToken or SyntaxKind.ProcessingInstructionToken)
            .ToArray();

        Assert.Equal(
            [SyntaxKind.XmlDeclarationToken, SyntaxKind.ProcessingInstructionToken],
            kinds);
    }

    [Fact]
    public void A_cdata_section_is_one_token_and_its_markup_is_not_parsed()
    {
        var tree = SyntaxTree.Parse("<a><![CDATA[<b/> & ]]></a>");

        var cdata = Assert.Single(
            tree.Root.DescendantNodes().OfType<SyntaxToken>(),
            token => token.Kind == SyntaxKind.CdataSectionToken);

        Assert.Equal("<![CDATA[<b/> & ]]>", cdata.Text);
        Assert.DoesNotContain(
            tree.Root.DescendantNodes(),
            node => node.Kind == SyntaxKind.Element && NameOf(node.ChildNodes().First()) == "b");
    }

    [Fact]
    public void An_unclosed_element_is_reported_and_still_holds_its_content()
    {
        var tree = SyntaxTree.Parse("<a><b/>");

        Assert.Contains(tree.Diagnostics, d => d.Code == SyntaxDiagnosticCode.UnclosedElement);

        var outer = FirstElement(tree);
        Assert.Null(outer.ChildOfKind(SyntaxKind.EndTag));
        Assert.Contains(outer.ChildNodes(), node => node.Kind == SyntaxKind.Element);
    }

    [Fact]
    public void A_mismatched_end_tag_closes_the_inner_element_and_is_left_for_its_owner()
    {
        var tree = SyntaxTree.Parse("<a><b></a>");

        Assert.Contains(tree.Diagnostics, d => d.Code == SyntaxDiagnosticCode.MismatchedEndTag);

        var outer = FirstElement(tree);
        var inner = Assert.Single(outer.ChildNodes(), node => node.Kind == SyntaxKind.Element);

        // The inner element has no end tag; the </a> went to the element that opened it.
        Assert.Null(inner.ChildOfKind(SyntaxKind.EndTag));
        Assert.NotNull(outer.ChildOfKind(SyntaxKind.EndTag));
    }

    [Fact]
    public void An_end_tag_for_an_element_never_opened_is_reported_where_it_is_met()
    {
        var tree = SyntaxTree.Parse("</a>");

        var diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Equal(SyntaxDiagnosticCode.UnexpectedEndTag, diagnostic.Code);
        Assert.Equal("</a>", tree.Root.ToFullString());
    }

    [Fact]
    public void An_unterminated_attribute_value_stops_at_the_end_of_the_line()
    {
        // One missing quote should cost one attribute, not the rest of the document. If the
        // value ran to the next quote wherever it was, everything up to it would vanish into
        // the value and XE-031's partial render would lose the whole file.
        var tree = SyntaxTree.Parse("<a name=\"unterminated>\n<b/>");

        Assert.Contains(
            tree.Diagnostics,
            d => d.Code == SyntaxDiagnosticCode.UnterminatedAttributeValue);
        Assert.Contains(
            tree.Root.DescendantNodes(),
            node => node.Kind == SyntaxKind.Element
                && NameOf(node.ChildNodes().First()) == "b");
    }

    [Fact]
    public void A_gap_records_what_could_not_be_parsed_positionally()
    {
        // XE-031 identifies unparsed regions positionally rather than discarding them.
        const string Source = "<a>1 < 2</a>";
        var tree = SyntaxTree.Parse(Source);

        var gap = Assert.Single(
            tree.Root.DescendantNodes().OfType<SyntaxToken>(),
            token => token.Kind == SyntaxKind.GapToken);

        Assert.Equal("< 2", gap.Text);
        Assert.Equal("< 2", gap.Span.TextIn(Source).ToString());
    }

    [Fact]
    public void A_document_type_declaration_survives_its_internal_subset()
    {
        const string Source = "<!DOCTYPE a [ <!ENTITY x \"y\"> ]>\n<a/>";
        var tree = SyntaxTree.Parse(Source);

        var doctype = Assert.Single(
            tree.Root.DescendantNodes().OfType<SyntaxToken>(),
            token => token.Kind == SyntaxKind.DocumentTypeToken);

        // The '>' closing the entity declaration must not end the doctype.
        Assert.Equal("<!DOCTYPE a [ <!ENTITY x \"y\"> ]>", doctype.Text);
        Assert.True(tree.IsWellFormed);
    }

    [Fact]
    public void Parsing_the_empty_buffer_yields_a_document_of_width_zero()
    {
        var tree = SyntaxTree.Parse(string.Empty);

        Assert.Equal(0, tree.Root.Width);
        Assert.Equal(SyntaxKind.Document, tree.Root.Kind);
        Assert.True(tree.IsWellFormed);
    }

    [Fact]
    public void Parse_rejects_a_null_buffer()
    {
        Assert.Throws<ArgumentNullException>(() => SyntaxTree.Parse(null!));
    }

    [Fact]
    public void A_raw_ampersand_in_annotation_text_is_allowed()
    {
        // XE-070. Non-conforming schemas write "R&D" in documentation, and the editor has to
        // open them. The corpus has no instance of this, which is exactly why it is a test.
        var tree = SyntaxTree.Parse(
            "<xs:annotation><xs:documentation>Tom & Jerry, R&D</xs:documentation></xs:annotation>");

        Assert.True(tree.IsWellFormed, string.Join("; ", tree.Diagnostics));
    }

    [Theory]
    [InlineData("annotation")]
    [InlineData("documentation")]
    [InlineData("appinfo")]
    public void The_leniency_covers_each_annotation_element(string local)
    {
        var tree = SyntaxTree.Parse($"<xs:{local}>a & b</xs:{local}>");

        Assert.True(tree.IsWellFormed, string.Join("; ", tree.Diagnostics));
    }

    [Fact]
    public void The_leniency_reaches_elements_nested_below_documentation()
    {
        // xs:documentation takes arbitrary markup, so the tolerance has to be inherited by
        // descendants rather than applying only to its immediate text.
        var tree = SyntaxTree.Parse("<xs:documentation><b><i>a & b</i></b></xs:documentation>");

        Assert.True(tree.IsWellFormed, string.Join("; ", tree.Diagnostics));
    }

    [Fact]
    public void A_raw_ampersand_in_ordinary_element_text_is_reported_on_the_ampersand_itself()
    {
        const string Source = "<xs:element>Tom & Jerry</xs:element>";
        var tree = SyntaxTree.Parse(Source);

        var diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Equal(SyntaxDiagnosticCode.RawAmpersand, diagnostic.Code);

        // Derived rather than hand-counted, so the test does not encode its author's
        // arithmetic. The span must cover the '&' alone, so a caret can be put on it.
        Assert.Equal(Source.IndexOf('&', StringComparison.Ordinal), diagnostic.Span.Start);
        Assert.Equal(1, diagnostic.Span.Length);
        Assert.Equal("&", diagnostic.Span.TextIn(Source).ToString());
    }

    [Fact]
    public void The_leniency_stops_when_the_annotation_element_closes()
    {
        var tree = SyntaxTree.Parse(
            "<a><xs:documentation>in & here</xs:documentation>out & here</a>");

        var diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Equal(SyntaxDiagnosticCode.RawAmpersand, diagnostic.Code);
        Assert.Equal("out ", tree.Text[(diagnostic.Span.Start - 4)..diagnostic.Span.Start]);
    }

    [Fact]
    public void An_attribute_value_stays_strict_even_inside_an_annotation()
    {
        // XE-070 scopes the rule to annotation and documentation *text*, and says in terms
        // that it does not apply to attribute content.
        var tree = SyntaxTree.Parse("<xs:documentation><b title=\"R&D\">text</b></xs:documentation>");

        var diagnostic = Assert.Single(tree.Diagnostics);
        Assert.Equal(SyntaxDiagnosticCode.RawAmpersand, diagnostic.Code);
    }

    [Fact]
    public void Every_raw_ampersand_in_a_run_is_reported_separately()
    {
        var tree = SyntaxTree.Parse("<a>R&D and AT&T and B&Q</a>");

        Assert.Equal(3, tree.Diagnostics.Count);
        Assert.All(tree.Diagnostics, d => Assert.Equal(SyntaxDiagnosticCode.RawAmpersand, d.Code));
    }

    [Theory]
    [InlineData("&amp;")]
    [InlineData("&lt;")]
    [InlineData("&gt;")]
    [InlineData("&quot;")]
    [InlineData("&apos;")]
    [InlineData("&#38;")]
    [InlineData("&#x20;")]
    [InlineData("&#X7E;")]
    public void A_valid_reference_is_never_reported_as_a_raw_ampersand(string reference)
    {
        var tree = SyntaxTree.Parse($"<a>{reference}</a>");

        Assert.True(tree.IsWellFormed, string.Join("; ", tree.Diagnostics));
    }

    [Theory]
    [InlineData("&notdeclared;")]
    [InlineData("&#x20")]
    [InlineData("&#;")]
    [InlineData("&;")]
    [InlineData("&")]
    public void Something_that_only_looks_like_a_reference_is_reported(string text)
    {
        // 0005 puts DTDs outside scope, so there is no entity declaration to consult: an
        // ampersand that does not open a numeric or predefined reference is a raw one.
        var tree = SyntaxTree.Parse($"<a>{text}</a>");

        Assert.Contains(tree.Diagnostics, d => d.Code == SyntaxDiagnosticCode.RawAmpersand);
    }

    [Fact]
    public void An_ampersand_inside_a_cdata_section_is_not_a_reference_and_not_an_error()
    {
        // Inside CDATA nothing is markup, so there is nothing to escape and nothing to report.
        var tree = SyntaxTree.Parse("<a><![CDATA[Tom & Jerry]]></a>");

        Assert.True(tree.IsWellFormed, string.Join("; ", tree.Diagnostics));
    }

    private static SyntaxNode FirstElement(SyntaxTree tree) =>
        tree.Root.DescendantNodes().First(node => node.Kind == SyntaxKind.Element);

    private static string NameOf(SyntaxNode tag) =>
        ((SyntaxToken)tag.ChildOfKind(SyntaxKind.NameToken)!).Text;
}
