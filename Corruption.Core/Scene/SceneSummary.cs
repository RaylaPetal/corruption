namespace Corruption.Core;

/// What one scene added up to, from its first verified tick until it ended. Stored as the character's last scene.
public sealed class SceneSummary
{
    public DateTime StartedUtc { get; set; }
    public DateTime EndedUtc { get; set; }

    /// Seconds that passed the scene check: pauses and gaps aren't counted.
    public double VerifiedSeconds { get; set; }

    /// XP actually gained, after difficulty, including climaxes, goals and achievements during the scene.
    public double Xp { get; set; }
    public long Sins { get; set; }
    public int OwnClimaxes { get; set; }
    public int PartnerClimaxes { get; set; }
    public int PeakWatchers { get; set; }

    /// Partners as `Name@World`, in the order they joined.
    public List<string> Partners { get; set; } = [];
}

public static class SceneSummaries
{
    /// Scenes shorter than this only update the Last scene card.
    public const double PopupSeconds = 60;

    public static bool WorthAPopup(SceneSummary summary) => summary.VerifiedSeconds >= PopupSeconds;

    public static string Length(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}" : $"{span.Minutes}:{span.Seconds:00}";
    }

    private static string Count(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{n} {many}";

    /// The names, without worlds.
    public static string Partners(SceneSummary summary)
        => string.Join(", ", summary.Partners.Select(p => p.Split('@')[0]));

    /// The summary as lines for a popup or card.
    public static IReadOnlyList<string> Lines(SceneSummary summary)
    {
        var lines = new List<string> { $"{Length(summary.VerifiedSeconds)} · +{summary.Xp:0} XP · +{summary.Sins} Sins" };
        var events = new List<string>();
        if (summary.OwnClimaxes > 0) events.Add(Count(summary.OwnClimaxes, "climax", "climaxes"));
        if (summary.PartnerClimaxes > 0) events.Add(Count(summary.PartnerClimaxes, "partner climax", "partner climaxes"));
        if (summary.PeakWatchers > 0) events.Add($"up to {Count(summary.PeakWatchers, "watcher", "watchers")}");
        if (events.Count > 0) lines.Add(string.Join(" · ", events));
        if (summary.Partners.Count > 0) lines.Add($"With {Partners(summary)}");
        return lines;
    }

    public static GoalReward Reward(SceneSummary summary)
        => new(RewardKind.Scene, "Scene over", "", 0, string.Join("\n", Lines(summary)));
}

/// A partner's history with this character: scenes, time together, and when.
public sealed class PartnerRecord
{
    public int Scenes { get; set; }
    public double Seconds { get; set; }

    /// Null for records seeded from an older save, which only knew the count.
    public DateTime? FirstUtc { get; set; }
    public DateTime? LastUtc { get; set; }
}

public static class PartnerBook
{
    /// Turns the older per-partner scene counts into records, once.
    public static void Seed(CharacterSave save)
    {
        if (save.PartnersSeeded) return;
        foreach (var (key, count) in save.Goals.PartnerScenes)
            if (!save.Partners.ContainsKey(key)) save.Partners[key] = new PartnerRecord { Scenes = count };
        save.PartnersSeeded = true;
    }

    public static PartnerRecord? Of(CharacterSave save, string key) => save.Partners.GetValueOrDefault(key);

    /// A scene with this partner has started.
    public static void Started(CharacterSave save, string key, DateTime now)
    {
        if (!save.Partners.TryGetValue(key, out var record)) save.Partners[key] = record = new PartnerRecord();
        record.Scenes++;
        record.FirstUtc ??= now;
        record.LastUtc = now;
    }

    /// Another verified second with this partner.
    public static void Together(CharacterSave save, string key, DateTime now, double seconds)
    {
        if (!save.Partners.TryGetValue(key, out var record)) return;
        record.Seconds += seconds;
        record.LastUtc = now;
    }
}
