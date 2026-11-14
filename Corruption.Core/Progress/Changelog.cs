namespace Corruption.Core;

/// One release's notes, as players read them.
public sealed record ChangelogEntry(string Version, IReadOnlyList<string> Notes);

/// What changed in each release, shown once after an update. Add the new release at the top of Entries, with the
/// version it ships as in Corruption.csproj.
public static class Changelog
{
    public static readonly IReadOnlyList<ChangelogEntry> Entries =
    [
        new("0.3.6.3",
        [
            "Acts: when a picked mod's animation plays but doesn't count, the scene card now says why, and \"Copy details\" copies what to send with a bug report.",
            "The changelog now shows after an update even if a test build had marked a newer one as seen.",
        ]),
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

    /// The releases newer than the last one seen, newest first. With nothing seen yet, or a seen release newer than any
    /// here (left by a test build), just the newest.
    public static IReadOnlyList<ChangelogEntry> Unseen(string? lastSeen)
    {
        if (!System.Version.TryParse(lastSeen, out var seen) || seen > System.Version.Parse(Latest)) return [Entries[0]];
        return Entries.Where(e => System.Version.Parse(e.Version) > seen).ToList();
    }
}
