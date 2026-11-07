using System.Text.RegularExpressions;

namespace Corruption.Core;

/// Where a title comes from. Also how the picker groups it and what colour it shows in.
public enum TitleSource { Level, Prestige, Achievement, Hidden, Moon, Shop }

/// <param name="Id">Stored in saves and sent in the title tag: never rename one.</param>
/// <param name="HowTo">How to get it, for the picker.</param>
/// <param name="Effect">What it does, for the picker and popups. Empty for a cosmetic title.</param>
/// <param name="Achievement">The achievement that unlocks it, for achievement, hidden and moon titles.</param>
public sealed record TitleDef(string Id, string Name, TitleSource Source, string HowTo, string Effect, Modifiers Bonus,
    int Level = 0, int Stars = 0, string? Achievement = null, int Price = 0)
{
    public bool IsCosmetic => Effect.Length == 0;
}

/// One character's titles. Stored in the save.
public sealed class TitleSave
{
    public HashSet<string> Unlocked { get; set; } = [];

    /// The equipped title's id, or null for none.
    public string? Equipped { get; set; }

    /// True once titles already earned before this version have been unlocked, which happens once and silently.
    public bool Initialized { get; set; }
}

public static partial class TitleCatalog
{
    private static TitleDef Level(string id, string name, int level, double xp)
        => new(id, name, TitleSource.Level, $"Reach level {level}", $"Scene XP ×{xp:0.##}", new Modifiers { TickXp = xp }, Level: level);

    private static TitleDef Earned(string achievement, string name, string effect, Modifiers bonus, TitleSource source = TitleSource.Achievement)
        => new(achievement, name, source, "", effect, bonus, Achievement: achievement);

    private static TitleDef Shop(string id, string name, int price, string effect, Modifiers bonus)
        => new(id, name, TitleSource.Shop, $"Buy it in the shop for {price} Sins", effect, bonus, Price: price);

    public static readonly IReadOnlyList<TitleDef> All =
    [
        Level("lv_10", "Wanton", 10, 1.01),
        Level("lv_25", "Libertine", 25, 1.02),
        Level("lv_50", "Hedonist", 50, 1.03),
        Level("lv_75", "Debauchee", 75, 1.04),
        Level("lv_100", "Unrepentant", 100, 1.05),

        new("star_1", "Reborn", TitleSource.Prestige, "Prestige once", "Scene XP ×1.02", new Modifiers { TickXp = 1.02 }, Stars: 1),
        new("star_10", "Starfallen", TitleSource.Prestige, "Reach 10 stars", "Scene XP ×1.06", new Modifiers { TickXp = 1.06 }, Stars: 10),
        new("star_11", "Eternal", TitleSource.Prestige, "Reach 11 stars", "", Modifiers.None, Stars: 11),

        Earned("first_taste", "First Taste", "", Modifiers.None),
        Earned("expecting", "Expecting", "", Modifiers.None),
        Earned("worked_up", "Worked Up", "Arousal builds ×1.05 as fast", new Modifiers { Arousal = 1.05 }),
        Earned("edged", "Edged", "Arousal builds ×0.9 as fast from 80", new Modifiers { HighArousal = 0.9 }),
        Earned("well_travelled", "Well Travelled", "First scene with someone new pays ×2 XP", new Modifiers { FirstPartnerXp = 2 }),
        Earned("well_travelled_2", "Well Travelled II", "First scene with someone new pays ×2.5 XP", new Modifiers { FirstPartnerXp = 2.5 }),
        Earned("well_travelled_3", "Well Travelled III", "First scene with someone new pays ×3 XP", new Modifiers { FirstPartnerXp = 3 }),
        Earned("collector", "Collector", "A new animation pays ×2 XP", new Modifiers { FirstActXp = 2 }),
        Earned("shameless", "Shameless", "Watchers' bonus goes up to ×1.6", new Modifiers { WatcherCap = 1.6 }),
        Earned("shameless_2", "Shameless II", "Watchers' bonus goes up to ×1.7", new Modifiers { WatcherCap = 1.7 }),
        Earned("shameless_3", "Shameless III", "Watchers' bonus goes up to ×1.8", new Modifiers { WatcherCap = 1.8 }),
        Earned("habit", "Habit", "Streak bonus goes up to ×1.35", new Modifiers { StreakCap = 1.35 }),
        Earned("fallen", "Fallen", "Scene XP ×1.03", new Modifiers { TickXp = 1.03 }),
        Earned("fallen_star", "Fallen Star", "Scene XP ×1.05", new Modifiers { TickXp = 1.05 }),
        Earned("insatiable", "Insatiable", "Refractory period ×0.75", new Modifiers { Refractory = 0.75 }),
        Earned("insatiable_2", "Insatiable II", "Refractory period ×0.65", new Modifiers { Refractory = 0.65 }),
        Earned("insatiable_3", "Insatiable III", "Refractory period ×0.55", new Modifiers { Refractory = 0.55 }),
        Earned("painted", "Painted", "Marked bonus goes up to ×1.35", new Modifiers { MarkedCap = 1.35 }),
        Earned("cum_dump", "Cum Dump", "Marked bonus goes up to ×1.4", new Modifiers { MarkedCap = 1.4 }),
        Earned("generous", "Generous", "A partner's climax pays you 75% of theirs", new Modifiers { PartnerClimaxShare = 0.75 }),
        Earned("devoted", "Devoted", "Time with your bond counts ×1.2", new Modifiers { BondTime = 1.2 }),
        Earned("soulmates", "Soulmates", "Climaxing together pays ×2 XP and Sins", new Modifiers { TogetherPay = 2 }),
        Earned("brood_mother", "Brood Mother", "Your conception chance ×1.25", new Modifiers { ConceptionOwn = 1.25 }),
        Earned("sire", "Sire", "Partners' conception chance with you ×1.15", new Modifiers { ConceptionPartner = 1.15 }),
        Earned("labour_of_love", "Labour of Love", "Births pay ×1.5 Sins", new Modifiers { BirthSins = 1.5 }),
        Earned("big_spender", "Big Spender", "Shop items cost 5% less", new Modifiers { ItemPrice = 0.95 }),
        Earned("devout", "Devout", "Daily check-in +10 Sins", new Modifiers { CheckInBonus = 10 }),

        Earned("marathon", "Marathon", "Fatigue starts 5 minutes later", new Modifiers { FatigueGraceMinutes = 5 }, TitleSource.Hidden),
        Earned("crowd_pleaser", "Crowd Pleaser", "Each watcher counts ×1.25", new Modifiers { WatcherCount = 1.25 }, TitleSource.Hidden),
        Earned("in_sync", "In Sync", "Climaxing together counts within 60 seconds", new Modifiers { TogetherSeconds = 60 }, TitleSource.Hidden),
        Earned("glutton", "Glutton", "Cravings pay ×2 Sins", new Modifiers { CravingSins = 2 }, TitleSource.Hidden),

        Earned("moon_touched", "Moon-Touched", "Scene XP ×1.08 during a Full Moon", new Modifiers { FullMoonTickXp = 1.08 }, TitleSource.Moon),

        Shop("shop_tease", "Tease", 400, "/corr climax works from 60 arousal", new Modifiers { ManualClimaxMinimum = 60 }),
        Shop("shop_harlot", "Harlot", 500, "Scene time pays ×1.25 Sins", new Modifiers { SceneSins = 1.25 }),
        Shop("shop_tireless", "Tireless", 600, "Fatigue starts 3 minutes later", new Modifiers { FatigueGraceMinutes = 3 }),
        Shop("shop_unholy", "Unholy", 2000, "", Modifiers.None),
    ];

    public const string Unholy = "shop_unholy";

    public static TitleDef? Find(string? id) => id is null ? null : All.FirstOrDefault(t => t.Id == id);

    [GeneratedRegex("^[a-z0-9_]{1,24}$")]
    private static partial Regex IdPattern();

    /// Ids are sent in a tag, so they're kept short and plain.
    public static bool IsValidId(string? id) => id is not null && IdPattern().IsMatch(id);

    /// How to get a title. Achievement titles take their achievement's own condition.
    public static string HowTo(TitleDef title)
        => title.Achievement is { } id && GoalCatalog.FindAchievement(id) is { } achievement ? achievement.Text : title.HowTo;
}

public static class TitleRules
{
    /// Where Edged's slower arousal starts.
    public const double HighArousal = 80;
}

/// Unlocking, buying and equipping titles, and the equipped one's bonus, for one character.
public static class TitleBook
{
    public static bool IsUnlocked(CharacterSave save, string id) => save.Titles.Unlocked.Contains(id);

    public static TitleDef? Equipped(CharacterSave save)
        => save.Titles.Equipped is { } id && IsUnlocked(save, id) ? TitleCatalog.Find(id) : null;

    public static Modifiers Bonus(CharacterSave save) => Equipped(save)?.Bonus ?? Modifiers.None;

    /// Whether the character has earned a title that isn't bought. Prestige means level 100 was reached once.
    private static bool Earned(CharacterSave save, TitleDef title) => title.Source switch
    {
        TitleSource.Level => save.Level >= title.Level || save.Prestige > 0,
        TitleSource.Prestige => save.Prestige >= title.Stars,
        TitleSource.Shop => false,
        _ => title.Achievement is { } id && save.Goals.Achievements.Contains(id),
    };

    /// Unlocks everything newly earned and returns it. The first time, it unlocks what was earned before titles existed
    /// and returns nothing, so there are no popups for it.
    public static IReadOnlyList<TitleDef> Sync(CharacterSave save)
    {
        var unlocked = new List<TitleDef>();
        foreach (var title in TitleCatalog.All)
            if (!IsUnlocked(save, title.Id) && Earned(save, title) && save.Titles.Unlocked.Add(title.Id))
                unlocked.Add(title);

        if (save.Titles.Initialized) return unlocked;
        save.Titles.Initialized = true;
        return [];
    }

    /// Why a shop title can't be bought right now, or null when it can.
    public static string? BuyRefusal(CharacterSave save, TitleDef title)
        => title.Source != TitleSource.Shop ? "That title isn't for sale."
            : IsUnlocked(save, title.Id) ? "You already own it."
            : save.Wallet.Sins < title.Price ? $"You need {title.Price - save.Wallet.Sins} more Sins."
            : null;

    /// Buys a shop title for good. False, with nothing taken, when refused.
    public static bool Buy(CharacterSave save, TitleDef title)
    {
        if (BuyRefusal(save, title) is not null || !Sins.TrySpend(save.Wallet, title.Price)) return false;
        save.Titles.Unlocked.Add(title.Id);
        return true;
    }

    /// Why this title can't be equipped, apart from the scene lock, or null when it can. Null id means none.
    public static string? EquipRefusal(CharacterSave save, string? id)
        => id is null ? null
            : TitleCatalog.Find(id) is null ? "There's no such title."
            : !IsUnlocked(save, id) ? "You haven't unlocked that title yet."
            : null;
}
