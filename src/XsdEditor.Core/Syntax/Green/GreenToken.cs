using System.Text;

namespace XsdEditor.Core.Syntax.Green;

/// <summary>
/// A terminal in the green layer: a run of source text with no children.
/// </summary>
/// <remarks>
/// The token carries its text verbatim. Nothing is normalised, resolved or trimmed on the
/// way in, which is what makes serialising an unmodified node a copy of its original
/// characters rather than a re-rendering of them (<c>XE-067</c>–<c>XE-069</c>).
/// </remarks>
internal sealed class GreenToken : GreenNode
{
    /// <summary>Creates a token.</summary>
    /// <param name="kind">The token's kind.</param>
    /// <param name="text">The exact source text, which fixes the token's width.</param>
    public GreenToken(SyntaxKind kind, string text)
        : base(kind, (text ?? throw new ArgumentNullException(nameof(text))).Length)
    {
        Text = text;
    }

    /// <summary>The exact source text this token covers.</summary>
    public string Text { get; }

    /// <inheritdoc/>
    public override int SlotCount => 0;

    /// <inheritdoc/>
    public override GreenNode GetSlot(int index) =>
        throw new ArgumentOutOfRangeException(nameof(index), index, "A token has no children.");

    /// <inheritdoc/>
    public override void WriteTo(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Append(Text);
    }
}
