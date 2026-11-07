using System.Globalization;

namespace Corruption.Core;

/// A player, identified the way the game does across worlds.
public sealed class PeerRecord
{
    public string Name { get; set; } = "";
    public string World { get; set; } = "";

    public string Key => $"{Name}@{World}";

    public bool Matches(string name, string world)
        => string.Equals(Name, name, StringComparison.OrdinalIgnoreCase)
        && string.Equals(World, world, StringComparison.OrdinalIgnoreCase);
}

/// What a client publishes about its character in a SimpleHeels tag, which sync services carry to synced players.
/// Having the tag at all is how another client knows this player runs Corruption. Players without the plugin never see
/// it. Pose is null when no known act is playing.
public readonly record struct PresenceTag(PoseKey? Pose, int Level, int Arousal, int ClimaxCount, bool Paused)
{
    public const string Key = "Corruption";

    private const string Prefix = "C1";
    private const string NoPose = "-";

    /// Like `C1|50.0|12|70|3|0`: version, pose, level, arousal, climaxes this session, paused.
    public string Format()
    {
        var pose = Pose is { } p ? string.Create(CultureInfo.InvariantCulture, $"{p.EmoteMode}.{p.Variant}") : NoPose;
        return string.Create(CultureInfo.InvariantCulture, $"{Prefix}|{pose}|{Level}|{Arousal}|{ClimaxCount}|{(Paused ? 1 : 0)}");
    }

    /// Null for anything that isn't a well-formed tag of this version.
    public static PresenceTag? Parse(string? text)
    {
        var parts = text?.Split('|');
        if (parts is not { Length: 6 } || parts[0] != Prefix) return null;
        if (!TryNumber(parts[2], 999, out var level) || !TryNumber(parts[3], 100, out var arousal)
            || !TryNumber(parts[4], int.MaxValue, out var climaxes) || parts[5] is not ("0" or "1")) return null;

        PoseKey? pose = null;
        if (parts[1] != NoPose)
        {
            var dot = parts[1].IndexOf('.');
            if (dot <= 0 || !TryNumber(parts[1][..dot], ushort.MaxValue, out var mode) || !TryNumber(parts[1][(dot + 1)..], byte.MaxValue, out var variant))
                return null;
            pose = PoseKey.Normalized((uint)mode, (byte)variant);
        }
        return new PresenceTag(pose, level, arousal, climaxes, parts[5] == "1");
    }

    private static bool TryNumber(string s, int max, out int value)
        => int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value <= max;
}

/// Decides when to rewrite the tag. It only changes when something does, because every write travels through a sync
/// service, and that service belongs to someone else.
public sealed class TagPublisher
{
    public const double StableSeconds = 3;
    public const double GoneSeconds = 2;
    public const double MinGapSeconds = 2;
    public const double ArousalGapSeconds = 30;
    public const int ArousalStep = 10;

    private PoseKey? candidate;
    private DateTime candidateSince;
    private PoseKey? settled;
    private PresenceTag? published;
    private DateTime lastPublished = DateTime.MinValue;

    /// The tag has to be written again, such as when SimpleHeels has just appeared.
    public void Forget() => published = null;

    /// The tag to write now, if any.
    public PresenceTag? Update(DateTime now, PoseKey? ownAct, int level, double arousal, int climaxCount, bool paused)
    {
        if (ownAct != candidate)
        {
            candidate = ownAct;
            candidateSince = now;
        }
        // A brief gap between emotes, or a few quick changes, shouldn't each go out.
        var settle = candidate is null ? GoneSeconds : StableSeconds;
        if ((now - candidateSince).TotalSeconds >= settle) settled = candidate;

        var whole = PlateComposer.ArousalNumber(arousal);
        var wanted = new PresenceTag(paused ? null : settled, level, whole, climaxCount, paused);
        if (published is not { } last)
            return Publish(now, wanted);

        var sinceLast = (now - lastPublished).TotalSeconds;
        if (wanted.ClimaxCount != last.ClimaxCount || wanted.Paused != last.Paused) return Publish(now, wanted);
        if ((wanted.Pose != last.Pose || wanted.Level != last.Level) && sinceLast >= MinGapSeconds) return Publish(now, wanted);
        // Reaching zero or leaving it counts as a step, because nameplates only show arousal above zero.
        var arousalMoved = wanted.Arousal / ArousalStep != last.Arousal / ArousalStep || (wanted.Arousal == 0) != (last.Arousal == 0);
        if (arousalMoved && sinceLast >= ArousalGapSeconds) return Publish(now, wanted);
        return null;
    }

    private PresenceTag Publish(DateTime now, PresenceTag tag)
    {
        published = tag;
        lastPublished = now;
        return tag;
    }
}
