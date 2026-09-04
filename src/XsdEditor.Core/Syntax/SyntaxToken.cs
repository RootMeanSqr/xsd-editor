using XsdEditor.Core.Syntax.Green;

namespace XsdEditor.Core.Syntax;

/// <summary>
/// A terminal in the red layer: a token, with the exact text it covers.
/// </summary>
public sealed class SyntaxToken : SyntaxNode
{
    internal SyntaxToken(GreenToken green, SyntaxNode? parent, int position)
        : base(green, parent, position)
    {
        Text = green.Text;
    }

    /// <summary>The exact source text this token covers, unnormalised.</summary>
    public string Text { get; }

    /// <inheritdoc/>
    public override string ToString() => $"{Kind} [{Span.Start}..{Span.End}) {Text}";
}
