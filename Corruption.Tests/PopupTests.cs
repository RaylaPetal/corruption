using Corruption.Core;

namespace Corruption.Tests;

/// Which popups the engine raises, and in what order.
public class PopupTests
{
    private static EngineRig Plain()
    {
        var r = new EngineRig();
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        return r;
    }

    [Fact]
    public void LevelTenGivesATierPopupThenItsTitle()
    {
        var r = Plain();
        r.Save.Level = 9;
        r.Save.Xp = Progression.XpToNext(9, r.Balance) - 1;
        r.Run(11);

        var kinds = r.Host.Celebrated.Select(c => (c.Kind, c.Title)).ToList();
        var tier = kinds.IndexOf((RewardKind.Tier, "Curious"));
        var title = kinds.IndexOf((RewardKind.Title, "Wanton"));
        Assert.True(tier >= 0 && title > tier, string.Join(", ", kinds));
        Assert.Equal("Scene XP ×1.01", r.Host.Celebrated[title].Detail);
    }

    [Fact]
    public void APlainLevelUpHasNoPopup()
    {
        var r = Plain();
        r.Save.Level = 11;
        r.Run(1);
        r.Save.Xp = Progression.XpToNext(11, r.Balance) - 1;
        r.Run(10);
        Assert.Equal(12, r.Save.Level);
        Assert.Empty(r.Host.Celebrated);
    }

    [Fact]
    public void TheFirstSyncHasNoTitlePopups()
    {
        var r = Plain();
        r.Save.Level = 60;
        r.Run(3);
        Assert.DoesNotContain(r.Host.Celebrated, c => c.Kind == RewardKind.Title);
        Assert.True(TitleBook.IsUnlocked(r.Save, "insatiable"));
    }

    [Fact]
    public void AnAchievementsTitleFollowsIt()
    {
        var r = new EngineRig();
        r.Run(1);
        r.Save.Goals.Stats["CrowdSeconds"] = 3600;
        r.Run(1);
        var kinds = r.Host.Celebrated.Select(c => (c.Kind, c.Title)).ToList();
        var achievement = kinds.IndexOf((RewardKind.Achievement, "Shameless"));
        var title = kinds.IndexOf((RewardKind.Title, "Shameless"));
        Assert.True(achievement >= 0 && title > achievement, string.Join(", ", kinds));
    }

    [Theory]
    [InlineData(30, false)]
    [InlineData(59, false)]
    [InlineData(60, true)]
    public void OnlyScenesOfAMinuteGetAPopup(double seconds, bool popup)
        => Assert.Equal(popup, SceneSummaries.WorthAPopup(new SceneSummary { VerifiedSeconds = seconds }));

    [Fact]
    public void TheSummaryReadsWell()
    {
        var summary = new SceneSummary
        {
            VerifiedSeconds = 14 * 60 + 20, Xp = 412.4, Sins = 18, OwnClimaxes = 1, PartnerClimaxes = 2, PeakWatchers = 3,
            Partners = ["Bob B@W", "Carol C@W"],
        };
        Assert.Equal(
            ["14:20 · +412 XP · +18 Sins", "1 climax · 2 partner climaxes · up to 3 watchers", "With Bob B, Carol C"],
            SceneSummaries.Lines(summary));
        var reward = SceneSummaries.Reward(summary);
        Assert.Equal(RewardKind.Scene, reward.Kind);
        Assert.Equal(0, reward.Xp);
    }

    [Fact]
    public void AQuietSceneLeavesOutWhatDidntHappen()
        => Assert.Equal(["1:05 · +6 XP · +1 Sins", "With Bob B"],
            SceneSummaries.Lines(new SceneSummary { VerifiedSeconds = 65, Xp = 6, Sins = 1, Partners = ["Bob B@W"] }));
}
