using System.Xml;
using XsdEditor.Core.Syntax;

namespace XsdEditor.Core.Tests.Syntax;

/// <summary>
/// Pins our verdict on ampersands and references to <see cref="XmlReader"/>'s.
/// </summary>
/// <remarks>
/// <para>
/// <c>XE-070</c>'s rule is "if a conforming reader rejects it, so do we". That is a claim
/// about agreement with another implementation, so it is tested against that implementation
/// rather than restated as a set of hand-written expectations that could drift from it.
/// </para>
/// <para>
/// <strong>Scoped to ampersands and references on purpose.</strong> A blanket parity suite
/// would fail on the places we are deliberately different: <c>0005</c> preserves a
/// <c>&lt;!DOCTYPE&gt;</c> verbatim where <see cref="XmlReader"/> rejects one under its
/// default <see cref="DtdProcessing.Prohibit"/>, and every <c>XE-031</c> recovery case
/// produces a tree here and an exception there. Those differences are the design; this one
/// is not.
/// </para>
/// </remarks>
public class XmlReaderParityTests
{
    [Theory]
    // The xs: prefix is declared wherever it appears. Without that, XmlReader rejects the
    // input for an undeclared prefix rather than for its ampersand, and the test would
    // agree with it for the wrong reason — which is exactly what it did on first run.
    //
    // Raw ampersands, in each of the places the leniency used to reach.
    [InlineData("<a>Tom & Jerry</a>")]
    [InlineData("<xs:documentation xmlns:xs=\"http://www.w3.org/2001/XMLSchema\">R&D</xs:documentation>")]
    [InlineData("<xs:annotation xmlns:xs=\"http://www.w3.org/2001/XMLSchema\"><xs:appinfo>k=1 & v=2</xs:appinfo></xs:annotation>")]
    [InlineData("<a name=\"Tom & Jerry\"/>")]
    // Things shaped like a reference that are not one.
    [InlineData("<a>&notdeclared;</a>")]
    [InlineData("<a>&#x20 missing the semicolon</a>")]
    [InlineData("<a>&;</a>")]
    [InlineData("<a>&</a>")]
    // References to code points XML forbids.
    [InlineData("<a>&#0;</a>")]
    [InlineData("<a>&#xD800;</a>")]
    [InlineData("<a>&#x110000;</a>")]
    // The valid side, which must stay valid: XE-069's preserved references.
    [InlineData("<a>&amp;&lt;&gt;&quot;&apos;</a>")]
    [InlineData("<xs:pattern xmlns:xs=\"http://www.w3.org/2001/XMLSchema\" value=\"[A-Z&#x20;a-z&#x7E;]\"/>")]
    [InlineData("<a>&#38;&#x9;&#xA;&#xD;&#xD7FF;&#xE000;&#xFFFD;&#x10000;&#x10FFFF;</a>")]
    // An ampersand inside CDATA is not a reference and not an error, for either reader.
    [InlineData("<a><![CDATA[Tom & Jerry]]></a>")]
    public void We_agree_with_XmlReader_on_whether_the_document_is_well_formed(string source)
    {
        var readerAccepts = XmlReaderAccepts(source);
        var tree = SyntaxTree.Parse(source);

        Assert.True(
            readerAccepts == tree.IsWellFormed,
            readerAccepts
                ? $"XmlReader accepts this and we report: {string.Join("; ", tree.Diagnostics)}"
                : "XmlReader rejects this and we report nothing.");
    }

    private static bool XmlReaderAccepts(string source)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(source));
            while (reader.Read())
            {
                // Reading to the end is what surfaces a malformed reference: the exception
                // is raised where it is met, not when the reader is created.
            }

            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
