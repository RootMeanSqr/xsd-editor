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
    /// Whether a valid character or predefined entity reference starts at an index.
    /// </summary>
    /// <param name="text">The buffer to look in.</param>
    /// <param name="index">Index of the <c>&amp;</c> that might open a reference.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="index"/> begins <c>&amp;#nn;</c>,
    /// <c>&amp;#xhh;</c>, or one of <c>&amp;amp;</c>, <c>&amp;lt;</c>, <c>&amp;gt;</c>,
    /// <c>&amp;quot;</c> and <c>&amp;apos;</c>.
    /// </returns>
    public static bool StartsAt(string text, int index)
    {
        if (index >= text.Length || text[index] != '&')
        {
            return false;
        }

        var next = index + 1;
        return next < text.Length && text[next] == '#'
            ? IsNumeric(text, next + 1)
            : IsPredefined(text, next);
    }

    private static bool IsNumeric(string text, int index)
    {
        var hex = index < text.Length && (text[index] is 'x' or 'X');
        if (hex)
        {
            index++;
        }

        var digits = 0;
        while (index < text.Length && IsDigit(text[index], hex))
        {
            index++;
            digits++;
        }

        return digits > 0 && index < text.Length && text[index] == ';';
    }

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
