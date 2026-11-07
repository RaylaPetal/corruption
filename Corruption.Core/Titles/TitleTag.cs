namespace Corruption.Core;

/// The fourth SimpleHeels tag: the equipped title's id, or none. Kept apart from the other tags so older clients still
/// parse theirs. Receivers show only ids they know, so a newer client's title shows nothing rather than garbage.
public readonly record struct TitleTag(string? Id)
{
    public const string Key = "Corruption.Title";

    private const string Prefix = "T1";
    private const string NoTitle = "-";

    /// Like `T1|insatiable`, or `T1|-` with no title.
    public string Format() => $"{Prefix}|{Id ?? NoTitle}";

    /// Null for anything that isn't a well-formed tag of this version.
    public static TitleTag? Parse(string? text)
    {
        var parts = text?.Split('|');
        if (parts is not { Length: 2 } || parts[0] != Prefix) return null;
        if (parts[1] == NoTitle) return new TitleTag(null);
        return TitleCatalog.IsValidId(parts[1]) ? new TitleTag(parts[1]) : null;
    }
}

/// Writes the title tag only when the equipped title changes, which can't happen during a scene.
public sealed class TitleTagPublisher
{
    private TitleTag? published;

    public void Forget() => published = null;

    public TitleTag? Update(TitleTag wanted)
    {
        if (published == wanted) return null;
        published = wanted;
        return wanted;
    }
}
