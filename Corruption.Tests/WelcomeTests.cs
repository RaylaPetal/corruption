using Corruption.Core;

namespace Corruption.Tests;

public class WelcomeTests
{
    private static readonly PregnancyChoice Off = new(false, true, false, 3, 1);
    private static readonly PersonaChoice Plain = new(PersonaIds.Easygoing, []);

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
        Assert.Null(r.Engine.CompleteWelcome(new WelcomeChoices(new ProfileSetup(), Anatomy.Vagina, Off, Plain), r.Now));
        Assert.False(Welcome.IsNeeded(r.Save));
        r.Engine.ResetProgress(r.Now);
        Assert.True(Welcome.IsNeeded(r.Save));
    }

    [Fact]
    public void ExistingCharactersSeeItOnce()
    {
        var r = new EngineRig();
        Assert.True(Welcome.IsNeeded(r.Save));
        Assert.Null(r.Engine.CompleteWelcome(new WelcomeChoices(null, Anatomy.Penis, Off, Plain), r.Now));
        Assert.False(Welcome.IsNeeded(r.Save));
    }

    [Fact]
    public void AppliesEverything()
    {
        var r = Fresh();
        var setup = new ProfileSetup { Difficulty = Difficulty.Hard, Decay = DecayMode.Off, AutoClimax = false, ArousalGain = 1.5 };
        Assert.Null(r.Engine.CompleteWelcome(new WelcomeChoices(setup, Anatomy.Both, new PregnancyChoice(true, true, true, 4, 2), Plain), r.Now));
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
        r.Engine.CompleteWelcome(new WelcomeChoices(new ProfileSetup { Difficulty = Difficulty.Relaxed }, Anatomy.NotSet, Off, Plain), r.Now);
        Assert.Same(before, r.Save.Setup);
    }

    [Fact]
    public void RefusesAnIncompleteChoiceAndChangesNothing()
    {
        var r = Fresh();
        Assert.NotNull(r.Engine.CompleteWelcome(new WelcomeChoices(null, Anatomy.Penis, Off, Plain), r.Now));
        Assert.NotNull(r.Engine.CompleteWelcome(new WelcomeChoices(new ProfileSetup(), Anatomy.Penis, new PregnancyChoice(true, false, false, 3, 1), Plain), r.Now));
        Assert.Null(r.Save.Setup);
        Assert.Equal(Anatomy.NotSet, r.Save.Anatomy);
    }

    [Fact]
    public void TurningPregnancyOffEndsIt()
    {
        var r = new EngineRig();
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, 3, r.Now);
        r.Engine.CompleteWelcome(new WelcomeChoices(null, Anatomy.Vagina, Off, Plain), r.Now);
        Assert.False(r.Save.Pregnancy.Enabled);
    }

    [Fact]
    public void APregnancyKeepsItsRoles()
    {
        var r = new EngineRig();
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, 3, r.Now);
        KnownPregnancy.Conceive(r.Save.Pregnancy, r.Now, "Bob B@W");
        r.Engine.CompleteWelcome(new WelcomeChoices(null, Anatomy.Vagina, new PregnancyChoice(true, false, true, 3, 1), Plain), r.Now);
        Assert.True(r.Save.Pregnancy.CanCarry);
        Assert.False(r.Save.Pregnancy.CanBreed);
        Assert.True(PregnancyBook.IsPregnant(r.Save.Pregnancy));
    }

    // ── The persona ─────────────────────────────────────────────────────────

    [Fact]
    public void AnOlderCharacterIsOfferedAPersona()
    {
        var r = new EngineRig { Playing = false };
        r.Save.WelcomeVersion = 1;
        Assert.True(Welcome.NeedsPersona(r.Save));
        Assert.True(Welcome.IsNeeded(r.Save));
    }

    [Fact]
    public void FinishingLocksThePersona()
    {
        var r = new EngineRig { Playing = false };
        var choice = new PersonaChoice(PersonaIds.Shy, [PersonaIds.Hypersexual, PersonaIds.Sensitive]);
        Assert.Null(r.Engine.CompleteWelcome(new WelcomeChoices(null, Anatomy.NotSet, Off, choice), r.Now));
        Assert.Equal(PersonaIds.Shy, r.Save.Setup!.Personality);
        Assert.Equal([PersonaIds.Hypersexual, PersonaIds.Sensitive], r.Save.Setup.Traits);
        Assert.False(Welcome.IsNeeded(r.Save));
    }

    [Fact]
    public void GoingThroughItAgainDoesntChangeIt()
    {
        var r = new EngineRig { Playing = false };
        r.Engine.CompleteWelcome(new WelcomeChoices(null, Anatomy.NotSet, Off, new PersonaChoice(PersonaIds.Shy, [])), r.Now);
        Assert.Null(r.Engine.CompleteWelcome(new WelcomeChoices(null, Anatomy.Penis, Off, new PersonaChoice(PersonaIds.Lewd, [PersonaIds.Edger])), r.Now));
        Assert.Equal(PersonaIds.Shy, r.Save.Setup!.Personality);
        Assert.Empty(r.Save.Setup.Traits);
        Assert.Equal(Anatomy.Penis, r.Save.Anatomy);
    }

    [Fact]
    public void ANewCharacterNeedsAPersonality()
    {
        var r = Fresh();
        Assert.Equal("Pick a personality.", r.Engine.CompleteWelcome(new WelcomeChoices(new ProfileSetup(), Anatomy.NotSet, Off), r.Now));
        Assert.Null(r.Save.Setup);
    }

    [Fact]
    public void TooManyTraitsAreRefused()
    {
        var r = Fresh();
        var choice = new PersonaChoice(PersonaIds.Shy, [PersonaIds.Edger, PersonaIds.Greedy, PersonaIds.Messy]);
        Assert.NotNull(r.Engine.CompleteWelcome(new WelcomeChoices(new ProfileSetup(), Anatomy.NotSet, Off, choice), r.Now));
        Assert.Null(r.Save.Setup);
    }

    [Fact]
    public void AResetMeansChoosingAgain()
    {
        var r = Fresh();
        r.Engine.CompleteWelcome(new WelcomeChoices(new ProfileSetup(), Anatomy.NotSet, Off, new PersonaChoice(PersonaIds.Bratty, [])), r.Now);
        r.Engine.ResetProgress(r.Now);
        Assert.True(Welcome.NeedsPersona(r.Save));
    }
}
