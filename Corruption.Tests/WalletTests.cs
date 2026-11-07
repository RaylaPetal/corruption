using Corruption.Core;

namespace Corruption.Tests;

public class WalletTests
{
    private static readonly DateTime Day = EngineRig.T0;

    [Fact]
    public void CheckInStreakGrowsToTheCap()
    {
        var wallet = new Wallet();
        int[] expected = [20, 25, 30, 35, 40, 45, 50, 50];
        for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], Sins.CheckIn(wallet, Day.AddDays(i).AddHours(5)));
        Assert.Equal(expected.Sum(), wallet.Sins);
    }

    [Fact]
    public void SameDayPaysOnce()
    {
        var wallet = new Wallet();
        Sins.CheckIn(wallet, Day.AddHours(1));
        Assert.Equal(0, Sins.CheckIn(wallet, Day.AddHours(23)));
        Assert.Equal(20, wallet.Sins);
    }

    [Fact]
    public void MissingADayStartsOver()
    {
        var wallet = new Wallet();
        Sins.CheckIn(wallet, Day);
        Sins.CheckIn(wallet, Day.AddDays(1));
        Assert.Equal(20, Sins.CheckIn(wallet, Day.AddDays(3)));
        Assert.Equal(1, wallet.CheckInStreak);
    }

    [Fact]
    public void SceneMinutesCarryParts()
    {
        var wallet = new Wallet();
        for (var tick = 0; tick < 36; tick++) Sins.ForScene(wallet, 10);
        Assert.Equal(6, wallet.Sins);
        Sins.ForScene(wallet, 50);
        Assert.Equal(6, wallet.Sins);
        Sins.ForScene(wallet, 10);
        Assert.Equal(7, wallet.Sins);
    }

    [Theory]
    [InlineData(2, 5)]
    [InlineData(26, 15)]
    [InlineData(95, 35)]
    public void LevelPays(int level, int sins) => Assert.Equal(sins, Sins.ForLevel(level));

    [Theory]
    [InlineData(RewardKind.Daily, 15)]
    [InlineData(RewardKind.Weekly, 60)]
    [InlineData(RewardKind.Achievement, 100)]
    public void RewardsPay(RewardKind kind, int sins) => Assert.Equal(sins, Sins.ForReward(kind));

    [Fact]
    public void SpendingNeedsEnough()
    {
        var wallet = new Wallet { Sins = 30 };
        Assert.False(Sins.TrySpend(wallet, 40));
        Assert.Equal(30, wallet.Sins);
        Assert.True(Sins.TrySpend(wallet, 30));
        Assert.Equal((0, 30), (wallet.Sins, wallet.SpentTotal));
    }

    [Fact]
    public void EngineChecksInScenesAndLevels()
    {
        var r = new EngineRig();
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Run(61);
        Assert.Contains(r.Host.Notes, n => n == "Daily check-in: +20 Sins.");
        // 20 check-in, 6 ticks = 1 minute, and the first-time XP takes level 1 to 3 or so.
        var levelSins = Enumerable.Range(2, r.Save.Level - 1).Sum(Sins.ForLevel);
        Assert.Equal(20 + 1 + levelSins, r.Save.Wallet.Sins);
    }

    [Fact]
    public void GoalsPaySinsToo()
    {
        var r = new EngineRig();
        r.Run(25);
        Assert.Contains(r.Host.Notes, n => n.Contains("First Taste") && n.Contains("+100 Sins"));
    }

    [Fact]
    public void NothingBeforeSetup()
    {
        var r = new EngineRig();
        r.Save.Setup = null;
        r.Run(120);
        Assert.Equal(0, r.Save.Wallet.Sins);
    }

    [Fact]
    public void ResetEmptiesTheWallet()
    {
        var save = new CharacterSave { Wallet = { Sins = 300, CheckInStreak = 4 } };
        save.ResetProgress(Day);
        Assert.Equal(0, save.Wallet.Sins);
        Assert.Equal(0, save.Wallet.CheckInStreak);
    }

    [Fact]
    public void PrestigeKeepsSins()
    {
        var save = new CharacterSave { Setup = new ProfileSetup(), Level = 100, Wallet = { Sins = 300 } };
        PrestigeRules.Apply(save, Day, new Balance());
        Assert.Equal(300, save.Wallet.Sins);
    }
}
