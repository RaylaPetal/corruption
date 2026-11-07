using System.Globalization;

namespace Corruption.Core;

/// The second SimpleHeels tag: stars and the bond request's target, as a hash. Kept apart from <see cref="PresenceTag"/>
/// so clients that only know the first tag still parse it.
public readonly record struct ExtraTag(int Stars, string? BondHash)
{
    public const string Key = "Corruption.Extra";

    private const string Prefix = "E1";
    private const string NoBond = "-";

    /// Like `E1|3|a1b2c3d4`, or `E1|0|-` with no bond request.
    public string Format()
        => string.Create(CultureInfo.InvariantCulture, $"{Prefix}|{Stars}|{BondHash ?? NoBond}");

    /// Null for anything that isn't a well-formed tag of this version.
    public static ExtraTag? Parse(string? text)
    {
        var parts = text?.Split('|');
        if (parts is not { Length: 3 } || parts[0] != Prefix) return null;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var stars) || stars > 9999) return null;
        if (parts[2] == NoBond) return new ExtraTag(stars, null);
        return BondHashes.IsHash(parts[2]) ? new ExtraTag(stars, parts[2]) : null;
    }
}

/// Writes the extra tag only when it changes, since it rarely does.
public sealed class ExtraTagPublisher
{
    private ExtraTag? published;

    /// The tag has to be written again, such as when SimpleHeels has just appeared.
    public void Forget() => published = null;

    /// The tag to write now, if any.
    public ExtraTag? Update(int stars, string? bondHash)
    {
        var wanted = new ExtraTag(stars, bondHash);
        if (published == wanted) return null;
        published = wanted;
        return wanted;
    }
}
