namespace XsdEditor.Core.Syntax;

/// <summary>
/// What went wrong at one place in the source, and where.
/// </summary>
/// <remarks>
/// A diagnostic never stops the parse. <c>XE-031</c> requires a best-effort partial render
/// of a buffer that is not well-formed, so the parser records what it could not interpret
/// and carries on; the tree it produces still covers every character of the input.
/// </remarks>
/// <param name="Code">Which problem this is.</param>
/// <param name="Span">The extent of source the problem covers.</param>
/// <param name="Message">A description suitable for showing to a person.</param>
public sealed record SyntaxDiagnostic(SyntaxDiagnosticCode Code, SourceSpan Span, string Message)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Code} at [{Span.Start}..{Span.End}): {Message}";
}

/// <summary>The problems the syntax layer reports.</summary>
public enum SyntaxDiagnosticCode
{
    /// <summary>Absent. Not produced by the parser.</summary>
    None = 0,

    /// <summary>A run of source that could not be interpreted as markup or character data.</summary>
    UnexpectedText,

    /// <summary>An element's start tag has no matching end tag.</summary>
    UnclosedElement,

    /// <summary>An end tag names an element that is not the innermost open one.</summary>
    MismatchedEndTag,

    /// <summary>An end tag closes an element that was never opened.</summary>
    UnexpectedEndTag,

    /// <summary>A comment, processing instruction or CDATA section runs to the end of the buffer.</summary>
    UnterminatedConstruct,

    /// <summary>A tag is missing its closing <c>&gt;</c> or <c>/&gt;</c>.</summary>
    UnclosedTag,

    /// <summary>An attribute has a name but no <c>=</c> and value.</summary>
    MalformedAttribute,

    /// <summary>An attribute value is missing its closing quote.</summary>
    UnterminatedAttributeValue,
}
