using System.Text;

namespace XsdEditor.Core.Syntax.Green;

/// <summary>
/// A node in the immutable, position-free green layer of the syntax tree.
/// </summary>
/// <remarks>
/// <para>
/// A green node knows its <see cref="Width"/> — how many UTF-16 code units it and its
/// descendants cover — and never its absolute offset. That is what makes it shareable:
/// the same subtree may appear in many documents and at many versions, so an edit costs
/// O(depth) rather than O(file) and the undo stack holds versions rather than copies
/// (<c>0005</c>).
/// </para>
/// <para>
/// <strong>A green node must never learn its absolute position.</strong> The moment one
/// caches one, sharing is unsound. Absolute positions belong to the red layer, which
/// computes them on descent. This is why the whole green layer is internal: nothing above
/// the syntax layer sees it.
/// </para>
/// </remarks>
internal abstract class GreenNode
{
    /// <summary>Initialises a green node.</summary>
    /// <param name="kind">The node's kind.</param>
    /// <param name="width">Width in UTF-16 code units. Never negative.</param>
    protected GreenNode(SyntaxKind kind, int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        Kind = kind;
        Width = width;
    }

    /// <summary>The node's kind.</summary>
    public SyntaxKind Kind { get; }

    /// <summary>
    /// How many UTF-16 code units this node and its descendants cover.
    /// </summary>
    /// <remarks>
    /// Code units, not bytes and not Unicode scalar values: a CRLF is width 2, and a
    /// non-BMP character is width 2 because it is a surrogate pair. <c>0005</c> fixes this
    /// because mixing units produces spans that are wrong only in files containing
    /// non-ASCII, which is the worst failure mode available.
    /// </remarks>
    public int Width { get; }

    /// <summary>How many child slots this node has. Zero for a token.</summary>
    public abstract int SlotCount { get; }

    /// <summary>Returns the child in the given slot.</summary>
    /// <param name="index">Zero-based slot index, below <see cref="SlotCount"/>.</param>
    /// <returns>The child green node.</returns>
    public abstract GreenNode GetSlot(int index);

    /// <summary>Appends this node's original source text to <paramref name="builder"/>.</summary>
    /// <param name="builder">The destination.</param>
    public abstract void WriteTo(StringBuilder builder);

    /// <summary>Returns this node's original source text.</summary>
    /// <returns>The text this node was parsed from, byte for byte.</returns>
    public sealed override string ToString()
    {
        var builder = new StringBuilder(Width);
        WriteTo(builder);
        return builder.ToString();
    }
}
