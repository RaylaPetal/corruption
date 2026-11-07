using Corruption.Core;

namespace Corruption.Tests;

public class GoalBookTests
{
    private static readonly DateTime T0 = new(2026, 1, 7, 12, 0, 0, DateTimeKind.Utc); // A Wednesday.

    private static (CharacterSave Save, GoalBook Book) Rolled()
    {
        var save = new CharacterSave();
        var book = new GoalBook(save);
        book.Roll(T0);
        return (save, book);
    }

    /// Sets the current goals to exactly one, so a test doesn't depend on the random pick.
    private static void Only(List<GoalState> goals, string id)
    {
        goals.Clear();
        goals.Add(new GoalState { Id = id });
    }

    [Fact]
    public void RollingPicksDistinctGoals()
    {
        var (save, _) = Rolled();
        Assert.Equal(GoalCatalog.DailyCount, save.Goals.Daily.Select(g => g.Id).Distinct().Count());
        Assert.Equal(GoalCatalog.WeeklyCount, save.Goals.Weekly.Select(g => g.Id).Distinct().Count());
    }

    [Fact]
    public void ThePickStaysTheSameWithinAPeriod()
    {
        var (save, book) = Rolled();
        var daily = save.Goals.Daily.Select(g => g.Id).ToList();
        save.Goals.Daily[0].Progress = 1;
        book.Roll(T0.AddHours(6));
        Assert.Equal(daily, save.Goals.Daily.Select(g => g.Id).ToList());
        Assert.Equal(1, save.Goals.Daily[0].Progress);
    }

    [Fact]
    public void ANewDayBringsNewDailiesButKeepsTheWeek()
    {
        var (save, book) = Rolled();
        save.Goals.Daily[0].Progress = 1;
        save.Goals.Weekly[0].Progress = 1;
        book.Roll(T0.AddDays(1));
        Assert.All(save.Goals.Daily, g => Assert.Equal(0, g.Progress));
        Assert.Equal(1, save.Goals.Weekly[0].Progress);
    }

    [Fact]
    public void ANewWeekStartsOnMonday()
    {
        var (save, book) = Rolled();
        save.Goals.Weekly[0].Progress = 1;
        book.Roll(new DateTime(2026, 1, 11, 23, 0, 0, DateTimeKind.Utc)); // Sunday.
        Assert.Equal(1, save.Goals.Weekly[0].Progress);
        book.Roll(new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc)); // Monday.
        Assert.All(save.Goals.Weekly, g => Assert.Equal(0, g.Progress));
    }

    [Fact]
    public void CompletingAGoalPaysOnce()
    {
        var (save, book) = Rolled();
        Only(save.Goals.Daily, "d_climax3");
        save.Goals.Weekly.Clear();
        book.Record(GoalKind.Climaxes, 2);
        Assert.DoesNotContain(book.TakeRewards(), r => r.Message.Contains("Daily"));
        book.Record(GoalKind.Climaxes);
        var reward = Assert.Single(book.TakeRewards(), r => r.Message.Contains("Daily"));
        Assert.Equal(250, reward.Xp);
        Assert.True(save.Goals.Daily[0].Done);
        book.Record(GoalKind.Climaxes);
        Assert.DoesNotContain(book.TakeRewards(), r => r.Message.Contains("Daily"));
    }

    [Fact]
    public void ProgressCountsTowardBothPeriods()
    {
        var (save, book) = Rolled();
        Only(save.Goals.Daily, "d_climax3");
        Only(save.Goals.Weekly, "w_climax15");
        book.Record(GoalKind.Climaxes, 2);
        Assert.Equal(2, save.Goals.Daily[0].Progress);
        Assert.Equal(2, save.Goals.Weekly[0].Progress);
    }

    [Fact]
    public void DifferentAnimationsCountOncePerPeriod()
    {
        var (save, book) = Rolled();
        Only(save.Goals.Daily, "d_variety");
        Only(save.Goals.Weekly, "w_variety");
        book.RecordOnce(GoalKind.DistinctAnimations, "a");
        book.RecordOnce(GoalKind.DistinctAnimations, "a");
        book.RecordOnce(GoalKind.DistinctAnimations, "b");
        Assert.Equal(2, save.Goals.Daily[0].Progress);
        Assert.Equal(2, save.Goals.Weekly[0].Progress);

        book.Roll(T0.AddDays(1));
        Only(save.Goals.Daily, "d_variety");
        book.RecordOnce(GoalKind.DistinctAnimations, "a");
        Assert.Equal(1, save.Goals.Daily[0].Progress);
        Assert.Equal(2, save.Goals.Weekly[0].Progress);
    }

    [Fact]
    public void NoGoalNeedsCummy()
    {
        Assert.DoesNotContain(GoalCatalog.Daily.Concat(GoalCatalog.Weekly),
            g => g.Kind is GoalKind.LayersGiven or GoalKind.LayersReceived);
    }

    [Fact]
    public void ThereAreEnoughGoalsToPickFrom()
    {
        Assert.True(GoalCatalog.Daily.Count > GoalCatalog.DailyCount);
        Assert.True(GoalCatalog.Weekly.Count > GoalCatalog.WeeklyCount);
    }

    [Fact]
    public void GoalIdsAreUnique()
    {
        var ids = GoalCatalog.Daily.Concat(GoalCatalog.Weekly).Select(g => g.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(GoalCatalog.Achievements.Count, GoalCatalog.Achievements.Select(a => a.Id).Distinct().Count());
    }

    [Fact]
    public void AchievementUnlocksOnceAtItsTarget()
    {
        var (_, book) = Rolled();
        book.Record(GoalKind.Climaxes, 9);
        Assert.DoesNotContain(book.TakeRewards(), r => r.Message.Contains("Worked Up"));
        book.Record(GoalKind.Climaxes);
        Assert.Contains(book.TakeRewards(), r => r.Message.Contains("Worked Up"));
        book.Record(GoalKind.Climaxes);
        Assert.DoesNotContain(book.TakeRewards(), r => r.Message.Contains("Worked Up"));
    }

    [Fact]
    public void AnAchievementStaysEarnedWhenItsNumberDrops()
    {
        var (save, book) = Rolled();
        save.StreakDays = 7;
        Assert.Contains(book.TakeRewards(), r => r.Message.Contains("Habit"));
        save.StreakDays = 1;
        book.TakeRewards();
        Assert.Contains("habit", save.Goals.Achievements);
    }

    [Theory]
    [InlineData("devoted", 50)]
    [InlineData("well_travelled", 10)]
    [InlineData("collector", 50)]
    [InlineData("fallen", 95)]
    public void SaveBasedAchievementsReadTheSave(string id, int target)
    {
        var achievement = GoalCatalog.Achievements.Single(a => a.Id == id);
        Assert.Equal(target, achievement.Target);
        Assert.True(achievement.Progress(new CharacterSave()) < target);
    }

    [Fact]
    public void DevotedCountsScenesWithOnePartner()
    {
        var (save, book) = Rolled();
        for (var i = 0; i < 30; i++) book.RecordPartnerScene("Bob B@W");
        for (var i = 0; i < 30; i++) book.RecordPartnerScene("Cat C@W");
        Assert.Equal(30, GoalCatalog.Achievements.Single(a => a.Id == "devoted").Progress(save));
    }

    [Fact]
    public void GoalsSurviveSaving()
    {
        var dir = Directory.CreateTempSubdirectory("corruption-goals").FullName;
        try
        {
            var path = Path.Combine(dir, "progress.json");
            var file = new ProgressFile();
            var save = file.ForCharacter(1, T0);
            var book = new GoalBook(save);
            book.Roll(T0);
            book.Record(GoalKind.Climaxes, 12);
            book.TakeRewards();
            JsonStore.Save(path, file);
            var loaded = JsonStore.Load<ProgressFile>(path, out _).ForCharacter(1, T0);
            Assert.Equal(12, loaded.Goals.Stats["Climaxes"]);
            Assert.Contains("worked_up", loaded.Goals.Achievements);
            Assert.Equal(save.Goals.Daily.Select(g => g.Id), loaded.Goals.Daily.Select(g => g.Id));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AProfileResetClearsThem()
    {
        var (save, book) = Rolled();
        book.Record(GoalKind.Climaxes, 12);
        book.TakeRewards();
        save.ResetProgress(T0);
        Assert.Empty(save.Goals.Achievements);
        Assert.Empty(save.Goals.Stats);
    }
}
