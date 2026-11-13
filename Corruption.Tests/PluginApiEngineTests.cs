using Corruption.Core;

namespace Corruption.Tests;

/// Breed, arousal and climax from other plugins, as the engine applies them.
public class PluginBreedTests
{
    private static readonly PeerRecord Carol = new() { Name = "Carol C", World = "W" };

    /// A carrier on <paramref name="day"/> who lets plugins breed her, near Bob, a tagged breeder.
    private static EngineRig Carrier(double roll, int day = 5, bool consent = true)
    {
        var r = new EngineRig(new FixedRandom(roll), new Balance().WithoutMoon());
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, day, r.Now);
        r.Save.PluginConsent.Breed = consent;
        r.BobBody = new BodyTag(BodyFlags.CanBreed, PregnancyStage.None, null);
        r.Playing = false;
        r.Run(2);
        return r;
    }

    private static string Breed(EngineRig r, PeerRecord? who = null) => r.Engine.BreedFromPlugin(r.Now, who ?? r.Bob, "Milkers");

    [Fact]
    public void Conceived()
    {
        var r = Carrier(0.1);
        string? source = null;
        r.Engine.Conceived += (_, _, s) => source = s;
        Assert.Equal(ApiOutcomes.Conceived, Breed(r));
        Assert.Equal(r.Bob.Key, r.Save.Pregnancy.FatherKey);
        Assert.Contains(r.Host.Notes, n => n.Contains("conceived"));
        Assert.Equal("Milkers", source);
    }

    [Fact]
    public void NotConceived() => Assert.Equal(ApiOutcomes.NotConceived, Breed(Carrier(0.99)));

    [Fact]
    public void NotAllowedByDefault() => Assert.Equal(ApiOutcomes.NotAllowed, Breed(Carrier(0, consent: false)));

    [Fact]
    public void NotSetUp()
    {
        var r = Carrier(0);
        r.Save.Setup = null;
        Assert.Equal(ApiOutcomes.NotSetUp, Breed(r));
    }

    [Fact]
    public void PregnancyOff()
    {
        var r = Carrier(0);
        r.Save.Pregnancy.Enabled = false;
        Assert.Equal(ApiOutcomes.PregnancyOff, Breed(r));
    }

    [Fact]
    public void CantCarry()
    {
        var r = Carrier(0);
        r.Save.Pregnancy.CanCarry = false;
        Assert.Equal(ApiOutcomes.CantCarry, Breed(r));
    }

    [Fact]
    public void AlreadyPregnant()
    {
        var r = Carrier(0);
        PregnancyBook.Conceive(r.Save.Pregnancy, r.Now, "Someone@W");
        Assert.Equal(ApiOutcomes.Pregnant, Breed(r));
        Assert.Equal("Someone@W", r.Save.Pregnancy.FatherKey);
    }

    [Fact]
    public void Recovering()
    {
        var r = Carrier(0);
        r.Save.Pregnancy.RecoveryUntilUtc = r.Now.AddDays(1);
        Assert.Equal(ApiOutcomes.Recovering, Breed(r));
    }

    [Fact]
    public void PausedAndHidden()
    {
        var r = Carrier(0);
        r.Save.Paused = true;
        Assert.Equal(ApiOutcomes.Paused, Breed(r));
        r.Save.Paused = false;
        r.Save.Hidden = true;
        Assert.Equal(ApiOutcomes.Hidden, Breed(r));
    }

    [Fact]
    public void Protected()
    {
        var r = Carrier(0);
        r.Save.Wallet.Sins = 500;
        Assert.Null(r.Engine.BuyItem(ItemIds.Contraceptive));
        Assert.Null(r.Engine.UseItem(ItemIds.Contraceptive, r.Now));
        Assert.Equal(ApiOutcomes.Protected, Breed(r));
    }

    [Fact]
    public void ATaggedNonBreeder()
    {
        var r = Carrier(0);
        r.BobBody = BodyTag.Empty;
        r.Run(1);
        Assert.Equal(ApiOutcomes.NotABreeder, Breed(r));
    }

    [Fact]
    public void TooSoonForTheSameBreeder()
    {
        var r = Carrier(0.99);
        Assert.Equal(ApiOutcomes.NotConceived, Breed(r));
        Assert.Equal(ApiOutcomes.TooSoon, r.Engine.BreedFromPlugin(r.Now.AddSeconds(30), r.Bob, "Milkers"));
        Assert.Equal(ApiOutcomes.NotConceived, r.Engine.BreedFromPlugin(r.Now.AddSeconds(30), Carol, "Milkers"));
        Assert.Equal(ApiOutcomes.NotConceived, r.Engine.BreedFromPlugin(r.Now.AddSeconds(61), r.Bob, "Milkers"));
    }

    [Fact]
    public void AnUntaggedBreederRollsWithoutVirility()
    {
        var r = Carrier(0.1);
        Assert.Equal(0.40, r.Engine.BreedChanceFor(r.Now, Carol), 6);
        Assert.Equal(ApiOutcomes.Conceived, Breed(r, Carol));
        Assert.Equal(Carol.Key, r.Save.Pregnancy.FatherKey);
    }

    [Fact]
    public void AskingFirstDoesntRoll()
    {
        var r = Carrier(0);
        Assert.Equal(0.40, r.Engine.BreedChanceFor(r.Now, r.Bob), 6);
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
        r.Save.PluginConsent.Breed = false;
        Assert.Equal(0, r.Engine.BreedChanceFor(r.Now, r.Bob));
    }

    [Fact]
    public void EveryFactor()
    {
        var r = Carrier(0, day: 4);
        r.BobBody = new BodyTag(BodyFlags.CanBreed | BodyFlags.Virile, PregnancyStage.None, null);
        r.Save.Wallet.Sins = 500;
        Assert.Null(r.Engine.BuyItem(ItemIds.FertilityCharm));
        Assert.Null(r.Engine.UseItem(ItemIds.FertilityCharm, r.Now));
        r.Run(1);
        Assert.Equal(0.75, r.Engine.BreedChanceFor(r.Now, r.Bob), 6);
    }
}

public class PluginArousalTests
{
    private static EngineRig Rig(bool consent = true, bool playing = false)
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon()) { Playing = playing };
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        r.Save.PluginConsent.Arousal = consent;
        r.Save.PluginConsent.Climax = consent;
        r.Run(1);
        return r;
    }

    [Fact]
    public void AGentleRise()
    {
        var r = Rig();
        r.Save.Arousal = 30;
        Assert.Equal(ApiOutcomes.Ok, r.Engine.AddArousalFromPlugin(r.Now, 10, "Milkers"));
        Assert.Equal(40, r.Save.Arousal, 6);
    }

    [Fact]
    public void TooMuchAtOnce()
    {
        var r = Rig();
        r.Save.Arousal = 30;
        Assert.Equal(ApiOutcomes.Limited, r.Engine.AddArousalFromPlugin(r.Now, 40, "Milkers"));
        Assert.Equal(55, r.Save.Arousal, 6);
    }

    [Fact]
    public void TooMuchInAMinute()
    {
        var r = Rig();
        r.Save.Arousal = 0;
        r.Engine.AddArousalFromPlugin(r.Now, 25, "Milkers");
        r.Engine.AddArousalFromPlugin(r.Now.AddSeconds(5), 25, "Milkers");
        Assert.Equal(ApiOutcomes.Limited, r.Engine.AddArousalFromPlugin(r.Now.AddSeconds(10), 25, "Milkers"));
        Assert.Equal(60, r.Save.Arousal, 6);
    }

    [Fact]
    public void NotAllowedByDefault()
    {
        var r = Rig(consent: false);
        Assert.Equal(ApiOutcomes.NotAllowed, r.Engine.AddArousalFromPlugin(r.Now, 10, "Milkers"));
        Assert.Equal(ApiOutcomes.NotAllowed, r.Engine.ClimaxFromPlugin(r.Now, "Milkers"));
    }

    [Fact]
    public void NoRiseInRefractory()
    {
        var r = Rig();
        r.Save.RefractoryUntilUtc = r.Now.AddSeconds(30);
        Assert.Equal(ApiOutcomes.Refractory, r.Engine.AddArousalFromPlugin(r.Now, 10, "Milkers"));
        Assert.Equal(ApiOutcomes.Ok, r.Engine.AddArousalFromPlugin(r.Now, -5, "Milkers"));
    }

    [Fact]
    public void ReachingAHundredClimaxesInAScene()
    {
        var r = Rig(playing: true);
        r.Run(5);
        r.Save.Arousal = 90;
        r.Engine.AddArousalFromPlugin(r.Now, 15, "Milkers");
        Assert.Equal(100, r.Save.Arousal, 6);
        r.Run(11);
        Assert.Contains("You climaxed.", r.Host.Notes);
    }

    [Fact]
    public void ClimaxNeedsAScene() => Assert.Equal(ApiOutcomes.NoScene, Rig().Engine.ClimaxFromPlugin(EngineRig.T0.AddSeconds(1), "Milkers"));

    [Fact]
    public void ClimaxTooLow()
    {
        var r = Rig(playing: true);
        r.Run(5);
        r.Save.Arousal = 30;
        Assert.Equal(ApiOutcomes.TooLow, r.Engine.ClimaxFromPlugin(r.Now, "Milkers"));
        Assert.DoesNotContain("You climaxed.", r.Host.Notes);
    }

    [Fact]
    public void ClimaxedInAScene()
    {
        var r = Rig(playing: true);
        r.Run(5);
        r.Save.Arousal = 95;
        var fired = 0;
        r.Engine.Climaxed += () => fired++;
        Assert.Equal(ApiOutcomes.Climaxed, r.Engine.ClimaxFromPlugin(r.Now, "Milkers"));
        Assert.Equal(1, fired);
    }

    [Fact]
    public void TheLogKeepsEachPluginsLastCall()
    {
        var r = Rig();
        r.Engine.AddArousalFromPlugin(r.Now, 5, "Milkers");
        r.Engine.ClimaxFromPlugin(r.Now.AddSeconds(1), "Other");
        Assert.Equal([("Other", "Climax", ApiOutcomes.NoScene), ("Milkers", "AddArousal", ApiOutcomes.Ok)],
            r.Engine.PluginCalls.Select(c => (c.Plugin, c.Call, c.Outcome)));
    }
}

public class EngineEventTests
{
    private static EngineRig Scene()
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon());
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        return r;
    }

    [Fact]
    public void SceneStartsAndEndsOnce()
    {
        var r = Scene();
        var started = new List<(string?, bool)>();
        var ended = new List<double>();
        r.Engine.SceneStarted += (p, solo) => started.Add((p?.Key, solo));
        r.Engine.SceneFinished += (seconds, _) => ended.Add(seconds);
        r.Run(20);
        r.Playing = false;
        r.Run(40);
        Assert.Equal([(r.Bob.Key, false)], started);
        Assert.Single(ended);
        Assert.True(ended[0] >= 19);
    }

    [Fact]
    public void PartnerClimaxAndOwnClimax()
    {
        var r = Scene();
        var partners = new List<string>();
        var own = 0;
        r.Engine.PartnerClimaxed += p => partners.Add(p.Key);
        r.Engine.Climaxed += () => own++;
        r.Run(11);
        r.BobClimaxes();
        r.Save.Arousal = 99.9;
        r.Run(10);
        Assert.Equal([r.Bob.Key], partners);
        Assert.Equal(1, own);
    }

    [Fact]
    public void LevelUp()
    {
        var r = Scene();
        var levels = new List<int>();
        r.Engine.LeveledUp += levels.Add;
        r.Save.Xp = Progression.XpToNext(1, r.Balance) - 0.01;
        r.Run(11);
        Assert.Equal([2], levels);
    }

    [Fact]
    public void CyclePhaseAndTrimesterAndBirth()
    {
        var r = Scene();
        r.Playing = false;
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, 3, r.Now);
        var phases = new List<CyclePhase>();
        var stages = new List<PregnancyStage>();
        var births = new List<string>();
        var recovered = 0;
        r.Engine.CyclePhaseChanged += phases.Add;
        r.Engine.TrimesterChanged += stages.Add;
        r.Engine.Born += (name, _) => births.Add(name);
        r.Engine.RecoveryEnded += () => recovered++;
        r.Run(1);
        r.Now = r.Now.AddDays(1);
        r.Run(1);
        Assert.Equal([CyclePhase.Fertile], phases);

        PregnancyBook.Conceive(r.Save.Pregnancy, r.Now, "Bob B@W");
        r.Run(1);
        r.Now = r.Now.AddDays(6);
        r.Run(1);
        r.Now = r.Now.AddDays(9);
        r.Run(1);
        r.Now = r.Now.AddDays(3);
        r.Run(1);
        Assert.Equal([PregnancyStage.First, PregnancyStage.Second], stages);
        Assert.Equal(["Bob B"], births);
        Assert.Equal(1, recovered);
    }

    [Fact]
    public void ArousalShownOnlyWhenTheNumberChanges()
    {
        var r = Scene();
        r.Playing = false;
        var shown = new List<int>();
        r.Engine.ArousalShown += shown.Add;
        r.Save.Arousal = 10;
        r.Run(1);
        r.Save.Arousal = 10.4;
        r.Run(1);
        r.Save.Arousal = 12;
        r.Run(1);
        Assert.Equal([12], shown);
    }
}
