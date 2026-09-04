using XsdEditor.Core.Syntax.Green;

namespace XsdEditor.Core.Syntax;

/// <summary>
/// Builds the green tree from source text, recovering rather than throwing.
/// </summary>
/// <remarks>
/// <para>
/// The parser sequences the lexer's scans and never inspects the buffer itself, so there is
/// one idea of where the scan is rather than two that can drift apart.
/// </para>
/// <para>
/// <strong>Every character of the input ends up in exactly one token.</strong> That is not
/// an aspiration: anything the parser cannot interpret becomes a <see
/// cref="SyntaxKind.GapToken"/> covering it, so the width-sum invariant holds for malformed
/// input as well as well-formed input, and <c>XE-031</c>'s partial render has the positions
/// it needs to mark the unparsed regions.
/// </para>
/// </remarks>
internal sealed class XmlParser
{
    private readonly XmlLexer _lexer;
    private readonly List<SyntaxDiagnostic> _diagnostics = [];
    private readonly List<string> _openElements = [];

    /// <summary>Creates a parser over a source buffer.</summary>
    /// <param name="text">The buffer to parse.</param>
    public XmlParser(string text) => _lexer = new XmlLexer(text);

    /// <summary>What the parser could not interpret, in the order it was met.</summary>
    public IReadOnlyList<SyntaxDiagnostic> Diagnostics => _diagnostics;

    /// <summary>Parses the whole buffer.</summary>
    /// <returns>The green <see cref="SyntaxKind.Document"/> node covering every character.</returns>
    public GreenNode ParseDocument()
    {
        var children = new List<GreenNode>();
        ParseContent(children);
        children.Add(XmlLexer.EndOfFile());
        return new GreenSyntax(SyntaxKind.Document, [.. children]);
    }

    /// <summary>
    /// Parses content until the buffer ends or an end tag arrives that this level cannot
    /// consume.
    /// </summary>
    private void ParseContent(List<GreenNode> children)
    {
        while (!_lexer.AtEnd)
        {
            if (_lexer.Peek() != '<')
            {
                var start = _lexer.Position;
                var text = _lexer.LexText();
                children.Add(text);

                // Only whitespace may sit outside the root element. Non-whitespace there is
                // a well-formedness error, and XE-031 wants it marked rather than quietly
                // rendered as if it were content.
                if (_openElements.Count == 0 && !string.IsNullOrWhiteSpace(text.Text))
                {
                    Report(
                        SyntaxDiagnosticCode.UnexpectedText,
                        SourceSpan.FromBounds(start, _lexer.Position),
                        "Character data outside the root element.");
                }

                continue;
            }

            if (_lexer.StartsWith("</"))
            {
                // An end tag belongs to whichever open element it names. If some open
                // element is waiting for it, stop and let that element consume it. If
                // nothing opened it, consume it here as an error — leaving it for a caller
                // that may not exist is how it ends up in no node at all.
                var closingName = _lexer.PeekName("</".Length);
                if (_openElements.Contains(closingName, StringComparer.Ordinal))
                {
                    return;
                }

                children.Add(ParseEndTag(
                    SyntaxDiagnosticCode.UnexpectedEndTag,
                    $"</{closingName}> closes an element that was never opened."));
                continue;
            }

            if (_lexer.StartsWith("<!--"))
            {
                children.Add(LexAndCheckTerminated(_lexer.LexComment, "<!--", "-->"));
            }
            else if (_lexer.StartsWith("<![CDATA["))
            {
                children.Add(LexAndCheckTerminated(_lexer.LexCdataSection, "<![CDATA[", "]]>"));
            }
            else if (_lexer.StartsWith("<!DOCTYPE"))
            {
                children.Add(_lexer.LexDocumentType());
            }
            else if (_lexer.StartsWith("<?"))
            {
                children.Add(LexAndCheckTerminated(_lexer.LexProcessingInstruction, "<?", "?>"));
            }
            else if (IsElementStart())
            {
                children.Add(ParseElement());
            }
            else
            {
                children.Add(Gap(SyntaxDiagnosticCode.UnexpectedText, "Not markup or character data."));
            }
        }
    }

    private bool IsElementStart()
    {
        if (_lexer.Peek() != '<')
        {
            return false;
        }

        return _lexer.AtNameStart(1);
    }

    private GreenSyntax ParseElement()
    {
        var start = _lexer.Position;
        var tagChildren = new List<GreenNode>();

        tagChildren.Add(_lexer.LexPunctuation(SyntaxKind.LessThanToken, "<"));
        var name = _lexer.LexName();
        tagChildren.Add(name);

        ParseAttributes(tagChildren);

        if (_lexer.StartsWith("/>"))
        {
            tagChildren.Add(_lexer.LexPunctuation(SyntaxKind.SlashGreaterThanToken, "/>"));
            var emptyTag = new GreenSyntax(SyntaxKind.EmptyElementTag, [.. tagChildren]);
            return new GreenSyntax(SyntaxKind.Element, [emptyTag]);
        }

        if (_lexer.Peek() == '>')
        {
            tagChildren.Add(_lexer.LexPunctuation(SyntaxKind.GreaterThanToken, ">"));
        }
        else
        {
            Report(
                SyntaxDiagnosticCode.UnclosedTag,
                SourceSpan.FromBounds(start, _lexer.Position),
                $"The tag <{name.Text} is missing its '>' or '/>'.");
        }

        var startTag = new GreenSyntax(SyntaxKind.StartTag, [.. tagChildren]);

        var elementChildren = new List<GreenNode> { startTag };
        _openElements.Add(name.Text);
        try
        {
            ParseContent(elementChildren);
            ParseEndTagIfPresent(elementChildren, name.Text, start);
        }
        finally
        {
            _openElements.RemoveAt(_openElements.Count - 1);
        }

        return new GreenSyntax(SyntaxKind.Element, [.. elementChildren]);
    }

    private void ParseEndTagIfPresent(List<GreenNode> elementChildren, string name, int elementStart)
    {
        if (!_lexer.StartsWith("</"))
        {
            Report(
                SyntaxDiagnosticCode.UnclosedElement,
                SourceSpan.FromBounds(elementStart, _lexer.Position),
                $"The element <{name}> has no end tag.");
            return;
        }

        var closingName = _lexer.PeekName("</".Length);
        if (closingName != name)
        {
            // ParseContent only stops on an end tag that some open element is waiting for,
            // so this one belongs to an ancestor. Leave it there and close this element
            // without an end tag.
            Report(
                SyntaxDiagnosticCode.MismatchedEndTag,
                SourceSpan.FromBounds(elementStart, _lexer.Position),
                $"The element <{name}> is closed by </{closingName}>.");
            return;
        }

        elementChildren.Add(ParseEndTag(SyntaxDiagnosticCode.None, message: null));
    }

    private GreenSyntax ParseEndTag(SyntaxDiagnosticCode code, string? message)
    {
        var start = _lexer.Position;
        var children = new List<GreenNode>
        {
            _lexer.LexPunctuation(SyntaxKind.LessThanSlashToken, "</"),
            _lexer.LexName(),
        };

        if (_lexer.AtWhitespace())
        {
            children.Add(_lexer.LexWhitespace());
        }

        if (_lexer.Peek() == '>')
        {
            children.Add(_lexer.LexPunctuation(SyntaxKind.GreaterThanToken, ">"));
        }

        if (code != SyntaxDiagnosticCode.None && message is not null)
        {
            Report(code, SourceSpan.FromBounds(start, _lexer.Position), message);
        }

        return new GreenSyntax(SyntaxKind.EndTag, [.. children]);
    }

    private void ParseAttributes(List<GreenNode> tagChildren)
    {
        while (!_lexer.AtEnd)
        {
            if (_lexer.AtWhitespace())
            {
                tagChildren.Add(_lexer.LexWhitespace());
                continue;
            }

            if (_lexer.Peek() is '>' or '<' || _lexer.StartsWith("/>"))
            {
                return;
            }

            if (_lexer.AtNameStart())
            {
                tagChildren.Add(ParseAttribute());
                continue;
            }

            tagChildren.Add(GapInTag(SyntaxDiagnosticCode.UnexpectedText, "Not an attribute."));
        }
    }

    private GreenSyntax ParseAttribute()
    {
        var start = _lexer.Position;
        var children = new List<GreenNode> { _lexer.LexName() };

        if (_lexer.AtWhitespace())
        {
            children.Add(_lexer.LexWhitespace());
        }

        if (_lexer.Peek() == '=')
        {
            children.Add(_lexer.LexPunctuation(SyntaxKind.EqualsToken, "="));

            if (_lexer.AtWhitespace())
            {
                children.Add(_lexer.LexWhitespace());
            }

            if (_lexer.Peek() is '"' or '\'')
            {
                var quote = _lexer.Peek();
                var value = _lexer.LexAttributeValue();
                children.Add(value);

                if (value.Text.Length < 2 || value.Text[^1] != quote)
                {
                    Report(
                        SyntaxDiagnosticCode.UnterminatedAttributeValue,
                        SourceSpan.FromBounds(start, _lexer.Position),
                        "The attribute value has no closing quote.");
                }
            }
            else
            {
                Report(
                    SyntaxDiagnosticCode.MalformedAttribute,
                    SourceSpan.FromBounds(start, _lexer.Position),
                    "The attribute has no quoted value.");
            }
        }
        else
        {
            Report(
                SyntaxDiagnosticCode.MalformedAttribute,
                SourceSpan.FromBounds(start, _lexer.Position),
                "The attribute has no '=' and value.");
        }

        return new GreenSyntax(SyntaxKind.XmlAttribute, [.. children]);
    }

    private GreenToken LexAndCheckTerminated(Func<GreenToken> lex, string opening, string closing)
    {
        var start = _lexer.Position;
        var token = lex();

        // Length matters as well as the suffix: "<!-->" ends with "-->" without ever having
        // closed, because the same characters are doing both jobs.
        var terminated = token.Text.Length >= opening.Length + closing.Length
            && token.Text.EndsWith(closing, StringComparison.Ordinal);

        if (!terminated)
        {
            Report(
                SyntaxDiagnosticCode.UnterminatedConstruct,
                SourceSpan.FromBounds(start, _lexer.Position),
                $"The construct is missing its closing '{closing}'.");
        }

        return token;
    }

    private GreenToken Gap(SyntaxDiagnosticCode code, string message)
    {
        var start = _lexer.Position;
        var token = _lexer.LexGap();
        Report(code, SourceSpan.FromBounds(start, _lexer.Position), message);
        return token;
    }

    private GreenToken GapInTag(SyntaxDiagnosticCode code, string message)
    {
        var start = _lexer.Position;
        var token = _lexer.LexGapInTag();
        Report(code, SourceSpan.FromBounds(start, _lexer.Position), message);
        return token;
    }

    private void Report(SyntaxDiagnosticCode code, SourceSpan span, string message) =>
        _diagnostics.Add(new SyntaxDiagnostic(code, span, message));
}
