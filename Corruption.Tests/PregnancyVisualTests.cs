using Corruption.Core;

namespace Corruption.Tests;

public class PregnancyVisualTests
{
    private static readonly DateTime T0 = EngineRig.T0;

    private static CharacterSave Pregnant()
    {
        var save = new CharacterSave { Setup = new ProfileSetup(), Level = 30 };
        PregnancyBook.Enable(save.Pregnancy, true, false, 4, T0);
        KnownPregnancy.Conceive(save.Pregnancy, T0, "Bob B@W");
        save.Pregnancy.Visuals["Second"] = new TierVisual { Kind = VisualKind.Penumbra, ModDirectory = "belly", Options = { ["Size"] = "Medium" } };
        save.TierVisuals["Tempted"] = new TierVisual { Name = "tempted" };
        return save;
    }

    [Fact]
    public void FollowsTheTrimesterAlongsideTheTier()
    {
        var save = Pregnant();
        Assert.Null(PregnancyVisualPlan.Wanted(save, T0.AddDays(2), false, true));
        Assert.Equal("belly", PregnancyVisualPlan.Wanted(save, T0.AddDays(7), false, true)!.ModDirectory);
        Assert.Equal("tempted", TierVisualPlan.Wanted(save, false)!.Name);
    }

    [Fact]
    public void OffAtBirthAndWhenTurnedOff()
    {
        var save = Pregnant();
        PregnancyBook.Update(save.Pregnancy, T0.AddDays(15));
        Assert.Null(PregnancyVisualPlan.Wanted(save, T0.AddDays(15), false, true));

        var other = Pregnant();
        PregnancyBook.Disable(other.Pregnancy);
        Assert.Null(PregnancyVisualPlan.Wanted(other, T0.AddDays(7), false, true));
    }

    [Fact]
    public void RemovedWhilePausedWhenChosen()
    {
        var save = Pregnant();
        save.Paused = true;
        Assert.NotNull(PregnancyVisualPlan.Wanted(save, T0.AddDays(7), false, true));
        Assert.Null(PregnancyVisualPlan.Wanted(save, T0.AddDays(7), true, true));
    }
}

public class PregnancyBellyTests
{
    private static readonly DateTime T0 = EngineRig.T0;

    private static CharacterSave WithBelly(bool confirmed = true)
    {
        var save = new CharacterSave { Setup = new ProfileSetup(), Level = 30 };
        PregnancyBook.Enable(save.Pregnancy, true, false, 4, T0);
        if (confirmed) KnownPregnancy.Conceive(save.Pregnancy, T0, "Bob B@W");
        else PregnancyBook.Conceive(save.Pregnancy, T0, "Bob B@W");
        save.Pregnancy.Visuals[PregnancyVisualPlan.BellyKey] = new TierVisual { Kind = VisualKind.Penumbra, ModDirectory = "belly" };
        save.Pregnancy.Visuals["Second"] = new TierVisual { Kind = VisualKind.Penumbra, ModDirectory = "second" };
        return save;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(12)]
    public void BellyAllPregnancyWithTrimestersOff(int day)
        => Assert.Equal("belly", PregnancyVisualPlan.Wanted(WithBelly(), T0.AddDays(day), false, false)!.ModDirectory);

    [Fact]
    public void TrimesterBeatsTheBellyWhenOn()
    {
        var save = WithBelly();
        Assert.Equal("belly", PregnancyVisualPlan.Wanted(save, T0.AddDays(1), false, true)!.ModDirectory);
        Assert.Equal("second", PregnancyVisualPlan.Wanted(save, T0.AddDays(7), false, true)!.ModDirectory);
        Assert.Equal("belly", PregnancyVisualPlan.Wanted(save, T0.AddDays(12), false, true)!.ModDirectory);
    }

    [Fact]
    public void TrimestersIgnoredWhenOffWithoutABelly()
    {
        var save = WithBelly();
        save.Pregnancy.Visuals.Remove(PregnancyVisualPlan.BellyKey);
        Assert.Null(PregnancyVisualPlan.Wanted(save, T0.AddDays(7), false, false));
    }

    [Fact]
    public void NoBellyWhileSecret()
        => Assert.Null(PregnancyVisualPlan.Wanted(WithBelly(confirmed: false), T0.AddDays(7), false, false));

    [Fact]
    public void NoBellyAfterBirthOrTurningOff()
    {
        var born = WithBelly();
        PregnancyBook.Update(born.Pregnancy, T0.AddDays(15));
        Assert.Null(PregnancyVisualPlan.Wanted(born, T0.AddDays(15), false, false));

        var off = WithBelly();
        PregnancyBook.Disable(off.Pregnancy);
        Assert.Null(PregnancyVisualPlan.Wanted(off, T0.AddDays(7), false, false));
    }

    [Fact]
    public void BellyRemovedWhilePausedWhenChosen()
    {
        var save = WithBelly();
        save.Paused = true;
        Assert.NotNull(PregnancyVisualPlan.Wanted(save, T0.AddDays(7), false, false));
        Assert.Null(PregnancyVisualPlan.Wanted(save, T0.AddDays(7), true, false));
    }
}

public class PregnancyMoodleTests
{
    private static MoodlesInput Base => new(true, 0, 0, false, 0, false, false, false);

    [Fact]
    public void PregnancyStatuses()
    {
        var plan = MoodlesPlan.Plan(Base with { Trimester = 2, Craving = true, Unsatisfied = true });
        Assert.Equal(2, plan[MoodleKind.Pregnant]);
        Assert.True(plan.ContainsKey(MoodleKind.Craving));
        Assert.True(plan.ContainsKey(MoodleKind.Unsatisfied));
    }

    [Fact]
    public void FertileAndRecovering()
    {
        Assert.True(MoodlesPlan.Plan(Base with { Fertile = true }).ContainsKey(MoodleKind.Fertile));
        Assert.True(MoodlesPlan.Plan(Base with { Recovering = true }).ContainsKey(MoodleKind.Recovering));
    }

    [Fact]
    public void HiddenShowsNoPregnancy()
        => Assert.Empty(MoodlesPlan.Plan(Base with { Visible = false, Trimester = 3, Fertile = true }));

    [Theory]
    [InlineData("Bob Smith", "Carrying Bob Smith's child.")]
    [InlineData(" Bob Smith ", "Carrying Bob Smith's child.")]
    [InlineData(null, "Carrying a child.")]
    [InlineData("", "Carrying a child.")]
    [InlineData("  ", "Carrying a child.")]
    public void PregnantDescriptionNamesTheFather(string? father, string expected)
        => Assert.Equal(expected, MoodlesPlan.PregnantDescription(father));

    private static PregnancyState Carrier()
    {
        var p = new PregnancyState();
        PregnancyBook.Enable(p, true, false, 4, EngineRig.T0);
        return p;
    }

    [Fact]
    public void FatherIsTheNameWithoutTheWorld()
    {
        var p = Carrier();
        KnownPregnancy.Conceive(p, EngineRig.T0, "Bob Smith@Balmung");
        var input = MoodlesPlan.WithPregnancy(Base, p, EngineRig.T0.AddDays(1));
        Assert.Equal("Bob Smith", input.Father);
        Assert.DoesNotContain("Balmung", MoodlesPlan.PregnantDescription(input.Father));
    }

    [Fact]
    public void BareFatherNameFromAPlugin()
    {
        var p = Carrier();
        KnownPregnancy.Conceive(p, EngineRig.T0, "Bob Smith");
        Assert.Equal("Bob Smith", MoodlesPlan.WithPregnancy(Base, p, EngineRig.T0.AddDays(1)).Father);
    }

    [Fact]
    public void EmptyFatherKeyGivesNoName()
    {
        var p = Carrier();
        KnownPregnancy.Conceive(p, EngineRig.T0, "");
        Assert.Null(MoodlesPlan.WithPregnancy(Base, p, EngineRig.T0.AddDays(1)).Father);
    }

    [Fact]
    public void SecretPregnancyShowsNoFather()
    {
        var p = Carrier();
        PregnancyBook.Conceive(p, EngineRig.T0, "Bob Smith@Balmung");
        var input = MoodlesPlan.WithPregnancy(Base, p, EngineRig.T0.AddDays(1));
        Assert.Null(input.Father);
        Assert.False(MoodlesPlan.Plan(input).ContainsKey(MoodleKind.Pregnant));
    }
}
