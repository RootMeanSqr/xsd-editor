using XsdEditor.Core.Syntax.Green;

namespace XsdEditor.Core.Syntax;

/// <summary>
/// The purpose-built XML lexer <c>0005</c> chose over <see cref="System.Xml.XmlReader"/>.
/// </summary>
/// <remarks>
/// <para>
/// It exposes the lexical grammar — scan a name, scan an attribute value, scan character
/// data — rather than a token stream, and the parser sequences those calls. XML is
/// context-sensitive at the lexical level (a <c>&lt;</c> inside an attribute value is not
/// markup, whitespace inside a tag is not character data), so a single flat token stream
/// would need the parser to re-lex or the lexer to duplicate the parser's state. One
/// scanner, driven by the parser, is what keeps the two from disagreeing about where they
/// are.
/// </para>
/// <para>
/// Every scan method returns a token whose text is exactly the characters consumed, and
/// advances <see cref="Position"/> by exactly that token's width. Together those two
/// facts are what make the width-sum invariant hold: no scan can drop a character or
/// claim one twice.
/// </para>
/// <para>
/// Scope is what makes this affordable (<c>0005</c>): this is not a general XML processor.
/// No DTD internal subsets are interpreted, no entity declarations are expanded, and no
/// reference is ever resolved — <c>&amp;#x20;</c> stays six characters of text so that
/// <c>XE-069</c> holds by construction.
/// </para>
/// </remarks>
internal sealed class XmlLexer
{
    private readonly string _text;

    /// <summary>Creates a lexer over a source buffer.</summary>
    /// <param name="text">The buffer to scan. Already ampersand-preprocessed, if at all.</param>
    public XmlLexer(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
    }

    /// <summary>The index the next scan starts at.</summary>
    public int Position { get; private set; }

    /// <summary>Whether the whole buffer has been consumed.</summary>
    public bool AtEnd => Position >= _text.Length;

    /// <summary>Returns the character at an offset from the current position.</summary>
    /// <param name="offset">Offset from <see cref="Position"/>. Zero is the next character.</param>
    /// <returns>The character, or <c>'\0'</c> past the end of the buffer.</returns>
    public char Peek(int offset = 0)
    {
        var index = Position + offset;
        return index < _text.Length ? _text[index] : '\0';
    }

    /// <summary>Whether the buffer continues with the given text at the current position.</summary>
    /// <param name="value">The text to look for.</param>
    /// <returns><see langword="true"/> if the buffer matches at <see cref="Position"/>.</returns>
    public bool StartsWith(string value) =>
        string.CompareOrdinal(_text, Position, value, 0, value.Length) == 0
        && Position + value.Length <= _text.Length;

    /// <summary>Whether a name can start at an offset from the current position.</summary>
    /// <param name="offset">Offset from <see cref="Position"/>. Zero is the next character.</param>
    /// <returns><see langword="true"/> if an XML name-start character sits there.</returns>
    public bool AtNameStart(int offset = 0)
    {
        var index = Position + offset;
        return index < _text.Length && IsNameStartChar(_text, index);
    }

    /// <summary>
    /// Reads the name at an offset from the current position without consuming anything.
    /// </summary>
    /// <param name="offset">Offset from <see cref="Position"/>.</param>
    /// <returns>The name, or an empty string if none starts there.</returns>
    /// <remarks>
    /// The parser needs this to decide whose end tag <c>&lt;/name&gt;</c> is before
    /// committing to consuming it — an end tag naming an ancestor has to be left for that
    /// ancestor rather than eaten here.
    /// </remarks>
    public string PeekName(int offset)
    {
        var index = Position + offset;
        if (index >= _text.Length || !IsNameStartChar(_text, index))
        {
            return string.Empty;
        }

        var start = index;
        index += CharWidthAt(index);
        while (index < _text.Length && IsNameChar(_text, index))
        {
            index += CharWidthAt(index);
        }

        return _text[start..index];
    }

    /// <summary>Whether the next character is XML whitespace.</summary>
    /// <returns><see langword="true"/> for space, tab, carriage return or line feed.</returns>
    public bool AtWhitespace() => !AtEnd && IsWhitespace(_text[Position]);

    /// <summary>Scans a run of XML whitespace.</summary>
    /// <returns>A <see cref="SyntaxKind.WhitespaceToken"/> covering the run.</returns>
    public GreenToken LexWhitespace()
    {
        var start = Position;
        while (!AtEnd && IsWhitespace(_text[Position]))
        {
            Position++;
        }

        return Token(SyntaxKind.WhitespaceToken, start);
    }

    /// <summary>Scans an XML name, prefix included.</summary>
    /// <returns>A <see cref="SyntaxKind.NameToken"/> covering the name.</returns>
    public GreenToken LexName()
    {
        var start = Position;
        if (AtNameStart())
        {
            Position += CharWidthAt(Position);
            while (!AtEnd && IsNameChar(_text, Position))
            {
                Position += CharWidthAt(Position);
            }
        }

        return Token(SyntaxKind.NameToken, start);
    }

    /// <summary>
    /// Scans a quoted attribute value, <em>including</em> its quotes.
    /// </summary>
    /// <returns>An <see cref="SyntaxKind.AttributeValueToken"/> covering quotes and content.</returns>
    /// <remarks>
    /// <para>
    /// The value is taken verbatim: no attribute-value normalisation, and no reference
    /// resolution. Those two behaviours of <see cref="System.Xml.XmlReader"/> are precisely
    /// why <c>0005</c> replaced it.
    /// </para>
    /// <para>
    /// An unterminated value runs to the next <c>&lt;</c> rather than to the end of the
    /// file, so one missing quote costs one tag rather than the rest of the document
    /// (<c>XE-031</c>). <c>&lt;</c> is the right bound because XML forbids it inside an
    /// attribute value, so it cannot appear in a well-formed one. **A newline is not a
    /// bound**: a wrapped value such as a multi-line <c>xsi:schemaLocation</c> is perfectly
    /// legal, and stopping at the line end would truncate a valid document — corrupting
    /// correct input to improve recovery on incorrect input.
    /// </para>
    /// </remarks>
    public GreenToken LexAttributeValue()
    {
        var start = Position;
        var quote = Peek();
        if (quote is not ('"' or '\''))
        {
            return Token(SyntaxKind.AttributeValueToken, start);
        }

        Position++;
        while (!AtEnd && _text[Position] != quote && _text[Position] != '<')
        {
            Position++;
        }

        if (!AtEnd && _text[Position] == quote)
        {
            Position++;
        }

        return Token(SyntaxKind.AttributeValueToken, start);
    }

    /// <summary>Scans character data up to the next <c>&lt;</c> or the end of the buffer.</summary>
    /// <returns>A <see cref="SyntaxKind.TextToken"/> covering the run.</returns>
    public GreenToken LexText()
    {
        var start = Position;
        while (!AtEnd && _text[Position] != '<')
        {
            Position++;
        }

        return Token(SyntaxKind.TextToken, start);
    }

    /// <summary>Scans a comment, from <c>&lt;!--</c> through <c>--&gt;</c>.</summary>
    /// <returns>A <see cref="SyntaxKind.CommentToken"/> covering the whole comment.</returns>
    public GreenToken LexComment() => LexDelimited(SyntaxKind.CommentToken, "<!--", "-->");

    /// <summary>Scans a processing instruction or the XML declaration.</summary>
    /// <returns>
    /// An <see cref="SyntaxKind.XmlDeclarationToken"/> when the target is <c>xml</c>, and a
    /// <see cref="SyntaxKind.ProcessingInstructionToken"/> otherwise.
    /// </returns>
    public GreenToken LexProcessingInstruction()
    {
        var isDeclaration = StartsWith("<?xml")
            && (Position + 5 >= _text.Length || !IsNameChar(_text, Position + 5));

        var kind = isDeclaration
            ? SyntaxKind.XmlDeclarationToken
            : SyntaxKind.ProcessingInstructionToken;

        return LexDelimited(kind, "<?", "?>");
    }

    /// <summary>Scans a CDATA section, from <c>&lt;![CDATA[</c> through <c>]]&gt;</c>.</summary>
    /// <returns>A <see cref="SyntaxKind.CdataSectionToken"/> covering the whole section.</returns>
    public GreenToken LexCdataSection() =>
        LexDelimited(SyntaxKind.CdataSectionToken, "<![CDATA[", "]]>");

    /// <summary>
    /// Scans a document type declaration verbatim, without interpreting it.
    /// </summary>
    /// <param name="terminated">
    /// Set to <see langword="true"/> when the declaration's own closing <c>&gt;</c> was
    /// found. It has to be reported rather than inferred from the token's last character:
    /// an internal subset ends with <c>&gt;</c> of its own, so an unterminated
    /// <c>&lt;!DOCTYPE a [ &lt;!ENTITY x "y"&gt;</c> looks closed to any suffix test.
    /// </param>
    /// <returns>A <see cref="SyntaxKind.DocumentTypeToken"/> covering the declaration.</returns>
    /// <remarks>
    /// <c>0005</c> puts DTDs outside this lexer's scope, so the declaration is preserved as
    /// one opaque token rather than modelled. An internal subset is skipped by matching its
    /// brackets, so that a <c>&gt;</c> inside it does not end the declaration early.
    /// </remarks>
    public GreenToken LexDocumentType(out bool terminated)
    {
        var start = Position;
        Position += "<!DOCTYPE".Length;

        terminated = false;
        var depth = 0;
        while (!AtEnd)
        {
            var current = _text[Position];
            if (current == '[')
            {
                depth++;
            }
            else if (current == ']')
            {
                depth--;
            }
            else if (current == '>' && depth <= 0)
            {
                Position++;
                terminated = true;
                break;
            }

            Position++;
        }

        return Token(SyntaxKind.DocumentTypeToken, start);
    }

    /// <summary>
    /// Consumes a run the parser could not interpret, resynchronising at the next plausible
    /// tag start.
    /// </summary>
    /// <returns>A <see cref="SyntaxKind.GapToken"/> covering the skipped run.</returns>
    /// <remarks>
    /// This is <c>0005</c>'s recovery story and what <c>XE-031</c>'s best-effort partial
    /// render is built on: the unparsed region is identified positionally rather than
    /// discarded, so Design View can mark it instead of clearing the canvas.
    /// </remarks>
    public GreenToken LexGap()
    {
        var start = Position;

        // Always consume at least one character, or recovery cannot make progress.
        if (!AtEnd)
        {
            Position++;
        }

        while (!AtEnd && _text[Position] != '<')
        {
            Position++;
        }

        return Token(SyntaxKind.GapToken, start);
    }

    /// <summary>
    /// Consumes a run inside a tag that could not be interpreted, stopping at anything that
    /// could resume the parse.
    /// </summary>
    /// <returns>A <see cref="SyntaxKind.GapToken"/> covering the skipped run.</returns>
    /// <remarks>
    /// Bounded differently from <see cref="LexGap"/>: inside a tag, whitespace and the tag's
    /// own closers are recovery points, so one unrecognised character does not cost the rest
    /// of the element.
    /// </remarks>
    public GreenToken LexGapInTag()
    {
        var start = Position;

        if (!AtEnd)
        {
            Position++;
        }

        while (!AtEnd)
        {
            var current = _text[Position];
            if (IsWhitespace(current) || current is '<' or '>' || StartsWith("/>"))
            {
                break;
            }

            Position++;
        }

        return Token(SyntaxKind.GapToken, start);
    }

    /// <summary>Produces the zero-width token that ends every tree.</summary>
    /// <returns>An <see cref="SyntaxKind.EndOfFileToken"/> of width zero.</returns>
    public static GreenToken EndOfFile() => new(SyntaxKind.EndOfFileToken, string.Empty);

    /// <summary>Consumes a fixed piece of punctuation the parser has already recognised.</summary>
    /// <param name="kind">The token kind to produce.</param>
    /// <param name="text">The exact characters to consume.</param>
    /// <returns>A token covering those characters.</returns>
    public GreenToken LexPunctuation(SyntaxKind kind, string text)
    {
        var start = Position;
        Position += text.Length;
        return Token(kind, start);
    }

    private GreenToken LexDelimited(SyntaxKind kind, string opening, string closing)
    {
        var start = Position;
        Position += opening.Length;

        while (!AtEnd && !StartsWith(closing))
        {
            Position++;
        }

        // An unterminated construct runs to the end of the buffer. The parser records the
        // diagnostic; the token still covers every character, so the tree stays lossless
        // even for a document that is not well-formed.
        Position = AtEnd ? _text.Length : Position + closing.Length;

        return Token(kind, start);
    }

    private GreenToken Token(SyntaxKind kind, int start) =>
        new(kind, _text[start..Position]);

    private int CharWidthAt(int index) =>
        char.IsHighSurrogate(_text[index]) && index + 1 < _text.Length
        && char.IsLowSurrogate(_text[index + 1])
            ? 2
            : 1;

    private static bool IsWhitespace(char value) =>
        value is ' ' or '\t' or '\r' or '\n';

    private static bool IsNameStartChar(string text, int index)
    {
        var value = text[index];
        if (char.IsHighSurrogate(value))
        {
            // XML 1.0 5th edition allows [#x10000-#xEFFFF] to start a name; everything a
            // surrogate pair can encode below #xF0000 is in that range.
            return index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]);
        }

        return value is ':' or '_'
            or >= 'A' and <= 'Z'
            or >= 'a' and <= 'z'
            or >= 'À' and <= 'Ö'
            or >= 'Ø' and <= 'ö'
            or >= 'ø' and <= '˿'
            or >= 'Ͱ' and <= 'ͽ'
            or >= 'Ϳ' and <= '῿'
            or >= '‌' and <= '‍'
            or >= '⁰' and <= '↏'
            or >= 'Ⰰ' and <= '⿯'
            or >= '、' and <= '퟿'
            or >= '豈' and <= '﷏'
            or >= 'ﷰ' and <= '�';
    }

    private static bool IsNameChar(string text, int index)
    {
        if (IsNameStartChar(text, index))
        {
            return true;
        }

        return text[index] is '-' or '.' or '·'
            or >= '0' and <= '9'
            or >= '̀' and <= 'ͯ'
            or >= '‿' and <= '⁀';
    }
}
