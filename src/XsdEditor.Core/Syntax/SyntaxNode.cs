using System.Text;
using XsdEditor.Core.Syntax.Green;

namespace XsdEditor.Core.Syntax;

/// <summary>
/// A node in the red layer: a throwaway façade over a green node, carrying the parent link
/// and the absolute position the green layer deliberately does not hold.
/// </summary>
/// <remarks>
/// <para>
/// Red nodes are created on descent and discarded when the walk ends. They are not cached:
/// asking a node for its children twice produces two equal but distinct façades. That is
/// the point — caching them would keep the throwaway layer alive and start to defeat the
/// sharing the green layer exists for (<c>0005</c>).
/// </para>
/// <para>
/// Everything above the syntax layer — the schema model, the index, the command layer —
/// addresses the tree through red nodes, so the sharing discipline stays inside this
/// component rather than becoming a rule every contributor has to know.
/// </para>
/// </remarks>
public class SyntaxNode
{
    private readonly GreenNode _green;

    internal SyntaxNode(GreenNode green, SyntaxNode? parent, int position)
    {
        _green = green;
        Parent = parent;
        Position = position;
    }

    /// <summary>The node's kind.</summary>
    public SyntaxKind Kind => _green.Kind;

    /// <summary>The node containing this one, or <see langword="null"/> at the root.</summary>
    public SyntaxNode? Parent { get; }

    /// <summary>Absolute index of this node's first character in the source buffer.</summary>
    public int Position { get; }

    /// <summary>How many UTF-16 code units this node and its descendants cover.</summary>
    public int Width => _green.Width;

    /// <summary>The exact extent of source this node was parsed from, trivia included.</summary>
    public SourceSpan Span => new(Position, _green.Width);

    /// <summary>Whether this node is a token, and so has no children.</summary>
    public bool IsToken => _green.SlotCount == 0;

    internal GreenNode GreenNode => _green;

    /// <summary>Returns this node's children, in source order.</summary>
    /// <returns>
    /// Freshly created red façades. Each child's position is this node's position plus the
    /// widths of its preceding siblings, which is how absolute positions are recovered from
    /// a layer that stores none.
    /// </returns>
    public IEnumerable<SyntaxNode> ChildNodes()
    {
        var position = Position;
        for (var slot = 0; slot < _green.SlotCount; slot++)
        {
            var child = _green.GetSlot(slot);
            yield return Wrap(child, this, position);
            position += child.Width;
        }
    }

    /// <summary>Returns this node's descendants in document order, excluding itself.</summary>
    /// <returns>Every node beneath this one, parents before children.</returns>
    public IEnumerable<SyntaxNode> DescendantNodes()
    {
        foreach (var child in ChildNodes())
        {
            yield return child;
            foreach (var descendant in child.DescendantNodes())
            {
                yield return descendant;
            }
        }
    }

    /// <summary>Returns this node and its descendants in document order.</summary>
    /// <returns>This node, then everything beneath it.</returns>
    public IEnumerable<SyntaxNode> DescendantNodesAndSelf()
    {
        yield return this;
        foreach (var descendant in DescendantNodes())
        {
            yield return descendant;
        }
    }

    /// <summary>Returns the first child of the given kind, if there is one.</summary>
    /// <param name="kind">The kind to look for.</param>
    /// <returns>The first matching child, or <see langword="null"/>.</returns>
    public SyntaxNode? ChildOfKind(SyntaxKind kind)
    {
        foreach (var child in ChildNodes())
        {
            if (child.Kind == kind)
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the original source text this node covers, byte for byte.
    /// </summary>
    /// <returns>
    /// The text the node was parsed from. For an unmodified node this is a copy of its
    /// span rather than a re-rendering, which is what makes preservation
    /// (<c>XE-067</c>–<c>XE-069</c>) the default behaviour of the tree.
    /// </returns>
    public string ToFullString()
    {
        var builder = new StringBuilder(_green.Width);
        _green.WriteTo(builder);
        return builder.ToString();
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Kind} [{Span.Start}..{Span.End})";

    internal static SyntaxNode Wrap(GreenNode green, SyntaxNode? parent, int position) =>
        green is GreenToken token
            ? new SyntaxToken(token, parent, position)
            : new SyntaxNode(green, parent, position);
}
