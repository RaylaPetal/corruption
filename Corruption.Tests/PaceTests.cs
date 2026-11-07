using Corruption.Core;

namespace Corruption.Tests;

/// The shipped defaults: how fast a plain scene goes.
public class PaceTests
{
    private static EngineRig Quiet()
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon());
        foreach (var achievement in GoalCatalog.Achievements) r.Save.Goals.Achievements.Add(achievement.Id);
        r.Save.SeenPartners.Add(r.Bob.Key);
        r.Save.SeenActs.Add(r.Act.Key);
        return r;
    }

    [Fact]
    public void FirstClimaxTakesAboutElevenMinutes()
    {
        var r = Quiet();
        var seconds = 0;
        while (!r.Host.Notes.Contains("You climaxed.") && seconds < 3600)
        {
            r.Run(1);
            seconds++;
        }
        Assert.InRange(seconds / 60.0, 10, 12.5);
    }

    [Fact]
    public void ClimaxesPayLessThanTicksOverAnHour()
    {
        var r = Quiet();
        var other = new ActEntry { ModDirectory = "m", Option = "other", EmoteMode = 50 };
        r.Save.SeenActs.Add(other.Key);
        var first = r.Act;
        var xp = 0.0;
        var lastXp = r.Save.Xp;
        var lastLevel = r.Save.Level;

        for (var second = 0; second < 3600; second++)
        {
            // Change animation every 10 minutes, as people do, so fatigue doesn't hide the comparison.
            r.Act = second / 600 % 2 == 0 ? first : other;
            if (second > 0 && second % 660 == 0) r.BobTag = r.BobTag with { ClimaxCount = r.BobTag.ClimaxCount + 1 };
            r.Run(1);
            xp += Gained(r, ref lastXp, ref lastLevel);
        }

        var own = r.Host.Notes.Count(n => n == "You climaxed.");
        var theirs = r.Host.Notes.Count(n => n == "Bob B climaxed.");
        var events = own * r.Balance.ClimaxXp + theirs * r.Balance.ClimaxXp * r.Balance.PartnerClimaxShare
                     + r.Host.Layers.Count(l => true) * r.Balance.LayerXp;
        var ticks = xp - events;
        Assert.InRange(own, 4, 7);
        Assert.True(events < ticks, $"events {events:0} vs ticks {ticks:0}");
    }

    [Fact]
    public void GoalsAndTheBestTitleStillPayLessThanTicks()
    {
        var r = Quiet();
        // Every goal at once, so the hour completes all it can; and the best tick-XP title.
        r.Save.Goals.Daily = GoalCatalog.Daily.Select(g => new GoalState { Id = g.Id }).ToList();
        r.Save.Goals.Weekly = GoalCatalog.Weekly.Select(g => new GoalState { Id = g.Id }).ToList();
        r.Save.Titles.Unlocked.Add("star_10");
        r.Save.Titles.Equipped = "star_10";
        var other = new ActEntry { ModDirectory = "m", Option = "other", EmoteMode = 50 };
        r.Save.SeenActs.Add(other.Key);
        var first = r.Act;
        var xp = 0.0;
        var lastXp = r.Save.Xp;
        var lastLevel = r.Save.Level;

        for (var second = 0; second < 3600; second++)
        {
            r.Act = second / 600 % 2 == 0 ? first : other;
            if (second > 0 && second % 660 == 0) r.BobTag = r.BobTag with { ClimaxCount = r.BobTag.ClimaxCount + 1 };
            r.Run(1);
            xp += Gained(r, ref lastXp, ref lastLevel);
        }

        var own = r.Host.Notes.Count(n => n == "You climaxed.");
        var theirs = r.Host.Notes.Count(n => n == "Bob B climaxed.");
        var goals = r.Host.Celebrated.Where(c => c.Kind is RewardKind.Daily or RewardKind.Weekly).Sum(c => c.Xp);
        var events = own * r.Balance.ClimaxXp + theirs * r.Balance.ClimaxXp * r.Balance.PartnerClimaxShare
                     + r.Host.Layers.Count * r.Balance.LayerXp + goals;
        var ticks = xp - events;
        Assert.True(goals > 0, "some goals should complete in an hour");
        Assert.Equal(1.06, r.Engine.Status.Factors.Title, 6);
        Assert.True(events < ticks, $"events {events:0} (goals {goals:0}) vs ticks {ticks:0}");
    }

    /// XP gained since the last call, counting whole levels.
    private static double Gained(EngineRig r, ref double lastXp, ref int lastLevel)
    {
        var gained = r.Save.Xp - lastXp;
        for (var level = lastLevel; level < r.Save.Level; level++) gained += Progression.XpToNext(level, r.Balance);
        lastXp = r.Save.Xp;
        lastLevel = r.Save.Level;
        return gained;
    }
}
