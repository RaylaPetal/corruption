using Corruption.Core;

namespace Corruption.Tests;

/// How modifiers from a title, a personality and traits combine.
public class ModifierTests
{
    [Fact]
    public void FactorsMultiply()
    {
        var combined = Modifiers.Combine(new Modifiers { Arousal = 1.15 }, new Modifiers { Arousal = 1.2 });
        Assert.Equal(1.38, combined.Arousal, 6);
    }

    [Fact]
    public void CapsTakeTheHigher()
    {
        var combined = Modifiers.Combine(new Modifiers { StreakCap = 1.4 }, new Modifiers { StreakCap = 1.35 }, Modifiers.None);
        Assert.Equal(1.4, combined.StreakCap);
        Assert.Null(Modifiers.Combine(Modifiers.None, Modifiers.None).WatcherCap);
    }

    [Fact]
    public void AddedValuesAdd()
    {
        var combined = Modifiers.Combine(new Modifiers { FatigueGraceMinutes = 5 }, new Modifiers { FatigueGraceMinutes = -3 },
            new Modifiers { CheckInBonus = 10 });
        Assert.Equal(2, combined.FatigueGraceMinutes);
        Assert.Equal(10, combined.CheckInBonus);
    }

    [Fact]
    public void FatigueGraceNeverGoesBelowZero()
    {
        var b = new Balance();
        // Grace is 10 minutes; -20 takes it below 0, which counts as 0.
        Assert.Equal(Progression.Fatigue(TimeSpan.FromMinutes(1.5), b, -10), Progression.Fatigue(TimeSpan.FromMinutes(1.5), b, -20), 6);
        Assert.True(Progression.Fatigue(TimeSpan.FromMinutes(1.5), b, -20) < 1);
    }

    [Fact]
    public void TheLowerManualThresholdThenTheShift()
    {
        var combined = Modifiers.Combine(new Modifiers { ManualClimaxMinimum = 60 }, new Modifiers { ManualClimaxShift = 10 });
        Assert.Equal(60, combined.ManualClimaxMinimum);
        Assert.Equal(10, combined.ManualClimaxShift);
        Assert.Equal(60, Modifiers.Combine(new Modifiers { ManualClimaxMinimum = 60 }, new Modifiers { ManualClimaxMinimum = 65 }).ManualClimaxMinimum);
    }

    [Fact]
    public void FlagsAndPerItemPricesCombine()
    {
        var combined = Modifiers.Combine(
            new Modifiers { PentUp = true, ItemPrice = 0.85, ItemPriceFor = new Dictionary<string, double> { [ItemIds.Contraceptive] = 2 } },
            new Modifiers { StreakEndsOnMiss = true });
        Assert.True(combined.PentUp);
        Assert.True(combined.StreakEndsOnMiss);
        Assert.Equal(1.7, combined.PriceFactorFor(ItemIds.Contraceptive), 6);
        Assert.Equal(0.85, combined.PriceFactorFor(ItemIds.Aphrodisiac), 6);
    }

    [Fact]
    public void OneSourceIsItself()
    {
        var only = new Modifiers { Refractory = 0.5 };
        Assert.Same(only, Modifiers.Combine(only));
        Assert.Same(Modifiers.None, Modifiers.Combine());
    }
}
