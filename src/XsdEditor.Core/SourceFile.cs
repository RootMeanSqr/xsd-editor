using System.Text;

namespace XsdEditor.Core;

/// <summary>
/// Reads a schema file into text without losing the bytes that are not text.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="File.ReadAllText(string)"/> is not sufficient here. It detects and
/// <em>discards</em> a byte-order mark, so text read that way and written back is not the
/// file that was read — which quietly weakens every claim <c>XE-067</c> and <c>XE-083</c>
/// make about byte fidelity, in exactly the case nobody tests.
/// </para>
/// <para>
/// The preamble is therefore kept separately rather than parsed. A BOM is an encoding
/// artefact, not markup: leaving it in the text would put a <c>U+FEFF</c> in front of the
/// XML declaration, where the parser would rightly call it character data outside the root
/// element.
/// </para>
/// </remarks>
public sealed class SourceFile
{
    private static readonly UTF8Encoding _utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private SourceFile(byte[] preamble, string text)
    {
        Preamble = preamble;
        Text = text;
    }

    /// <summary>The byte-order mark the file began with, empty when it had none.</summary>
    public byte[] Preamble { get; }

    /// <summary>The file's text, with the preamble removed.</summary>
    public string Text { get; }

    /// <summary>Reads a file, separating its preamble from its text.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The preamble and the decoded text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public static SourceFile Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var bytes = File.ReadAllBytes(path);
        var preamble = Encoding.UTF8.Preamble;

        return bytes.AsSpan().StartsWith(preamble)
            ? new SourceFile(bytes[..preamble.Length], _utf8WithoutBom.GetString(bytes.AsSpan(preamble.Length)))
            : new SourceFile([], _utf8WithoutBom.GetString(bytes));
    }

    /// <summary>
    /// Renders text back to the bytes this file would hold if it carried that text.
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <returns>The preamble followed by <paramref name="text"/> in UTF-8.</returns>
    /// <remarks>
    /// This is what makes a round-trip assertion a claim about bytes rather than about
    /// decoded strings. Comparing strings would pass on a file whose BOM had been dropped.
    /// </remarks>
    public byte[] ToBytes(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var body = _utf8WithoutBom.GetBytes(text);
        if (Preamble.Length == 0)
        {
            return body;
        }

        var result = new byte[Preamble.Length + body.Length];
        Preamble.CopyTo(result, 0);
        body.CopyTo(result, Preamble.Length);
        return result;
    }
}
