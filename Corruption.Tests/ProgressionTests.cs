using Corruption.Core;

namespace Corruption.Tests;

public class ProgressionTests
{
    private static readonly Balance B = new();
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1, 110)]
    [InlineData(10, 416)]
    [InlineData(50, 3636)]
    [InlineData(99, 9950)]
    public void LevelCurve(int level, long xp) => Assert.Equal(xp, Progression.XpToNext(level, B));

    [Fact]
    public void AnAwardCanSpanSeveralLevelsAndCarries()
    {
        var save = new CharacterSave();
        var result = Progression.AddXp(save, 110 + 128 + 5, B);
        Assert.Equal(3, save.Level);
        Assert.Equal(2, result.LevelsGained);
        Assert.Equal(5, save.Xp, 6);
    }

    [Fact]
    public void CapHoldsAndStoresNoXp()
    {
        var save = new CharacterSave { Level = 99 };
        Progression.AddXp(save, 1_000_000, B);
        Assert.Equal(100, save.Level);
        Assert.Equal(0, save.Xp);
        Assert.Equal(default, Progression.AddXp(save, 500, B));
        Assert.Equal(100, save.Level);
        Assert.Equal(0, save.Xp);
    }

    [Theory]
    [InlineData(1, Tier.Pure)]
    [InlineData(9, Tier.Pure)]
    [InlineData(10, Tier.Curious)]
    [InlineData(24, Tier.Curious)]
    [InlineData(25, Tier.Tempted)]
    [InlineData(40, Tier.Tainted)]
    [InlineData(60, Tier.Corrupted)]
    [InlineData(80, Tier.Depraved)]
    [InlineData(95, Tier.Fallen)]
    [InlineData(100, Tier.Fallen)]
    public void Tiers(int level, Tier tier) => Assert.Equal(tier, Progression.TierOf(level));

    [Fact]
    public void CrossingATierIsReported()
    {
        var save = new CharacterSave { Level = 9 };
        var result = Progression.AddXp(save, Progression.XpToNext(9, B), B);
        Assert.Equal(Tier.Curious, result.NewTier);
    }

    [Fact]
    public void LevelInsideATierReportsNoTier()
    {
        var save = new CharacterSave { Level = 10 };
        Assert.Null(Progression.AddXp(save, Progression.XpToNext(10, B), B).NewTier);
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(10, 1.0)]
    [InlineData(10.9, 1.0)]
    [InlineData(11, 0.9)]
    [InlineData(12, 0.8)]
    [InlineData(18, 0.2)]
    [InlineData(30, 0.2)]
    public void Fatigue(double minutes, double expected)
        => Assert.Equal(expected, Progression.Fatigue(TimeSpan.FromMinutes(minutes), B), 6);

    private static CharacterSave Fading(int level, double xp, DecayMode decay, double daysAway)
        => new() { Level = level, Xp = xp, Setup = new ProfileSetup { Decay = decay }, LastVerifiedUtc = T0.AddDays(-daysAway) };

    [Fact]
    public void NoFadingInsideTheGrace()
    {
        var save = Fading(30, 500, DecayMode.Normal, 2.9);
        Progression.ApplyFading(save, T0, B);
        Assert.Equal(500, save.Xp);
    }

    [Fact]
    public void FadingTakesAShareOfTheLevelPerDay()
    {
        var save = Fading(30, 1000, DecayMode.Normal, 5);
        Progression.ApplyFading(save, T0, B);
        Assert.Equal(1000 - 2 * 0.05 * Progression.XpToNext(30, B), save.Xp, 6);
    }

    [Fact]
    public void FadingIsOnlyAppliedOnce()
    {
        var save = Fading(30, 1000, DecayMode.Normal, 5);
        Progression.ApplyFading(save, T0, B);
        var after = save.Xp;
        Progression.ApplyFading(save, T0.AddHours(2), B);
        Assert.Equal(after, save.Xp);
    }

    [Fact]
    public void FadingStopsAtTheStartOfTheTier()
    {
        var save = Fading(30, 100, DecayMode.Harsh, 1000);
        Assert.True(Progression.ApplyFading(save, T0, B));
        Assert.Equal(25, save.Level);
        Assert.Equal(0, save.Xp);
    }

    [Fact]
    public void DecayOffNeverFades()
    {
        var save = Fading(30, 100, DecayMode.Off, 1000);
        Assert.False(Progression.ApplyFading(save, T0, B));
        Assert.False(Progression.IsFading(save, T0));
        Assert.Equal(100, save.Xp);
    }

    [Fact]
    public void NoSetupNeverFades() => Assert.False(Progression.ApplyFading(new CharacterSave { Level = 30, Xp = 5, LastVerifiedUtc = T0.AddDays(-99) }, T0, B));

    [Theory]
    [InlineData(1, 1)]
    [InlineData(9, 1)]
    [InlineData(24, 10)]
    [InlineData(60, 60)]
    [InlineData(100, 95)]
    public void TierFloors(int level, int floor) => Assert.Equal(floor, Progression.TierFloor(level));

    [Fact]
    public void NoBonusesLeavesFactorsAtOne()
        => Assert.Equal(1, Progression.Factors(1, null, 0, 1, 1, B).Total, 6);

    [Fact]
    public void OwnTierRaisesXp()
        => Assert.Equal(1.3, Progression.Factors(100, null, 0, 1, 1, B).Level, 6);

    [Theory]
    [InlineData(1, 30, 1.2)]
    [InlineData(1, 100, 1.3)]
    [InlineData(30, 1, 1.0)]
    [InlineData(30, 30, 1.0)]
    public void HigherTierPartnerRaisesXp(int own, int partner, double factor)
        => Assert.Equal(factor, Progression.Factors(own, partner, 0, 1, 1, B).Partner, 6);

    [Fact]
    public void UnknownPartnerGivesNoBonus()
        => Assert.Equal(1, Progression.Factors(1, null, 0, 1, 1, B).Partner, 6);

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(2, 1.2)]
    [InlineData(5, 1.5)]
    [InlineData(50, 1.5)]
    public void WatchersRaiseXpUpToTheCap(int watchers, double factor)
        => Assert.Equal(factor, Progression.Factors(1, null, watchers, 1, 1, B).Watchers, 6);

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 1.0)]
    [InlineData(3, 1.1)]
    [InlineData(7, 1.3)]
    [InlineData(30, 1.3)]
    public void StreakRaisesXpUpToTheCap(int days, double factor)
        => Assert.Equal(factor, Progression.Factors(1, null, 0, days, 1, B).Streak, 6);

    [Fact]
    public void FactorsMultiply()
        => Assert.Equal(1.05 * 1.1 * 1.2 * 1.05 * 0.8, new TickFactors(1.05, 1.1, 1.2, 1.05, 0.8).Total, 6);

    [Fact]
    public void StreakGrowsOncePerDay()
    {
        var save = new CharacterSave();
        Progression.UpdateStreak(save, T0);
        Progression.UpdateStreak(save, T0.AddHours(3));
        Assert.Equal(1, save.StreakDays);
        Progression.UpdateStreak(save, T0.AddDays(1));
        Assert.Equal(2, save.StreakDays);
    }

    [Fact]
    public void MissingADayHalvesTheStreakInsteadOfEndingIt()
    {
        var save = new CharacterSave { StreakDays = 6, LastSceneDayUtc = T0 };
        Progression.UpdateStreak(save, T0.AddDays(2));
        Assert.Equal(4, save.StreakDays);
    }

    [Fact]
    public void ResetClearsTheStreak()
    {
        var save = new CharacterSave { StreakDays = 6, LastSceneDayUtc = T0 };
        save.ResetProgress(T0);
        Assert.Equal(0, save.StreakDays);
    }

    [Fact]
    public void FractionsCarryTowardLaterTicks()
    {
        var save = new CharacterSave();
        for (var i = 0; i < 10; i++) Progression.AddXp(save, 6.6, B);
        Assert.Equal(66, save.Xp, 6);
    }

    [Fact]
    public void ArousalGainAtPure() => Assert.Equal(3, Arousal.Gain(Tier.Pure, 0, false, B), 6);

    [Fact]
    public void ArousalGainAtFallen() => Assert.Equal(3.9, Arousal.Gain(Tier.Fallen, 0, false, B), 6);

    [Fact]
    public void WatchersRaiseArousal() => Assert.Equal(3.6, Arousal.Gain(Tier.Pure, 2, false, B), 6);

    [Fact]
    public void RefractoryHalvesGain() => Assert.Equal(1.5, Arousal.Gain(Tier.Pure, 0, true, B), 6);

    [Fact]
    public void DecayAfterFiveMinutes()
    {
        var save = new CharacterSave { Arousal = 40, ArousalAnchorUtc = T0 };
        Arousal.ApplyDecay(save, T0.AddMinutes(5), B);
        Assert.InRange(save.Arousal, 30, 31);
    }

    [Fact]
    public void DecayCoversOfflineTimeAndStopsAtZero()
    {
        var save = new CharacterSave { Arousal = 40, ArousalAnchorUtc = T0 };
        Arousal.ApplyDecay(save, T0.AddHours(8), B);
        Assert.Equal(0, save.Arousal);
    }

    [Fact]
    public void DecayKeepsPartFinishedSteps()
    {
        var save = new CharacterSave { Arousal = 40, ArousalAnchorUtc = T0 };
        for (var s = 1; s <= 45; s++) Arousal.ApplyDecay(save, T0.AddSeconds(s), B);
        Assert.Equal(39, save.Arousal);
        Arousal.ApplyDecay(save, T0.AddSeconds(60), B);
        Assert.Equal(38, save.Arousal);
    }
}

public class WatcherCheckTests
{
    private static readonly Balance B = new();
    private static readonly System.Numerics.Vector3 Scene = System.Numerics.Vector3.Zero;

    // Rotation 0 faces +Z.
    private static readonly System.Numerics.Vector3 South = new(0, 0, -5);

    [Fact]
    public void CloseAndFacingIsWatching() => Assert.True(WatcherCheck.IsWatching(South, 0, false, Scene, B));

    [Fact]
    public void CloseButFacingAwayIsNot() => Assert.False(WatcherCheck.IsWatching(South, MathF.PI, false, Scene, B));

    [Fact]
    public void FacingButFarIsNot()
        => Assert.False(WatcherCheck.IsWatching(new System.Numerics.Vector3(0, 0, -12), 0, false, Scene, B));

    [Fact]
    public void TargetingCountsFromFarAndFacingAway()
        => Assert.True(WatcherCheck.IsWatching(new System.Numerics.Vector3(0, 0, -12), MathF.PI, true, Scene, B));

    [Fact]
    public void BeyondTheRadiusNothingCounts()
        => Assert.False(WatcherCheck.IsWatching(new System.Numerics.Vector3(0, 0, -16), 0, true, Scene, B));
}

public class BalanceTests : IDisposable
{
    private readonly string dir = Directory.CreateTempSubdirectory("corruption-balance").FullName;

    public void Dispose() => Directory.Delete(dir, recursive: true);

    [Fact]
    public void ShippedFileMatchesTheDefaults()
    {
        var shipped = Path.Combine(AppContext.BaseDirectory, "balance.json");
        var loaded = Balance.Load(shipped, out var warning);
        Assert.Null(warning);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(new Balance()), System.Text.Json.JsonSerializer.Serialize(loaded));
    }

    [Fact]
    public void ValuesAreRead()
    {
        var path = Path.Combine(dir, "b.json");
        File.WriteAllText(path, """{ "LevelCap": 50, "TickXp": 3 }""");
        var loaded = Balance.Load(path, out var warning);
        Assert.Null(warning);
        Assert.Equal(50, loaded.LevelCap);
    }

    [Fact]
    public void MissingFileGivesDefaultsAndAWarning()
    {
        Assert.Equal(100, Balance.Load(Path.Combine(dir, "none.json"), out var warning).LevelCap);
        Assert.NotNull(warning);
    }

    [Theory]
    [InlineData("{ broken")]
    [InlineData("""{ "TickXp": 0 }""")]
    [InlineData("""{ "TickSeconds": 0 }""")]
    public void InvalidFileGivesDefaultsAndAWarning(string content)
    {
        var path = Path.Combine(dir, "b.json");
        File.WriteAllText(path, content);
        Assert.Equal(100, Balance.Load(path, out var warning).LevelCap);
        Assert.NotNull(warning);
    }
}
