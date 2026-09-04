namespace XsdEditor.Core.Syntax;

/// <summary>
/// A parsed source buffer: the root of the syntax tree, the text it was built from, and
/// whatever the parser could not interpret.
/// </summary>
/// <remarks>
/// <para>
/// Parsing never throws on malformed input. <c>XE-031</c> requires a best-effort partial
/// render of a buffer that is not well-formed, so a tree is always produced and the
/// problems are reported in <see cref="Diagnostics"/>.
/// </para>
/// <para>
/// <strong>The tree covers every character of <see cref="Text"/>.</strong>
/// <see cref="SyntaxNode.ToFullString"/> on <see cref="Root"/> returns <see cref="Text"/>
/// exactly, for well-formed and malformed input alike. That round trip is the operational
/// form of "lossless" and is what the syntax invariant tests assert.
/// </para>
/// </remarks>
public sealed class SyntaxTree
{
    private SyntaxTree(string text, SyntaxNode root, IReadOnlyList<SyntaxDiagnostic> diagnostics)
    {
        Text = text;
        Root = root;
        Diagnostics = diagnostics;
    }

    /// <summary>The source buffer this tree was parsed from.</summary>
    public string Text { get; }

    /// <summary>The <see cref="SyntaxKind.Document"/> node at the root of the tree.</summary>
    public SyntaxNode Root { get; }

    /// <summary>What the parser could not interpret, in the order it was met.</summary>
    public IReadOnlyList<SyntaxDiagnostic> Diagnostics { get; }

    /// <summary>Whether the parser interpreted every character of the buffer.</summary>
    public bool IsWellFormed => Diagnostics.Count == 0;

    /// <summary>Parses a source buffer.</summary>
    /// <param name="text">The text to parse.</param>
    /// <returns>The tree, which always covers the whole of <paramref name="text"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static SyntaxTree Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var parser = new XmlParser(text);
        var green = parser.ParseDocument();
        var root = SyntaxNode.Wrap(green, parent: null, position: 0);

        return new SyntaxTree(text, root, parser.Diagnostics);
    }
}
