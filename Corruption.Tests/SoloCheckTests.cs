using Corruption.Core;

namespace Corruption.Tests;

/// When a scene is solo, and the daily budget.
public class SoloCheckTests
{
    private const double Range = 3;

    /// A known act with nobody nearby.
    private static SceneInput Alone(bool solo = true, bool budget = true)
        => new(false, true, false, 0, null, null, SoloAct: solo, SoloBudgetLeft: budget);

    [Fact]
    public void ASoloActAloneIsSolo() => Assert.Equal((SceneFailure.None, true), SceneCheck.Check(Alone(), Range));

    [Fact]
    public void ADuoActAloneStillFails() => Assert.Equal((SceneFailure.NoPartner, false), SceneCheck.Check(Alone(solo: false), Range));

    [Theory]
    [InlineData(4, "51.0", "51.0")]
    [InlineData(1, null, null)]
    [InlineData(1, "51.0", "52.0")]
    public void EveryPartnerFailureFallsBackToSolo(double distance, string? declared, string? seen)
    {
        PoseKey? Pose(string? s) => s is null ? null : new PoseKey(uint.Parse(s.Split('.')[0]), byte.Parse(s.Split('.')[1]));
        var input = new SceneInput(false, true, true, distance, Pose(declared), Pose(seen), SoloAct: true);
        Assert.Equal((SceneFailure.None, true), SceneCheck.Check(input, Range));
    }

    [Fact]
    public void AQualifyingPartnerMakesItDuo()
    {
        var input = new SceneInput(false, true, true, 1, new PoseKey(51, 0), new PoseKey(51, 0), SoloAct: true);
        Assert.Equal((SceneFailure.None, false), SceneCheck.Check(input, Range));
    }

    [Theory]
    [InlineData(true, false, false, SceneFailure.NeedsSetup)]
    [InlineData(false, true, false, SceneFailure.Paused)]
    [InlineData(false, false, true, SceneFailure.Hidden)]
    public void SetupPauseAndHidingStillWin(bool setup, bool paused, bool hidden, SceneFailure failure)
    {
        var input = Alone() with { NeedsSetup = setup, Paused = paused, Hidden = hidden };
        Assert.Equal((failure, false), SceneCheck.Check(input, Range));
    }

    [Fact]
    public void NoActIsNoScene()
        => Assert.Equal((SceneFailure.NoKnownAct, false), SceneCheck.Check(Alone() with { HasKnownAct = false }, Range));

    [Fact]
    public void ASpentBudgetStopsIt()
        => Assert.Equal((SceneFailure.SoloBudgetSpent, false), SceneCheck.Check(Alone(budget: false), Range));

    [Fact]
    public void EvaluateStillGivesTheFailure()
    {
        Assert.Equal(SceneFailure.None, SceneCheck.Evaluate(Alone(), Range));
        Assert.Equal("Your solo time for today is used up", SceneCheck.Describe(SceneFailure.SoloBudgetSpent));
    }

    // ── The budget ──────────────────────────────────────────────────────────

    private static readonly DateTime T0 = EngineRig.T0;

    [Fact]
    public void FortyFiveMinutesADay()
        => Assert.Equal(45 * 60, SoloBook.SecondsLeft(new CharacterSave(), T0, Modifiers.None));

    [Fact]
    public void SpendingTakesFromIt()
    {
        var save = new CharacterSave();
        SoloBook.Spend(save, T0, 30 * 60);
        Assert.Equal(15 * 60, SoloBook.SecondsLeft(save, T0.AddHours(1), Modifiers.None));
        SoloBook.Spend(save, T0, 20 * 60);
        Assert.Equal(0, SoloBook.SecondsLeft(save, T0.AddHours(1), Modifiers.None));
    }

    [Fact]
    public void ANewDayRestoresIt()
    {
        var save = new CharacterSave();
        SoloBook.Spend(save, T0, 45 * 60);
        save.SoloBonusSecondsToday = 600;
        Assert.Equal(45 * 60, SoloBook.SecondsLeft(save, T0.AddDays(1), Modifiers.None));
        Assert.Equal(0, save.SoloBonusSecondsToday);
    }

    [Fact]
    public void TitlesAndSignsAddToIt()
    {
        var save = new CharacterSave { SoloDayUtc = T0.Date, SoloBonusSecondsToday = 20 * 60 };
        Assert.Equal((45 + 15) * 60 + 20 * 60, SoloBook.SecondsLeft(save, T0, new Modifiers { SoloBudgetMinutes = 15 }));
    }

    [Fact]
    public void AResetClearsIt()
    {
        var save = new CharacterSave { Setup = new ProfileSetup() };
        SoloBook.Spend(save, T0, 600);
        save.ResetProgress(T0);
        Assert.Equal(0, save.SoloSecondsToday);
    }
}
