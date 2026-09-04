namespace XsdEditor.Core.Syntax;

/// <summary>
/// The kind of a node or token in the lossless syntax tree.
/// </summary>
/// <remarks>
/// The set covers the well-formed-XML subset that XSD 1.0 uses, which is the scope
/// <c>0005</c> fixes for the lexer: no DTDs, no entity declarations, no XInclude. Anything
/// outside it is preserved verbatim rather than modelled — a document type declaration as
/// <see cref="DocumentTypeToken"/>, an unrecognised run as <see cref="GapToken"/>.
/// </remarks>
public enum SyntaxKind
{
    /// <summary>Absent. Not produced by the parser.</summary>
    None = 0,

    // ---- Tokens -------------------------------------------------------------------

    /// <summary><c>&lt;</c>, opening a start or empty-element tag.</summary>
    LessThanToken,

    /// <summary><c>&lt;/</c>, opening an end tag.</summary>
    LessThanSlashToken,

    /// <summary><c>&gt;</c>, closing a start or end tag.</summary>
    GreaterThanToken,

    /// <summary><c>/&gt;</c>, closing an empty-element tag.</summary>
    SlashGreaterThanToken,

    /// <summary><c>=</c>, between an attribute name and its value.</summary>
    EqualsToken,

    /// <summary>An XML name, possibly with a namespace prefix.</summary>
    NameToken,

    /// <summary>
    /// An attribute value <em>including its surrounding quotes</em>, verbatim.
    /// </summary>
    /// <remarks>
    /// The quotes are part of the token because <c>XE-068</c> requires byte-for-byte
    /// preservation and the source's choice of <c>'</c> or <c>"</c> is part of those bytes.
    /// The text is never normalised and references inside it are never resolved
    /// (<c>XE-069</c>).
    /// </remarks>
    AttributeValueToken,

    /// <summary>Whitespace between markup, or inside a tag between its parts.</summary>
    WhitespaceToken,

    /// <summary>
    /// Character data, verbatim, with character and entity references left in their
    /// original spelling (<c>XE-069</c>).
    /// </summary>
    TextToken,

    /// <summary>A complete comment, from <c>&lt;!--</c> to <c>--&gt;</c> (<c>XE-067</c>).</summary>
    CommentToken,

    /// <summary>A complete processing instruction, from <c>&lt;?</c> to <c>?&gt;</c>.</summary>
    ProcessingInstructionToken,

    /// <summary>The XML declaration: a processing instruction whose target is <c>xml</c>.</summary>
    XmlDeclarationToken,

    /// <summary>A complete CDATA section, from <c>&lt;![CDATA[</c> to <c>]]&gt;</c>.</summary>
    CdataSectionToken,

    /// <summary>
    /// A document type declaration, preserved verbatim but not parsed — <c>0005</c> puts
    /// DTDs outside the lexer's scope.
    /// </summary>
    DocumentTypeToken,

    /// <summary>
    /// A run of source the parser could not interpret, recorded positionally so that
    /// <c>XE-031</c>'s best-effort partial render can mark it.
    /// </summary>
    GapToken,

    /// <summary>The zero-width token at the end of the buffer.</summary>
    EndOfFileToken,

    // ---- Nodes --------------------------------------------------------------------

    /// <summary>The whole document. The root of every tree.</summary>
    Document,

    /// <summary>An element: a start tag, its content and its end tag, or an empty-element tag.</summary>
    Element,

    /// <summary>A start tag, <c>&lt;name …&gt;</c>.</summary>
    StartTag,

    /// <summary>An empty-element tag, <c>&lt;name …/&gt;</c>.</summary>
    EmptyElementTag,

    /// <summary>An end tag, <c>&lt;/name&gt;</c>.</summary>
    EndTag,

    /// <summary>An attribute: a name, <c>=</c>, and a quoted value.</summary>
    XmlAttribute,
}
