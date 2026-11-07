namespace Corruption.Core;

/// Where a nameplate's name text really is, from its node's values. The text can spill out of its box: a name and title
/// stacked in one node is taller than the box, and bottom-aligned text then grows upward past the box's top.
public static class PlateLayout
{
    private const float LineHeightPerFontUnit = 1.15f;

    /// The screen Y of the top of the drawn text.
    /// <param name="boxTop">The node's screen Y.</param>
    /// <param name="boxHeight">The node's height, already scaled.</param>
    /// <param name="alignment">The node's alignment: a 3x3 grid, left to right, top to bottom.</param>
    /// <param name="measuredHeight">The game's measure of the text, already scaled.</param>
    /// <param name="lines">Lines in the text, in case the game's measure counts fewer.</param>
    /// <param name="fontHeight">The font size, already scaled.</param>
    public static float DrawnTop(float boxTop, float boxHeight, int alignment, float measuredHeight, int lines, float fontHeight)
    {
        var drawn = MathF.Max(measuredHeight, lines * fontHeight * LineHeightPerFontUnit);
        return boxTop + (alignment / 3) switch
        {
            0 => 0f,
            1 => (boxHeight - drawn) / 2f,
            _ => boxHeight - drawn,
        };
    }

    /// Lines in a game text: a carriage return or the game's encoded newline ends one.
    public static int CountLines(ReadOnlySpan<byte> text)
    {
        ReadOnlySpan<byte> encoded = [0x02, 0x10, 0x01, 0x03];
        var lines = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == 0x0D)
            {
                lines++;
            }
            else if (text[i..].StartsWith(encoded))
            {
                lines++;
                i += encoded.Length - 1;
            }
        }
        return lines;
    }
}
