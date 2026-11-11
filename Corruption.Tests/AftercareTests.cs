using Corruption.Core;

namespace Corruption.Tests;

/// Licking a partner clean after your climax, and being licked or kissed clean.
public class AftercareTests
{
    /// A duo scene with Cummy version 5 (stages known), ending in the local character's climax.
    private static EngineRig ClimaxedWithBob(bool v5 = true)
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon());
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        if (v5)
        {
            r.Stages = [];
            r.Blocked = [];
        }
        r.Host.OntoAim = ["st", "ch"];
        r.Run(1);
        r.Save.Arousal = 99.9;
        r.Run(10);
        Assert.Contains("You climaxed.", r.Host.Notes);
        return r;
    }

    private static double TotalXp(EngineRig r)
    {
        var total = r.Save.Xp;
        for (var level = 1; level < r.Save.Level; level++) total += Progression.XpToNext(level, r.Balance);
        return total;
    }

    [Fact]
    public void AClimaxWithAPartnerOffersLickClean()
    {
        var r = ClimaxedWithBob();
        var offer = r.Engine.LickOfferAt(r.Now);
        Assert.NotNull(offer);
        Assert.Equal(r.Bob.Key, offer.Partner.Key);
        Assert.True(offer.Left(r.Now) > TimeSpan.FromMinutes(1.9));
    }

    [Fact]
    public void TheOfferEndsAfterTwoMinutes()
    {
        var r = ClimaxedWithBob();
        Assert.Null(r.Engine.LickOfferAt(r.Now.AddMinutes(2.1)));
        Assert.NotNull(r.Engine.TryLickClean(r.Now.AddMinutes(2.1)));
    }

    [Fact]
    public void OlderCummyOffersNothing() => Assert.Null(ClimaxedWithBob(v5: false).Engine.LickOfferAt(EngineRig.T0.AddSeconds(12)));

    [Fact]
    public void AClimaxAloneOffersNothing()
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon()) { BobVisible = false, Stages = [], Blocked = [] };
        r.Act.Solo = true;
        r.Run(1);
        r.Save.Arousal = 99.9;
        r.Run(10);
        Assert.Null(r.Engine.LickOfferAt(r.Now));
    }

    [Fact]
    public void TheLickGoesToTheNearestUnblockedPart()
    {
        var r = ClimaxedWithBob();
        r.Host.PartnerBlockList = ["st"];
        Assert.Null(r.Engine.TryLickClean(r.Now));
        Assert.Equal([(r.Bob.Key, "ch")], r.Host.Licks);
        Assert.Equal(LickState.Waiting, r.Engine.LickOfferAt(r.Now)!.State);
    }

    [Fact]
    public void WithoutBonesItGoesToTheStomach()
    {
        var r = ClimaxedWithBob();
        r.Host.OntoAim = null;
        r.Engine.TryLickClean(r.Now);
        Assert.Equal("st", r.Host.Licks.Single().Part);
    }

    [Fact]
    public void LickedPaysOnce()
    {
        var r = ClimaxedWithBob();
        r.Engine.TryLickClean(r.Now);
        var before = TotalXp(r);
        r.Engine.LickAnswered("Bob B", "W", "licked");
        Assert.Equal(20, TotalXp(r) - before, 6);
        Assert.Equal(1, r.Save.Goals.Stats["Licks"]);
        r.Engine.LickAnswered("Bob B", "W", "licked");
        Assert.Equal(20, TotalXp(r) - before, 6);
        Assert.Null(r.Engine.LickOfferAt(r.Now));
    }

    [Fact]
    public void ABondGainsAMinute()
    {
        var r = ClimaxedWithBob();
        r.Save.BondWith = r.Bob.Key;
        r.Save.BondFormed = true;
        var before = r.Save.BondSeconds;
        r.Engine.TryLickClean(r.Now);
        r.Engine.LickAnswered("Bob B", "W", "licked");
        Assert.Equal(before + 60, r.Save.BondSeconds);
    }

    [Fact]
    public void NothingAllowsOneMoreTry()
    {
        var r = ClimaxedWithBob();
        r.Engine.TryLickClean(r.Now);
        r.Engine.LickAnswered("Bob B", "W", "nothing");
        Assert.Equal(LickState.Offered, r.Engine.LickOfferAt(r.Now)!.State);
        Assert.Null(r.Engine.TryLickClean(r.Now));
        r.Engine.LickAnswered("Bob B", "W", "nothing");
        Assert.Null(r.Engine.LickOfferAt(r.Now));
        Assert.False(r.Save.Goals.Stats.ContainsKey("Licks"));
    }

    [Theory]
    [InlineData("not-paired", "isn't your Cummy partner")]
    [InlineData("out-of-range", "too far away")]
    public void ARefusalPaysNothingAndSaysWhy(string outcome, string says)
    {
        var r = ClimaxedWithBob();
        r.Host.LickOutcome = outcome;
        Assert.Contains(says, r.Engine.TryLickClean(r.Now));
        Assert.Equal(LickState.Offered, r.Engine.LickOfferAt(r.Now)!.State);
    }

    [Fact]
    public void AnAnswerFromSomeoneElseIsIgnored()
    {
        var r = ClimaxedWithBob();
        r.Engine.TryLickClean(r.Now);
        r.Engine.LickAnswered("Carol C", "W", "licked");
        Assert.Equal(LickState.Waiting, r.Engine.LickOfferAt(r.Now)!.State);
    }

    [Fact]
    public void CleanupCrewPaysDouble()
    {
        var r = ClimaxedWithBob();
        r.Save.Titles.Unlocked.Add("cleanup_crew");
        r.Save.Titles.Equipped = "cleanup_crew";
        r.Engine.TryLickClean(r.Now);
        var before = TotalXp(r);
        r.Engine.LickAnswered("Bob B", "W", "licked");
        Assert.Equal(40, TotalXp(r) - before, 6);
    }

    // ── Being licked or kissed ──────────────────────────────────────────────

    private static EngineRig AfterATick()
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon()) { Playing = false };
        r.Playing = true;
        r.Run(11);
        r.Playing = false;
        r.Save.Arousal = 10;
        return r;
    }

    [Fact]
    public void ARecentPartnersLickArouses()
    {
        var r = AfterATick();
        r.Engine.CleanedBy(r.Bob, r.Now);
        Assert.Equal(15, r.Save.Arousal, 6);
    }

    [Fact]
    public void AStrangersLickDoesNothing()
    {
        var r = AfterATick();
        r.Engine.CleanedBy(new PeerRecord { Name = "Carol C", World = "W" }, r.Now);
        Assert.Equal(10, r.Save.Arousal, 6);
    }

    [Fact]
    public void AnOldPartnersLickDoesNothing()
    {
        var r = AfterATick();
        r.Engine.CleanedBy(r.Bob, r.Now.AddMinutes(3));
        Assert.Equal(10, r.Save.Arousal, 6);
    }

    [Fact]
    public void AtMostOnceEveryThirtySeconds()
    {
        var r = AfterATick();
        r.Engine.CleanedBy(r.Bob, r.Now);
        r.Engine.CleanedBy(r.Bob, r.Now.AddSeconds(10));
        Assert.Equal(15, r.Save.Arousal, 6);
        r.Engine.CleanedBy(r.Bob, r.Now.AddSeconds(31));
        Assert.Equal(20, r.Save.Arousal, 6);
    }
}
