namespace XsdEditor.Core;

/// <summary>
/// A half-open range of characters in a source buffer.
/// </summary>
/// <remarks>
/// Every node in the syntax layer owns the exact span it was parsed from, including its
/// trivia. That is what lets an unmodified node be serialised as a byte copy of its
/// original text, which is how comment, whitespace and character-reference preservation
/// (<c>XE-067</c>, <c>XE-069</c>) fall out of the model rather than being implemented on
/// top of it.
/// </remarks>
/// <param name="Start">Zero-based index of the first character.</param>
/// <param name="Length">Number of characters covered. Never negative.</param>
public readonly record struct SourceSpan(int Start, int Length)
{
    /// <summary>Index one past the last character in the span.</summary>
    public int End => Start + Length;

    /// <summary>Whether the span covers no characters.</summary>
    public bool IsEmpty => Length == 0;

    /// <summary>Creates a span from a start and an end index.</summary>
    /// <param name="start">Zero-based index of the first character.</param>
    /// <param name="end">Index one past the last character. Must not precede <paramref name="start"/>.</param>
    /// <returns>The span running from <paramref name="start"/> up to <paramref name="end"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="start"/> is negative, or <paramref name="end"/> precedes it.
    /// </exception>
    public static SourceSpan FromBounds(int start, int end)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);
        return new SourceSpan(start, end - start);
    }

    /// <summary>Whether this span fully covers <paramref name="other"/>.</summary>
    /// <param name="other">The span to test for containment.</param>
    /// <returns><see langword="true"/> if <paramref name="other"/> lies within this span.</returns>
    public bool Contains(SourceSpan other) => other.Start >= Start && other.End <= End;

    /// <summary>Returns the text this span covers within <paramref name="source"/>.</summary>
    /// <param name="source">The buffer the span was measured against.</param>
    /// <returns>The covered characters.</returns>
    public ReadOnlySpan<char> TextIn(ReadOnlySpan<char> source) => source.Slice(Start, Length);
}
