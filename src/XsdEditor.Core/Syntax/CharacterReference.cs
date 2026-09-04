namespace XsdEditor.Core.Syntax;

/// <summary>
/// Recognises character and entity references without resolving them.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here returns the character a reference stands for, and that is deliberate.
/// <c>XE-069</c> requires references to survive in their original spelling, so the syntax
/// layer only ever needs to know <em>whether</em> an ampersand opens one — to tell a
/// reference apart from a raw ampersand — never what it means.
/// </para>
/// <para>
/// Only the five predefined entities are accepted by name. <c>0005</c> puts DTDs outside
/// the lexer's scope, so there is no entity declaration to consult and a
/// <c>&amp;something;</c> that is not predefined is not a reference we can honour.
/// </para>
/// </remarks>
internal static class CharacterReference
{
    private static readonly string[] _predefined = ["amp", "lt", "gt", "quot", "apos"];

    /// <summary>
    /// Classifies what, if anything, an ampersand opens.
    /// </summary>
    /// <param name="text">The buffer to look in.</param>
    /// <param name="index">Index of the <c>&amp;</c> that might open a reference.</param>
    /// <returns>
    /// <see cref="ReferenceKind.Valid"/> for <c>&amp;#nn;</c>, <c>&amp;#xhh;</c> naming a
    /// legal XML character, or one of <c>&amp;amp;</c>, <c>&amp;lt;</c>, <c>&amp;gt;</c>,
    /// <c>&amp;quot;</c> and <c>&amp;apos;</c>; <see cref="ReferenceKind.InvalidCharacter"/>
    /// for a numeric reference naming a code point XML forbids; otherwise
    /// <see cref="ReferenceKind.None"/>.
    /// </returns>
    public static ReferenceKind Classify(string text, int index)
    {
        if (index >= text.Length || text[index] != '&')
        {
            return ReferenceKind.None;
        }

        var next = index + 1;
        return next < text.Length && text[next] == '#'
            ? ClassifyNumeric(text, next + 1)
            : IsPredefined(text, next) ? ReferenceKind.Valid : ReferenceKind.None;
    }

    private static ReferenceKind ClassifyNumeric(string text, int index)
    {
        var hex = index < text.Length && (text[index] is 'x' or 'X');
        if (hex)
        {
            index++;
        }

        var digits = 0;
        var codePoint = 0;
        var overflowed = false;

        while (index < text.Length && IsDigit(text[index], hex))
        {
            if (!overflowed)
            {
                codePoint = (codePoint * (hex ? 16 : 10)) + Value(text[index]);

                // Anything past the last legal code point is out of range whatever the
                // remaining digits say, and stopping here keeps the accumulator from
                // overflowing on a long run of them.
                overflowed = codePoint > 0x10FFFF;
            }

            index++;
            digits++;
        }

        if (digits == 0 || index >= text.Length || text[index] != ';')
        {
            return ReferenceKind.None;
        }

        return !overflowed && IsXmlCharacter(codePoint)
            ? ReferenceKind.Valid
            : ReferenceKind.InvalidCharacter;
    }

    /// <summary>
    /// Whether a code point is one XML 1.0 allows in a document.
    /// </summary>
    /// <remarks>
    /// The <c>Char</c> production: tab, line feed and carriage return, then everything from
    /// the space up, minus the surrogate block and the two non-characters at the end of the
    /// BMP. A reference naming anything else is well-formed as syntax and still rejected by
    /// every conforming reader, <see cref="System.Xml.XmlReader"/> included — which is why
    /// it is reported here rather than passed on as if it were text.
    /// </remarks>
    private static bool IsXmlCharacter(int codePoint) =>
        codePoint is 0x9 or 0xA or 0xD
            or >= 0x20 and <= 0xD7FF
            or >= 0xE000 and <= 0xFFFD
            or >= 0x10000 and <= 0x10FFFF;

    private static int Value(char digit) => digit switch
    {
        >= '0' and <= '9' => digit - '0',
        >= 'a' and <= 'f' => digit - 'a' + 10,
        _ => digit - 'A' + 10,
    };

    private static bool IsDigit(char value, bool hex) =>
        value is >= '0' and <= '9'
        || (hex && (value is >= 'a' and <= 'f' or >= 'A' and <= 'F'));

    private static bool IsPredefined(string text, int index)
    {
        foreach (var name in _predefined)
        {
            var end = index + name.Length;
            if (end < text.Length
                && text[end] == ';'
                && string.CompareOrdinal(text, index, name, 0, name.Length) == 0)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>What an ampersand turned out to open.</summary>
internal enum ReferenceKind
{
    /// <summary>No reference: a raw ampersand, or something only shaped like one.</summary>
    None,

    /// <summary>A character or predefined entity reference naming a legal XML character.</summary>
    Valid,

    /// <summary>
    /// A numeric reference whose syntax is well formed but whose code point XML forbids —
    /// a surrogate, a control character, or one past the end of Unicode.
    /// </summary>
    InvalidCharacter,
}
