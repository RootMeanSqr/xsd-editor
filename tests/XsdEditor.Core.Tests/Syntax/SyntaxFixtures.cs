namespace XsdEditor.Core.Tests.Syntax;

/// <summary>
/// The source buffers the syntax invariants are asserted over.
/// </summary>
/// <remarks>
/// Each one exists for a named reason. The reference corpus is the real fixture for the
/// round-trip suite, but it is not in the repository and is only present when
/// <c>XSDEDITOR_CORPUS</c> is set, so these carry the invariants on every machine — and
/// they cover deliberately awkward cases the corpus happens not to contain, notably
/// malformed input, which no valid corpus can exercise.
/// </remarks>
public static class SyntaxFixtures
{
    /// <summary>Well-formed buffers, keyed by what each is for.</summary>
    public static IReadOnlyDictionary<string, string> WellFormed { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["empty buffer"] = string.Empty,

            ["a single empty element"] = "<xs:schema/>",

            ["declaration, comment and root"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
                + "<!-- a leading comment -->\n"
                + "<xs:schema xmlns:xs=\"http://www.w3.org/2001/XMLSchema\">\n"
                + "</xs:schema>\n",

            // XE-069: the corpus spells a space as &#x20; inside a character class on
            // purpose, because a literal space there is invisible. The tree must hand back
            // those six characters, not one.
            ["character references in a pattern facet"] =
                "<xs:pattern value=\"[A-Z&#x20;a-z&#x7E;]&amp;&lt;&quot;\"/>",

            // XE-068: an attribute in a foreign namespace is preserved byte for byte, and
            // the source's choice of quote is part of those bytes.
            ["foreign attributes and single quotes"] =
                "<xs:element name='Address' sawsdl:modelReference='urn:x' xml:lang=\"en\"/>",

            ["whitespace inside the tag and around the equals sign"] =
                "<xs:element\n    name  =  \"Address\"\n    type='xs:string'\n/>",

            ["nested elements with annotation text"] =
                "<xs:annotation>\n  <xs:documentation>Text with a &amp; and a &#38; in it."
                + "</xs:documentation>\n</xs:annotation>",

            ["a CDATA section"] =
                "<xs:documentation><![CDATA[ raw <not markup> & ampersand ]]></xs:documentation>",

            ["a processing instruction"] =
                "<?xml-stylesheet href=\"x.xsl\" type=\"text/xsl\"?>\n<xs:schema/>",

            // 0005: width is UTF-16 code units, so a CRLF is 2 and a non-BMP character is 2.
            // A fixture in ASCII with LF endings could not tell a code-unit bug from a
            // byte-count bug or a scalar-value bug, which is exactly the failure mode 0005
            // calls the worst available.
            ["CRLF endings, non-ASCII and a non-BMP character"] =
                "<xs:documentation>\r\n  café — naïve \U0001F600 \r\n</xs:documentation>\r\n",

            ["mixed content and an internal comment"] =
                "<a>before<b/>between<!-- why -->after</a>",

            ["trailing whitespace after the root"] = "<xs:schema/>\n\n  \n",

            // 0005 puts DTDs outside the lexer's scope, which is not the same as calling
            // them malformed: the declaration is well-formed XML and is preserved as one
            // opaque token. The internal subset matters because a '>' inside it must not
            // end the declaration early.
            ["a document type declaration with an internal subset"] =
                "<!DOCTYPE a [ <!ENTITY x \"y\"> ]>\n<a/>",
        };

    /// <summary>
    /// Buffers that are not well-formed, keyed by what each is for.
    /// </summary>
    /// <remarks>
    /// <c>XE-031</c> requires a best-effort partial render of these rather than a cleared
    /// canvas, so the tree must still cover every character and the parser must still
    /// terminate. These are the cases a valid corpus can never provide.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> Malformed { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["an element that is never closed"] = "<xs:schema><xs:element name=\"A\">",
            ["an end tag that closes the wrong element"] = "<a><b></a></b>",
            ["an end tag for an element never opened"] = "<a></b></a>",
            ["an unterminated comment"] = "<a><!-- runs off the end",
            ["a comment whose delimiters overlap"] = "<a><!--></a>",
            ["an unterminated CDATA section"] = "<a><![CDATA[ off the end",
            ["an unterminated processing instruction"] = "<a><?target off the end",
            ["an attribute value with no closing quote"] = "<a name=\"unterminated>\n<b/>",
            ["an attribute with no value"] = "<a name></a>",
            ["an attribute with no equals sign"] = "<a name \"value\"></a>",
            ["a tag with no closing angle bracket"] = "<a name=\"v\"\n<b/>",
            ["a bare less-than in content"] = "<a>1 < 2</a>",

            // XE-070. A raw & is a well-formedness error wherever it appears — XmlReader
            // rejects it, so the document cannot be validated — and there is no exception
            // for annotation text. The editor still opens these (XE-031) and still saves
            // them (XE-057); it just does not pretend they are fine.
            ["a raw ampersand in ordinary element text"] = "<xs:element>Tom & Jerry</xs:element>",
            ["a raw ampersand in an attribute value"] = "<a name=\"Tom & Jerry\"/>",
            ["a raw ampersand in documentation text"] =
                "<xs:annotation>\n  <xs:documentation>Tom & Jerry, R&D, AT&T</xs:documentation>\n"
                + "</xs:annotation>",
            ["a raw ampersand in appinfo"] =
                "<xs:annotation><xs:appinfo>k=1 & v=2</xs:appinfo></xs:annotation>",
            ["a raw ampersand below documentation, beside a real reference"] =
                "<xs:documentation><b>a & b</b> and &amp; and &notdeclared; too</xs:documentation>",
            ["a raw ampersand in an attribute inside documentation"] =
                "<xs:documentation><b title=\"R&D\">text</b></xs:documentation>",
            ["an entity that is not predefined, outside annotation text"] =
                "<a>&notdeclared;</a>",
            ["an unterminated numeric reference"] = "<a>&#x20 missing the semicolon</a>",
            ["a stray ampersand and junk before the root"] = "junk & more\n<a/>",
            ["nothing but junk"] = "not xml at all",
            ["a lone opening angle bracket"] = "<",
        };

    /// <summary>Every fixture, well-formed and malformed alike.</summary>
    public static IReadOnlyDictionary<string, string> All { get; } =
        WellFormed.Concat(Malformed).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

    /// <summary>Every fixture, as xUnit theory data of (name, source).</summary>
    /// <returns>One row per fixture.</returns>
    public static TheoryData<string, string> AllRows() => Rows(All);

    /// <summary>The well-formed fixtures, as xUnit theory data of (name, source).</summary>
    /// <returns>One row per well-formed fixture.</returns>
    public static TheoryData<string, string> WellFormedRows() => Rows(WellFormed);

    /// <summary>The malformed fixtures, as xUnit theory data of (name, source).</summary>
    /// <returns>One row per malformed fixture.</returns>
    public static TheoryData<string, string> MalformedRows() => Rows(Malformed);

    private static TheoryData<string, string> Rows(IReadOnlyDictionary<string, string> source)
    {
        var data = new TheoryData<string, string>();
        foreach (var entry in source)
        {
            data.Add(entry.Key, entry.Value);
        }

        return data;
    }
}
