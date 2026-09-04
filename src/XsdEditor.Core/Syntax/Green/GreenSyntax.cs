using System.Text;

namespace XsdEditor.Core.Syntax.Green;

/// <summary>
/// A non-terminal in the green layer: a node whose width is the sum of its children's.
/// </summary>
/// <remarks>
/// The width is computed from the children rather than supplied, so the invariant
/// <c>0005</c> names — a node's width equals the sum of its children's widths — holds by
/// construction here and is asserted over parsed trees by the syntax invariant tests. It
/// is the statement that no character is claimed twice and none is dropped, which is what
/// makes "lossless" falsifiable.
/// </remarks>
internal sealed class GreenSyntax : GreenNode
{
    private readonly GreenNode[] _children;

    /// <summary>Creates a non-terminal from its children.</summary>
    /// <param name="kind">The node's kind.</param>
    /// <param name="children">The children, in source order. Taken as given, not copied.</param>
    public GreenSyntax(SyntaxKind kind, GreenNode[] children)
        : base(kind, SumWidths(children))
    {
        _children = children;
    }

    /// <inheritdoc/>
    public override int SlotCount => _children.Length;

    /// <inheritdoc/>
    public override GreenNode GetSlot(int index) => _children[index];

    /// <inheritdoc/>
    public override void WriteTo(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        foreach (var child in _children)
        {
            child.WriteTo(builder);
        }
    }

    private static int SumWidths(GreenNode[] children)
    {
        ArgumentNullException.ThrowIfNull(children);

        var total = 0;
        foreach (var child in children)
        {
            total += child.Width;
        }

        return total;
    }
}
