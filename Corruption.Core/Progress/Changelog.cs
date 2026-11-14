namespace Corruption.Core;

/// One release's notes, as players read them.
public sealed record ChangelogEntry(string Version, IReadOnlyList<string> Notes);

/// What changed in each release, shown once after an update. Add the new release at the top of Entries, with the
/// version it ships as in Corruption.csproj.
public static class Changelog
{
    public static readonly IReadOnlyList<ChangelogEntry> Entries =
    [
        new("0.3.6.2",
        [
            "Moodles: Corruption's statuses come back by themselves within a few seconds after a duty, or anything else that clears them. Inside a duty they stay off.",
        ]),
        new("0.3.6.1",
        [
            "Moodles: the Pregnant status now names the father, as in \"Carrying Bob Smith's child.\" Anyone who can see your Moodles can read it.",
            "Pregnancy belly: carriers can pick a Glamourer design or a Penumbra mod in Settings > Pregnancy. It goes on for the whole pregnancy once it's confirmed, and comes off at the birth. Tier visuals don't need to be on.",
            "A trimester's own visual under Tier visuals still goes on instead of the belly during that trimester, while tier visuals are on.",
            "This changelog: it shows once after each update, and /corr changelog opens it any time.",
        ]),
    ];

    /// The newest release with notes.
    public static string Latest => Entries[0].Version;

    /// The releases newer than the last one seen, newest first. With nothing seen yet, just the newest.
    public static IReadOnlyList<ChangelogEntry> Unseen(string? lastSeen)
    {
        if (!System.Version.TryParse(lastSeen, out var seen)) return [Entries[0]];
        return Entries.Where(e => System.Version.Parse(e.Version) > seen).ToList();
    }
}
