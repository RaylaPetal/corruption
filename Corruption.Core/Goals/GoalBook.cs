using System.Globalization;

namespace Corruption.Core;

/// What goals and achievements count. Stored by name, so never rename one.
public enum GoalKind
{
    SceneSeconds,
    WatchedSeconds,
    CrowdSeconds,
    EdgedSeconds,
    Climaxes,
    PartnerClimaxes,
    LayersGiven,
    LayersReceived,
    DistinctAnimations,
    DistinctPartners,
    ActiveDays,
}

public sealed record GoalDef(string Id, string Text, GoalKind Kind, long Target, double Xp);

public sealed class GoalState
{
    public string Id { get; set; } = "";
    public long Progress { get; set; }
    public bool Done { get; set; }
}

/// Goals and achievements for one character, stored in the save.
public sealed class GoalSave
{
    /// Picks each period's goals, so they differ between characters.
    public int Seed { get; set; }

    public Dictionary<string, long> Stats { get; set; } = [];
    public HashSet<string> Achievements { get; set; } = [];
    public Dictionary<string, int> PartnerScenes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string DailyKey { get; set; } = "";
    public string WeeklyKey { get; set; } = "";
    public List<GoalState> Daily { get; set; } = [];
    public List<GoalState> Weekly { get; set; } = [];

    /// What's already counted this period for the goals that only count something once.
    public HashSet<string> DaySeen { get; set; } = [];
    public HashSet<string> WeekSeen { get; set; } = [];
}

public sealed record AchievementDef(string Id, string Name, string Text, double Xp, long Target, Func<CharacterSave, long> Progress);

public enum RewardKind { Daily, Weekly, Achievement }

/// Something just completed. Title is the bare name, for a popup; Message is the sentence for the chat log.
public sealed record GoalReward(RewardKind Kind, string Title, string Message, double Xp);

public static class GoalCatalog
{
    public const int DailyCount = 3;
    public const int WeeklyCount = 3;

    /// Every goal can be met with just Corruption: layers need a Cummy pairing, so they're only achievements.
    public static readonly IReadOnlyList<GoalDef> Daily =
    [
        new("d_scene15", "Spend 15 minutes in scenes", GoalKind.SceneSeconds, 15 * 60, 150),
        new("d_scene30", "Spend 30 minutes in scenes", GoalKind.SceneSeconds, 30 * 60, 300),
        new("d_climax1", "Climax", GoalKind.Climaxes, 1, 100),
        new("d_climax3", "Climax 3 times", GoalKind.Climaxes, 3, 250),
        new("d_partnerclimax", "Be there when a partner climaxes", GoalKind.PartnerClimaxes, 1, 100),
        new("d_watched", "Be watched for 5 minutes", GoalKind.WatchedSeconds, 5 * 60, 200),
        new("d_variety", "Play 3 different animations", GoalKind.DistinctAnimations, 3, 150),
    ];

    public static readonly IReadOnlyList<GoalDef> Weekly =
    [
        new("w_scene120", "Spend 2 hours in scenes", GoalKind.SceneSeconds, 120 * 60, 1500),
        new("w_climax15", "Climax 15 times", GoalKind.Climaxes, 15, 1200),
        new("w_partnerclimax", "Be there for 10 of a partner's climaxes", GoalKind.PartnerClimaxes, 10, 800),
        new("w_watched", "Be watched for 30 minutes", GoalKind.WatchedSeconds, 30 * 60, 1000),
        new("w_variety", "Play 10 different animations", GoalKind.DistinctAnimations, 10, 800),
        new("w_partners", "Have scenes with 2 different partners", GoalKind.DistinctPartners, 2, 800),
        new("w_days", "Have a scene on 4 different days", GoalKind.ActiveDays, 4, 1000),
    ];

    private static long Stat(CharacterSave save, GoalKind kind)
        => save.Goals.Stats.TryGetValue(kind.ToString(), out var value) ? value : 0;

    public static readonly IReadOnlyList<AchievementDef> Achievements =
    [
        new("first_taste", "First Taste", "Earn from your first scene", 250, 1, s => Stat(s, GoalKind.SceneSeconds) > 0 ? 1 : 0),
        new("worked_up", "Worked Up", "Climax 10 times", 500, 10, s => Stat(s, GoalKind.Climaxes)),
        new("insatiable", "Insatiable", "Climax 100 times", 3000, 100, s => Stat(s, GoalKind.Climaxes)),
        new("well_travelled", "Well Travelled", "Have scenes with 10 different partners", 1500, 10, s => s.SeenPartners.Count),
        new("devoted", "Devoted", "Have 50 scenes with one partner", 3000, 50, s => s.Goals.PartnerScenes.Count == 0 ? 0 : s.Goals.PartnerScenes.Values.Max()),
        new("collector", "Collector", "Play 50 different animations", 1500, 50, s => s.SeenActs.Count),
        new("shameless", "Shameless", "Spend an hour watched by 3 or more people", 3000, 3600, s => Stat(s, GoalKind.CrowdSeconds)),
        new("painted", "Painted", "Receive 10 layers from a climax", 500, 10, s => Stat(s, GoalKind.LayersReceived)),
        new("cum_dump", "Cum Dump", "Receive 100 layers from a climax", 3000, 100, s => Stat(s, GoalKind.LayersReceived)),
        new("generous", "Generous", "Give 50 layers from a climax", 1500, 50, s => Stat(s, GoalKind.LayersGiven)),
        new("edged", "Edged", "Hold at 100 arousal in a scene for 10 minutes", 1500, 600, s => Stat(s, GoalKind.EdgedSeconds)),
        new("habit", "Habit", "Reach a 7 day streak", 1000, 7, s => s.StreakDays),
        new("fallen", "Fallen", "Reach the Fallen tier", 5000, 95, s => s.Level),
        new("fallen_star", "Fallen Star", "Prestige 5 times", 10000, 5, s => s.Prestige),
    ];

    public static GoalDef? Find(string id) => Daily.Concat(Weekly).FirstOrDefault(g => g.Id == id);
}

/// Counts what happens, keeps the current goals, and reports what's been completed. Days and weeks are UTC.
public sealed class GoalBook(CharacterSave save)
{
    private readonly List<GoalReward> rewards = [];

    private GoalSave Data => save.Goals;

    /// Rolls over to new goals when the day or week has changed.
    public void Roll(DateTime now)
    {
        if (Data.Seed == 0) Data.Seed = Random.Shared.Next(1, int.MaxValue);

        var day = now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (Data.DailyKey != day)
        {
            Data.DailyKey = day;
            Data.Daily = Pick(GoalCatalog.Daily, GoalCatalog.DailyCount, day);
            Data.DaySeen.Clear();
        }

        var week = StartOfWeek(now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (Data.WeeklyKey != week)
        {
            Data.WeeklyKey = week;
            Data.Weekly = Pick(GoalCatalog.Weekly, GoalCatalog.WeeklyCount, week);
            Data.WeekSeen.Clear();
        }
    }

    public static DateTime StartOfWeek(DateTime now)
    {
        var sinceMonday = ((int)now.DayOfWeek + 6) % 7;
        return now.Date.AddDays(-sinceMonday);
    }

    private List<GoalState> Pick(IReadOnlyList<GoalDef> pool, int count, string period)
    {
        var random = new Random(Data.Seed ^ StableHash(period));
        var shuffled = pool.ToArray();
        random.Shuffle(shuffled);
        return shuffled.Take(count).Select(g => new GoalState { Id = g.Id }).ToList();
    }

    /// string.GetHashCode differs between runs, and a period's goals have to stay the same.
    private static int StableHash(string text)
    {
        var hash = 17;
        foreach (var c in text) hash = unchecked(hash * 31 + c);
        return hash;
    }

    /// Counts toward the lifetime total and the current goals.
    public void Record(GoalKind kind, long amount = 1)
    {
        AddStat(kind, amount);
        Advance(Data.Daily, kind, amount, RewardKind.Daily);
        Advance(Data.Weekly, kind, amount, RewardKind.Weekly);
    }

    /// For goals that count something once per period, such as different animations.
    public void RecordOnce(GoalKind kind, string key)
    {
        var id = $"{kind}:{key}";
        var isNewToday = Data.DaySeen.Add(id);
        var isNewThisWeek = Data.WeekSeen.Add(id);
        if (isNewToday) Advance(Data.Daily, kind, 1, RewardKind.Daily);
        if (isNewThisWeek)
        {
            Advance(Data.Weekly, kind, 1, RewardKind.Weekly);
            AddStat(kind, 1);
        }
    }

    public void RecordPartnerScene(string partner)
    {
        Data.PartnerScenes.TryGetValue(partner, out var count);
        Data.PartnerScenes[partner] = count + 1;
    }

    private void AddStat(GoalKind kind, long amount)
    {
        Data.Stats.TryGetValue(kind.ToString(), out var total);
        Data.Stats[kind.ToString()] = total + amount;
    }

    private void Advance(List<GoalState> goals, GoalKind kind, long amount, RewardKind rewardKind)
    {
        foreach (var goal in goals)
        {
            if (goal.Done || GoalCatalog.Find(goal.Id) is not { } def || def.Kind != kind) continue;
            goal.Progress = Math.Min(def.Target, goal.Progress + amount);
            if (goal.Progress < def.Target) continue;
            goal.Done = true;
            rewards.Add(new GoalReward(rewardKind, def.Text, $"{(rewardKind == RewardKind.Daily ? "Daily" : "Weekly")} goal done: {def.Text}.", def.Xp));
        }
    }

    /// Everything completed since the last call, including any achievements newly earned.
    public IReadOnlyList<GoalReward> TakeRewards()
    {
        foreach (var achievement in GoalCatalog.Achievements)
        {
            if (Data.Achievements.Contains(achievement.Id) || achievement.Progress(save) < achievement.Target) continue;
            Data.Achievements.Add(achievement.Id);
            rewards.Add(new GoalReward(RewardKind.Achievement, achievement.Name, $"Achievement unlocked: {achievement.Name}.", achievement.Xp));
        }

        var taken = rewards.ToList();
        rewards.Clear();
        return taken;
    }
}
