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
    ItemsUsed,
    CravingsSatisfied,
    ClimaxesTogether,
    SoloSeconds,
    SoloClimaxes,
    Licks,
    FullBodyLayers,
    Creampies,
}

/// <param name="Eligible">Whether a character can meet this goal now; null for anyone. Checked only when goals are picked.</param>
public sealed record GoalDef(string Id, string Text, GoalKind Kind, long Target, double Xp, Func<CharacterSave, bool>? Eligible = null);

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

/// <param name="Hidden">Shown as "???" until earned.</param>
/// <param name="Series">The first tier's id, for tiers after the first: only the next unearned tier is shown.</param>
public sealed record AchievementDef(string Id, string Name, string Text, double Xp, long Target, Func<CharacterSave, long> Progress,
    bool Hidden = false, string? Series = null);

/// What a popup celebrates. Stored nowhere, so new kinds can go anywhere.
public enum RewardKind { Daily, Weekly, Achievement, Tier, Title, Scene }

/// Something just completed. Title is the bare name, for a popup; Message is the sentence for the chat log. Detail is
/// more for the popup, one line each: a title's bonus, or a scene's totals.
public sealed record GoalReward(RewardKind Kind, string Title, string Message, double Xp, string? Detail = null);

public static class GoalCatalog
{
    public const int DailyCount = 3;
    public const int WeeklyCount = 3;

    private static bool Pregnant(CharacterSave s) => s.Pregnancy.Enabled && PregnancyBook.IsConfirmed(s.Pregnancy);

    private static bool TogetherBond(CharacterSave s) => s.BondFormed && BondLevels.LevelOf(s.BondSeconds) >= BondLevels.TogetherLevel;

    /// Holding at 100 only works by hand: automatic climax fires the moment arousal reaches 100.
    private static bool ClimaxesByHand(CharacterSave s) => s.Setup?.AutoClimax == false;

    /// Licking needs Cummy's plugin API 5.
    private static bool HasCummyV5(CharacterSave s) => s.HadCummyV5;

    /// Creampies need Cummy's plugin API 6.
    private static bool HasCummyV6(CharacterSave s) => s.HadCummyV6;

    /// Solo goals only for someone who has played alone, since they need a mod marked "Counts alone".
    private static bool HadSolo(CharacterSave s) => Stat(s, GoalKind.SoloSeconds) > 0;

    /// Every goal can be met with just Corruption: layers need a Cummy pairing, so they're only achievements. Goals that
    /// need pregnancy or a bond are only picked for characters who have them.
    public static readonly IReadOnlyList<GoalDef> Daily =
    [
        new("d_scene15", "Spend 15 minutes in scenes", GoalKind.SceneSeconds, 15 * 60, 75),
        new("d_scene30", "Spend 30 minutes in scenes", GoalKind.SceneSeconds, 30 * 60, 150),
        new("d_climax1", "Climax", GoalKind.Climaxes, 1, 50),
        new("d_climax3", "Climax 3 times", GoalKind.Climaxes, 3, 125),
        new("d_partnerclimax", "Be there when a partner climaxes", GoalKind.PartnerClimaxes, 1, 50),
        new("d_watched", "Be watched for 5 minutes", GoalKind.WatchedSeconds, 5 * 60, 100),
        new("d_variety", "Play 3 different animations", GoalKind.DistinctAnimations, 3, 75),
        new("d_edge", "Hold at 100 arousal for 2 minutes", GoalKind.EdgedSeconds, 2 * 60, 100, ClimaxesByHand),
        new("d_item", "Use an item", GoalKind.ItemsUsed, 1, 50),
        new("d_crowd", "Be watched by 3 or more for 3 minutes", GoalKind.CrowdSeconds, 3 * 60, 125),
        new("d_craving", "Satisfy today's craving", GoalKind.CravingsSatisfied, 1, 75, Pregnant),
        new("d_together", "Climax together with your bonded partner", GoalKind.ClimaxesTogether, 1, 100, TogetherBond),
        new("d_solo_climax", "Climax alone", GoalKind.SoloClimaxes, 1, 75, HadSolo),
        new("d_solo15", "Spend 15 minutes in solo scenes", GoalKind.SoloSeconds, 15 * 60, 50, HadSolo),
        new("d_lick", "Lick a partner clean", GoalKind.Licks, 1, 75, HasCummyV5),
        new("d_creampie", "Take a creampie", GoalKind.Creampies, 1, 75, HasCummyV6),
    ];

    public static readonly IReadOnlyList<GoalDef> Weekly =
    [
        new("w_scene120", "Spend 2 hours in scenes", GoalKind.SceneSeconds, 120 * 60, 750),
        new("w_climax15", "Climax 15 times", GoalKind.Climaxes, 15, 600),
        new("w_partnerclimax", "Be there for 10 of a partner's climaxes", GoalKind.PartnerClimaxes, 10, 400),
        new("w_watched", "Be watched for 30 minutes", GoalKind.WatchedSeconds, 30 * 60, 500),
        new("w_variety", "Play 10 different animations", GoalKind.DistinctAnimations, 10, 400),
        new("w_partners", "Have scenes with 2 different partners", GoalKind.DistinctPartners, 2, 400),
        new("w_days", "Have a scene on 4 different days", GoalKind.ActiveDays, 4, 500),
        new("w_edge", "Hold at 100 arousal for 10 minutes", GoalKind.EdgedSeconds, 10 * 60, 500, ClimaxesByHand),
        new("w_items", "Use 5 items", GoalKind.ItemsUsed, 5, 400),
        new("w_crowd", "Be watched by 3 or more for 15 minutes", GoalKind.CrowdSeconds, 15 * 60, 600),
        new("w_cravings", "Satisfy 4 cravings", GoalKind.CravingsSatisfied, 4, 500, Pregnant),
        new("w_together", "Climax together 3 times", GoalKind.ClimaxesTogether, 3, 600, TogetherBond),
        new("w_solo_climax", "Climax alone 5 times", GoalKind.SoloClimaxes, 5, 450, HadSolo),
        new("w_solo120", "Spend 2 hours in solo scenes", GoalKind.SoloSeconds, 120 * 60, 500, HadSolo),
        new("w_lick", "Lick partners clean 5 times", GoalKind.Licks, 5, 450, HasCummyV5),
        new("w_creampie", "Take 5 creampies", GoalKind.Creampies, 5, 450, HasCummyV6),
    ];

    private static long Stat(CharacterSave save, GoalKind kind)
        => save.Goals.Stats.TryGetValue(kind.ToString(), out var value) ? value : 0;

    public static readonly IReadOnlyList<AchievementDef> Achievements =
    [
        new("first_taste", "First Taste", "Earn from your first scene", 125, 1, s => Stat(s, GoalKind.SceneSeconds) > 0 ? 1 : 0),
        new("worked_up", "Worked Up", "Climax 10 times", 250, 10, s => Stat(s, GoalKind.Climaxes)),
        new("insatiable", "Insatiable", "Climax 100 times", 1500, 100, s => Stat(s, GoalKind.Climaxes)),
        new("well_travelled", "Well Travelled", "Have scenes with 10 different partners", 750, 10, s => s.SeenPartners.Count),
        new("devoted", "Devoted", "Have 50 scenes with one partner", 1500, 50, s => s.Goals.PartnerScenes.Count == 0 ? 0 : s.Goals.PartnerScenes.Values.Max()),
        new("collector", "Collector", "Play 50 different animations", 750, 50, s => s.SeenActs.Count),
        new("shameless", "Shameless", "Spend an hour watched by 3 or more people", 1500, 3600, s => Stat(s, GoalKind.CrowdSeconds)),
        new("painted", "Painted", "Receive 10 layers from a climax", 250, 10, s => Stat(s, GoalKind.LayersReceived)),
        new("cum_dump", "Cum Dump", "Receive 100 layers from a climax", 1500, 100, s => Stat(s, GoalKind.LayersReceived)),
        // Was "give 50 layers": each client now layers itself, so the giver can't count layers. Same id, so anyone who
        // earned it keeps it.
        new("generous", "Generous", "Be there for 50 of your partners' climaxes", 750, 50, s => Stat(s, GoalKind.PartnerClimaxes)),
        new("edged", "Edged", "Hold at 100 arousal in a scene for 10 minutes", 750, 600, s => Stat(s, GoalKind.EdgedSeconds)),
        new("habit", "Habit", "Reach a 7 day streak", 500, 7, s => s.StreakDays),
        new("fallen", "Fallen", "Reach the Fallen tier", 2500, 95, s => s.Level),
        new("fallen_star", "Fallen Star", "Prestige 5 times", 5000, 5, s => s.Prestige),
        new("expecting", "Expecting", "Conceive for the first time", 500, 1, s => s.Pregnancy.Conceptions),
        new("labour_of_love", "Labour of Love", "Give birth", 1000, 1, s => s.Pregnancy.Births),
        new("brood_mother", "Brood Mother", "Give birth 3 times", 2500, 3, s => s.Pregnancy.Births),
        new("sire", "Sire", "Father a child", 500, 1, s => s.Pregnancy.Fathered),
        new("soulmates", "Soulmates", "Reach bond level 5", 1500, 5, s => s.BondFormed ? BondLevels.LevelOf(s.BondSeconds) : 0),
        new("big_spender", "Big Spender", "Spend 1,000 Sins in the shop", 500, 1000, s => s.Wallet.SpentTotal),
        new("devout", "Devout", "Check in 14 days in a row", 750, 14, s => s.Wallet.CheckInStreak),

        // Higher tiers. Only the next one is shown until it's earned.
        new("insatiable_2", "Insatiable II", "Climax 250 times", 2000, 250, s => Stat(s, GoalKind.Climaxes), Series: "insatiable"),
        new("insatiable_3", "Insatiable III", "Climax 500 times", 2500, 500, s => Stat(s, GoalKind.Climaxes), Series: "insatiable"),
        new("well_travelled_2", "Well Travelled II", "Have scenes with 25 different partners", 1000, 25, s => s.SeenPartners.Count, Series: "well_travelled"),
        new("well_travelled_3", "Well Travelled III", "Have scenes with 50 different partners", 1500, 50, s => s.SeenPartners.Count, Series: "well_travelled"),
        new("shameless_2", "Shameless II", "Spend 5 hours watched by 3 or more people", 2000, 5 * 3600, s => Stat(s, GoalKind.CrowdSeconds), Series: "shameless"),
        new("shameless_3", "Shameless III", "Spend 10 hours watched by 3 or more people", 2500, 10 * 3600, s => Stat(s, GoalKind.CrowdSeconds), Series: "shameless"),

        // Hidden until earned.
        new("marathon", "Marathon", "Have a single scene of 60 minutes", 750, 3600, s => (long)s.LongestVerifiedSceneSeconds, Hidden: true),
        new("crowd_pleaser", "Crowd Pleaser", "Be watched by 6 or more at once", 750, 6, s => s.PeakWatchers, Hidden: true),
        new("in_sync", "In Sync", "Climax together 10 times", 1000, 10, s => Stat(s, GoalKind.ClimaxesTogether), Hidden: true),
        new("glutton", "Glutton", "Satisfy 10 cravings", 500, 10, s => Stat(s, GoalKind.CravingsSatisfied), Hidden: true),

        new("moon_touched", "Moon-Touched", "Have scenes during 5 different Full Moons", 750, 5, s => s.FullMoonsSeen),

        // Alone.
        new("self_love", "Self-Love", "Climax alone", 250, 1, s => Stat(s, GoalKind.SoloClimaxes)),
        new("hands_on", "Hands-On", "Climax alone 25 times", 750, 25, s => Stat(s, GoalKind.SoloClimaxes)),
        new("self_sufficient", "Self-Sufficient", "Climax alone 100 times", 1500, 100, s => Stat(s, GoalKind.SoloClimaxes), Series: "hands_on"),
        new("me_time", "Me Time", "Spend 5 hours in solo scenes", 1000, 5 * 3600, s => Stat(s, GoalKind.SoloSeconds)),
        new("caught_in_the_act", "Caught in the Act", "Climax alone while 3 or more watch", 750, 1, s => s.CaughtInTheAct, Hidden: true),

        // Through Cummy's plugin API 5.
        new("cleanup_crew", "Cleanup Crew", "Lick partners clean 25 times", 750, 25, s => Stat(s, GoalKind.Licks)),
        new("drenched", "Drenched", "Be layered Full Body by three quick climaxes", 1000, 1, s => Stat(s, GoalKind.FullBodyLayers)),
        new("head_to_toe", "Head to Toe", "Have every unblocked part layered at once", 750, 1, s => s.HeadToToe),
        new("glazed", "Glazed", "Have your face at its top stage", 500, 1, s => s.Glazed),

        // Through Cummy's plugin API 6.
        new("filled", "Filled", "Take 25 creampies", 750, 25, s => Stat(s, GoalKind.Creampies)),
        new("both_ways", "Both Ways", "Have your pussy and ass dripping at once", 500, 1, s => s.BothWays),
        new("seconds", "Seconds", "Be creampied again while still dripping", 300, 1, s => s.CreampieRefreshes),
        new("well_bred", "Well Bred", "Be creampied by 5 different partners", 750, 5, s => s.CreampiePartners.Count),
    ];

    public static AchievementDef? FindAchievement(string id) => Achievements.FirstOrDefault(a => a.Id == id);

    /// What the Goals page lists: everything earned, plus the next unearned tier of each series. Higher tiers stay out of
    /// sight until the one before is earned.
    public static IEnumerable<AchievementDef> Shown(CharacterSave save)
    {
        var earned = save.Goals.Achievements;
        foreach (var achievement in Achievements)
        {
            if (earned.Contains(achievement.Id)) { yield return achievement; continue; }
            var series = achievement.Series ?? achievement.Id;
            var tiers = Achievements.Where(a => a.Id == series || a.Series == series).ToList();
            if (tiers.First(a => !earned.Contains(a.Id)) == achievement) yield return achievement;
        }
    }

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
        var shuffled = pool.Where(g => g.Eligible?.Invoke(save) != false).ToArray();
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
