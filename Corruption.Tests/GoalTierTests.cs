using Corruption.Core;

namespace Corruption.Tests;

/// Tiered and hidden achievements, the new goals, and which goals a character is offered.
public class GoalTierTests
{
    private static readonly DateTime T0 = new(2026, 1, 7, 12, 0, 0, DateTimeKind.Utc); // A Wednesday.

    private static (CharacterSave Save, GoalBook Book) Rolled(CharacterSave? save = null)
    {
        save ??= new CharacterSave();
        var book = new GoalBook(save);
        book.Roll(T0);
        return (save, book);
    }

    private static void MakePregnant(CharacterSave save)
    {
        save.Pregnancy.Enabled = true;
        save.Pregnancy.CanCarry = true;
        save.Pregnancy.ConceivedUtc = T0;
        save.Pregnancy.DueUtc = T0.AddDays(14);
    }

    private static void MakeTogetherBond(CharacterSave save)
    {
        save.BondWith = "Bob B@W";
        save.BondFormed = true;
        save.BondSeconds = BondLevels.Thresholds[BondLevels.TogetherLevel - 1];
    }

    [Fact]
    public void ExistingAchievementIdsAreUnchanged()
    {
        string[] before =
        [
            "first_taste", "worked_up", "insatiable", "well_travelled", "devoted", "collector", "shameless", "painted", "cum_dump",
            "generous", "edged", "habit", "fallen", "fallen_star", "expecting", "labour_of_love", "brood_mother", "sire", "soulmates",
            "big_spender", "devout",
        ];
        Assert.All(before, id => Assert.NotNull(GoalCatalog.FindAchievement(id)));
    }

    [Theory]
    [InlineData("insatiable_2", 250, 2000)]
    [InlineData("insatiable_3", 500, 2500)]
    [InlineData("well_travelled_2", 25, 1000)]
    [InlineData("well_travelled_3", 50, 1500)]
    [InlineData("shameless_2", 5 * 3600, 2000)]
    [InlineData("shameless_3", 10 * 3600, 2500)]
    [InlineData("marathon", 3600, 750)]
    [InlineData("crowd_pleaser", 6, 750)]
    [InlineData("in_sync", 10, 1000)]
    [InlineData("glutton", 10, 500)]
    public void NewAchievementsHaveTheirTargets(string id, long target, double xp)
    {
        var achievement = GoalCatalog.FindAchievement(id)!;
        Assert.Equal(target, achievement.Target);
        Assert.Equal(xp, achievement.Xp);
    }

    [Fact]
    public void TiersBelongToTheirSeriesAndHiddenOnesAreHidden()
    {
        Assert.Equal("insatiable", GoalCatalog.FindAchievement("insatiable_3")!.Series);
        Assert.All(new[] { "marathon", "crowd_pleaser", "in_sync", "glutton" }, id => Assert.True(GoalCatalog.FindAchievement(id)!.Hidden));
        Assert.False(GoalCatalog.FindAchievement("insatiable")!.Hidden);
    }

    [Fact]
    public void ACountPastSeveralTiersUnlocksEachOne()
    {
        var (save, book) = Rolled();
        book.Record(GoalKind.Climaxes, 600);
        var names = book.TakeRewards().Where(r => r.Kind == RewardKind.Achievement).Select(r => r.Title).ToList();
        Assert.Contains("Insatiable", names);
        Assert.Contains("Insatiable II", names);
        Assert.Contains("Insatiable III", names);
        Assert.Contains("insatiable_3", save.Goals.Achievements);
    }

    [Fact]
    public void InSyncAndGluttonReadTheirStats()
    {
        var (save, book) = Rolled();
        book.Record(GoalKind.ClimaxesTogether, 10);
        book.Record(GoalKind.CravingsSatisfied, 10);
        book.TakeRewards();
        Assert.Contains("in_sync", save.Goals.Achievements);
        Assert.Contains("glutton", save.Goals.Achievements);
    }

    [Fact]
    public void SixWatchersAtOnceUnlockCrowdPleaser()
    {
        var r = new EngineRig { Watchers = 6 };
        r.Run(11);
        Assert.Contains("crowd_pleaser", r.Save.Goals.Achievements);
        Assert.Equal(6, r.Save.PeakWatchers);
    }

    [Fact]
    public void MarathonNeedsOneLongScene()
    {
        var r = new EngineRig();
        r.Run(1800);
        r.Playing = false;
        r.Run(60);
        r.Playing = true;
        r.Run(1800);
        Assert.DoesNotContain("marathon", r.Save.Goals.Achievements);

        r.Run(1800);
        Assert.Contains("marathon", r.Save.Goals.Achievements);
    }

    [Fact]
    public void NobodyIsOfferedGoalsTheyCantMeet()
    {
        var (save, book) = Rolled();
        for (var day = 0; day < 120; day++)
        {
            book.Roll(T0.AddDays(day));
            var ids = save.Goals.Daily.Concat(save.Goals.Weekly).Select(g => g.Id).ToList();
            Assert.DoesNotContain("d_craving", ids);
            Assert.DoesNotContain("w_cravings", ids);
            Assert.DoesNotContain("d_together", ids);
            Assert.DoesNotContain("w_together", ids);
        }
    }

    [Fact]
    public void APregnantBondedCharacterCanBeOfferedThem()
    {
        var save = new CharacterSave();
        save.Goals.Seed = 1;
        MakePregnant(save);
        MakeTogetherBond(save);
        var (_, book) = Rolled(save);
        var seen = new HashSet<string>();
        for (var day = 0; day < 700; day++)
        {
            book.Roll(T0.AddDays(day));
            foreach (var goal in save.Goals.Daily.Concat(save.Goals.Weekly)) seen.Add(goal.Id);
        }
        Assert.Contains("d_craving", seen);
        Assert.Contains("d_together", seen);
        Assert.Contains("w_cravings", seen);
        Assert.Contains("w_together", seen);
    }

    [Fact]
    public void PickedGoalsStayWhenEligibilityChanges()
    {
        var save = new CharacterSave();
        MakePregnant(save);
        MakeTogetherBond(save);
        var (_, book) = Rolled(save);
        var daily = save.Goals.Daily.Select(g => g.Id).ToList();
        var weekly = save.Goals.Weekly.Select(g => g.Id).ToList();

        save.BondFormed = false;
        save.Pregnancy.ConceivedUtc = null;
        book.Roll(T0.AddHours(6));
        Assert.Equal(daily, save.Goals.Daily.Select(g => g.Id).ToList());
        Assert.Equal(weekly, save.Goals.Weekly.Select(g => g.Id).ToList());
    }

    [Theory]
    [InlineData("d_edge")]
    [InlineData("d_item")]
    [InlineData("d_crowd")]
    [InlineData("d_craving")]
    [InlineData("d_together")]
    [InlineData("w_edge")]
    [InlineData("w_items")]
    [InlineData("w_crowd")]
    [InlineData("w_cravings")]
    [InlineData("w_together")]
    public void EachNewGoalCompletesFromItsStat(string id)
    {
        var (save, book) = Rolled();
        var def = GoalCatalog.Find(id)!;
        var list = id.StartsWith('d') ? save.Goals.Daily : save.Goals.Weekly;
        save.Goals.Daily.Clear();
        save.Goals.Weekly.Clear();
        list.Add(new GoalState { Id = id });
        book.Record(def.Kind, def.Target);
        Assert.Contains(book.TakeRewards(), r => r.Title == def.Text && r.Xp == def.Xp);
    }

    [Fact]
    public void NewGoalsPayWithinTheGoalRanges()
    {
        Assert.All(GoalCatalog.Daily, g => Assert.InRange(g.Xp, 50, 150));
        Assert.All(GoalCatalog.Weekly, g => Assert.InRange(g.Xp, 400, 750));
        Assert.Equal(16, GoalCatalog.Daily.Count);
        Assert.Equal(16, GoalCatalog.Weekly.Count);
    }
}

public class AchievementListTests
{
    private static List<string> Shown(CharacterSave save) => GoalCatalog.Shown(save).Select(a => a.Id).ToList();

    [Fact]
    public void OnlyTheNextTierShows()
    {
        var save = new CharacterSave();
        Assert.Contains("insatiable", Shown(save));
        Assert.DoesNotContain("insatiable_2", Shown(save));

        save.Goals.Achievements.Add("insatiable");
        Assert.Contains("insatiable", Shown(save));
        Assert.Contains("insatiable_2", Shown(save));
        Assert.DoesNotContain("insatiable_3", Shown(save));
    }

    [Fact]
    public void HiddenOnesAreListedSoTheyCount()
    {
        var shown = Shown(new CharacterSave());
        Assert.Contains("marathon", shown);
        Assert.Contains("glutton", shown);
    }

    [Fact]
    public void EverythingEarnedShows()
    {
        var save = new CharacterSave();
        save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        Assert.Equal(GoalCatalog.Achievements.Count, Shown(save).Count);
    }
}

public class EdgingGoalTests
{
    private static readonly DateTime T0 = new(2026, 1, 7, 12, 0, 0, DateTimeKind.Utc);

    private static HashSet<string> Offered(bool autoClimax)
    {
        var save = new CharacterSave { Setup = new ProfileSetup { AutoClimax = autoClimax }, Goals = { Seed = 1 } };
        var book = new GoalBook(save);
        var seen = new HashSet<string>();
        for (var day = 0; day < 700; day++)
        {
            book.Roll(T0.AddDays(day));
            foreach (var goal in save.Goals.Daily.Concat(save.Goals.Weekly)) seen.Add(goal.Id);
        }
        return seen;
    }

    [Fact]
    public void AutomaticClimaxNeverGetsEdgingGoals()
    {
        var seen = Offered(autoClimax: true);
        Assert.DoesNotContain("d_edge", seen);
        Assert.DoesNotContain("w_edge", seen);
    }

    [Fact]
    public void ClimaxByHandGetsThem()
    {
        var seen = Offered(autoClimax: false);
        Assert.Contains("d_edge", seen);
        Assert.Contains("w_edge", seen);
    }

    [Fact]
    public void ClimaxModeAndGainCanChangeAfterSetup()
    {
        var r = new EngineRig { Playing = false };
        r.Engine.SetAutoClimax(false);
        Assert.False(r.Save.Setup!.AutoClimax);
        r.Engine.SetArousalGain(2.5);
        Assert.Equal(2.5, r.Save.Setup.ArousalGain);
        r.Engine.SetArousalGain(10);
        Assert.Equal(ProfileSetup.MaxArousalGain, r.Save.Setup.ArousalGain);
        r.Engine.SetArousalGain(0);
        Assert.Equal(ProfileSetup.MinArousalGain, r.Save.Setup.ArousalGain);
    }

    [Fact]
    public void ByHandArousalHoldsAtTheTopAndEdgingCounts()
    {
        var r = new EngineRig();
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Engine.SetAutoClimax(false);
        r.Run(1);
        r.Save.Arousal = 100;
        r.Run(60);
        Assert.Equal(100, r.Save.Arousal);
        Assert.DoesNotContain("You climaxed.", r.Host.Notes);
        Assert.True(r.Save.Goals.Stats["EdgedSeconds"] >= 50);
    }
}
