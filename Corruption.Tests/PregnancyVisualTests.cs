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
        Assert.Null(PregnancyVisualPlan.Wanted(save, T0.AddDays(2), false));
        Assert.Equal("belly", PregnancyVisualPlan.Wanted(save, T0.AddDays(7), false)!.ModDirectory);
        Assert.Equal("tempted", TierVisualPlan.Wanted(save, false)!.Name);
    }

    [Fact]
    public void OffAtBirthAndWhenTurnedOff()
    {
        var save = Pregnant();
        PregnancyBook.Update(save.Pregnancy, T0.AddDays(15));
        Assert.Null(PregnancyVisualPlan.Wanted(save, T0.AddDays(15), false));

        var other = Pregnant();
        PregnancyBook.Disable(other.Pregnancy);
        Assert.Null(PregnancyVisualPlan.Wanted(other, T0.AddDays(7), false));
    }

    [Fact]
    public void RemovedWhilePausedWhenChosen()
    {
        var save = Pregnant();
        save.Paused = true;
        Assert.NotNull(PregnancyVisualPlan.Wanted(save, T0.AddDays(7), false));
        Assert.Null(PregnancyVisualPlan.Wanted(save, T0.AddDays(7), true));
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
}
