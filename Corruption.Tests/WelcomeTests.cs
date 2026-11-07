using Corruption.Core;

namespace Corruption.Tests;

public class WelcomeTests
{
    private static readonly PregnancyChoice Off = new(false, true, false, 3, 1);

    private static EngineRig Fresh()
    {
        var r = new EngineRig { Playing = false };
        r.Save.Setup = null;
        return r;
    }

    [Fact]
    public void NeededUntilFinishedAndAgainAfterAReset()
    {
        var r = Fresh();
        Assert.True(Welcome.IsNeeded(r.Save));
        Assert.Null(r.Engine.CompleteWelcome(new WelcomeChoices(new ProfileSetup(), Anatomy.Vagina, Off), r.Now));
        Assert.False(Welcome.IsNeeded(r.Save));
        r.Engine.ResetProgress(r.Now);
        Assert.True(Welcome.IsNeeded(r.Save));
    }

    [Fact]
    public void ExistingCharactersSeeItOnce()
    {
        var r = new EngineRig();
        Assert.True(Welcome.IsNeeded(r.Save));
        Assert.Null(r.Engine.CompleteWelcome(new WelcomeChoices(null, Anatomy.Penis, Off), r.Now));
        Assert.False(Welcome.IsNeeded(r.Save));
    }

    [Fact]
    public void AppliesEverything()
    {
        var r = Fresh();
        var setup = new ProfileSetup { Difficulty = Difficulty.Hard, Decay = DecayMode.Off, AutoClimax = false, ArousalGain = 1.5 };
        Assert.Null(r.Engine.CompleteWelcome(new WelcomeChoices(setup, Anatomy.Both, new PregnancyChoice(true, true, true, 4, 2)), r.Now));
        Assert.Same(setup, r.Save.Setup);
        Assert.Equal(Anatomy.Both, r.Save.Anatomy);
        Assert.True(r.Save.Pregnancy.Enabled && r.Save.Pregnancy.CanCarry && r.Save.Pregnancy.CanBreed);
        Assert.Equal(4, CycleClock.DayAt(r.Save.Pregnancy.CycleStartUtc, r.Now));
        Assert.Equal(2, r.Save.Pregnancy.ChanceFactor);
    }

    [Fact]
    public void TheLockedProfileIsntChanged()
    {
        var r = new EngineRig();
        var before = r.Save.Setup;
        r.Engine.CompleteWelcome(new WelcomeChoices(new ProfileSetup { Difficulty = Difficulty.Relaxed }, Anatomy.NotSet, Off), r.Now);
        Assert.Same(before, r.Save.Setup);
    }

    [Fact]
    public void RefusesAnIncompleteChoiceAndChangesNothing()
    {
        var r = Fresh();
        Assert.NotNull(r.Engine.CompleteWelcome(new WelcomeChoices(null, Anatomy.Penis, Off), r.Now));
        Assert.NotNull(r.Engine.CompleteWelcome(new WelcomeChoices(new ProfileSetup(), Anatomy.Penis, new PregnancyChoice(true, false, false, 3, 1)), r.Now));
        Assert.Null(r.Save.Setup);
        Assert.Equal(Anatomy.NotSet, r.Save.Anatomy);
    }

    [Fact]
    public void TurningPregnancyOffEndsIt()
    {
        var r = new EngineRig();
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, 3, r.Now);
        r.Engine.CompleteWelcome(new WelcomeChoices(null, Anatomy.Vagina, Off), r.Now);
        Assert.False(r.Save.Pregnancy.Enabled);
    }

    [Fact]
    public void APregnancyKeepsItsRoles()
    {
        var r = new EngineRig();
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, 3, r.Now);
        PregnancyBook.Conceive(r.Save.Pregnancy, r.Now, "Bob B@W");
        r.Engine.CompleteWelcome(new WelcomeChoices(null, Anatomy.Vagina, new PregnancyChoice(true, false, true, 3, 1)), r.Now);
        Assert.True(r.Save.Pregnancy.CanCarry);
        Assert.False(r.Save.Pregnancy.CanBreed);
        Assert.True(PregnancyBook.IsPregnant(r.Save.Pregnancy));
    }
}
