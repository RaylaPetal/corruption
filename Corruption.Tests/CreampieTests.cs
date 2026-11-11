using Corruption.Core;

namespace Corruption.Tests;

/// A partner's climax inside this character, with Cummy version 6.
public class CreampieTests
{
    private static readonly PeerRecord Carol = new() { Name = "Carol C", World = "W" };
    private static readonly PeerRecord Dan = new() { Name = "Dan D", World = "W" };

    /// A duo scene with Bob, whose tip is inside the pussy, and Cummy version 6.
    private static EngineRig Inside(string? hole = CreampieAim.Pussy, bool v6 = true)
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon());
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        r.Stages = [];
        r.Blocked = [];
        if (v6) r.Creampies = [];
        r.Host.Aim = ["gr", "st", "ch", "bt"];
        r.Host.InsideHole = hole;
        r.Run(11);
        return r;
    }

    private static double TotalXp(EngineRig r)
    {
        var total = r.Save.Xp;
        for (var level = 1; level < r.Save.Level; level++) total += Progression.XpToNext(level, r.Balance);
        return total;
    }

    /// XP from Bob's climax with this rig, less the same climax with layering off.
    private static double ClimaxXp(Action<EngineRig> setup)
    {
        var on = Inside();
        setup(on);
        var before = TotalXp(on);
        on.BobClimaxes();
        var off = Inside();
        setup(off);
        off.Engine.LayerOnPartnerClimax = false;
        var offBefore = TotalXp(off);
        off.BobClimaxes();
        return TotalXp(on) - before - (TotalXp(off) - offBefore);
    }

    // ── Landing ─────────────────────────────────────────────────────────────

    [Fact]
    public void InsideIsACreampieAndNoOutsideLayer()
    {
        var r = Inside();
        r.BobClimaxes();
        Assert.Equal([(r.Bob.Key, CreampieAim.Pussy)], r.Host.Creampies);
        Assert.Empty(r.Host.Layers);
        Assert.Contains($"{r.Bob.Name} came inside your pussy.", r.Host.Notes);
    }

    [Fact]
    public void ARefreshIsACreampieToo()
    {
        var r = Inside(CreampieAim.Ass);
        r.Host.CreampieOutcome = "refreshed";
        r.BobClimaxes();
        Assert.Empty(r.Host.Layers);
        Assert.Equal(1, r.Save.CreampieRefreshes);
        Assert.Equal(1, r.Save.Goals.Stats["Creampies"]);
    }

    [Fact]
    public void ABlockedHoleLandsOnTheNearestOutsidePart()
    {
        var r = Inside();
        r.Host.CreampieOutcome = "blocked";
        r.BobClimaxes();
        Assert.Equal([(r.Bob.Key, "gr")], r.Host.Layers);
        Assert.False(r.Save.Goals.Stats.ContainsKey("Creampies"));
    }

    [Theory]
    [InlineData("blacklisted")]
    [InlineData("no-character")]
    [InlineData("unknown-hole")]
    [InlineData("error")]
    public void OtherAnswersLandNothing(string outcome)
    {
        var r = Inside();
        r.Host.CreampieOutcome = outcome;
        r.BobClimaxes();
        Assert.Empty(r.Host.Layers);
        Assert.Equal(1, r.Save.Goals.Stats["PartnerClimaxes"]);
    }

    [Fact]
    public void CummyVersionFiveIsAlwaysOutside()
    {
        var r = Inside(v6: false);
        r.BobClimaxes();
        Assert.Empty(r.Host.Creampies);
        Assert.Equal([(r.Bob.Key, "gr")], r.Host.Layers);
    }

    [Fact]
    public void OutsideIsLayeredAsBefore()
    {
        var r = Inside(hole: null);
        r.BobClimaxes();
        Assert.Empty(r.Host.Creampies);
        Assert.Equal([(r.Bob.Key, "gr")], r.Host.Layers);
    }

    [Fact]
    public void LayeringTurnedOffMeansNoCreampie()
    {
        var r = Inside();
        r.Engine.LayerOnPartnerClimax = false;
        r.BobClimaxes();
        Assert.Empty(r.Host.Creampies);
        Assert.Empty(r.Host.Layers);
    }

    [Fact]
    public void AnOlderPartnerClientNeverCreampies()
    {
        var r = Inside();
        r.BobBody = null;
        r.Run(1);
        r.BobClimaxes();
        Assert.Empty(r.Host.Creampies);
    }

    [Fact]
    public void AnInsideThirdClimaxIsNotFullBody()
    {
        var r = Inside();
        r.Others =
        [
            new VisiblePlayer(Carol, 2, new PoseKey(52, 0), new PresenceTag(new PoseKey(52, 0), 1, 0, 0, false), Body: BodyTag.Empty),
            new VisiblePlayer(Dan, 2, new PoseKey(53, 0), new PresenceTag(new PoseKey(53, 0), 1, 0, 0, false), Body: BodyTag.Empty),
        ];
        r.Run(1);
        for (var i = 0; i < 2; i++)
        {
            var other = r.Others[i];
            r.Others[i] = other with { Tag = other.Tag with { ClimaxCount = other.Tag.ClimaxCount + 1 } };
            r.Run(1);
        }
        r.BobClimaxes();
        Assert.Single(r.Host.Creampies);
        Assert.DoesNotContain(r.Host.Layers, l => l.Part == CummyParts.FullBody);
    }

    // ── Rewards ─────────────────────────────────────────────────────────────

    [Fact]
    public void ACreampiePaysFiftyXpAndNoLayerXp() => Assert.Equal(CreampieRules.Xp, ClimaxXp(_ => { }), 6);

    [Fact]
    public void CumMagnetAddsOneOutsidePartAndSpendsAUse()
    {
        var r = Inside();
        r.Save.Wallet.Sins = 500;
        Assert.Null(r.Engine.BuyItem(ItemIds.CumMagnet));
        Assert.Null(r.Engine.UseItem(ItemIds.CumMagnet, r.Now));
        var uses = r.Save.Effects[ItemIds.CumMagnet].UsesLeft;
        r.BobClimaxes();
        Assert.Equal([(r.Bob.Key, "st")], r.Host.Layers);
        Assert.Equal(uses - 1, r.Save.Effects[ItemIds.CumMagnet].UsesLeft);
    }

    [Fact]
    public void PartnersAreKeptAsDistinctHashes()
    {
        var r = Inside();
        r.BobClimaxes();
        r.Run(70);
        r.BobClimaxes();
        Assert.Equal([BondHashes.Of(r.Bob.Key)], r.Save.CreampiePartners);
        Assert.Equal(2, r.Save.Goals.Stats["Creampies"]);
    }

    [Fact]
    public void BothHolesDrippingIsNoted()
    {
        var r = Inside();
        r.Creampies = new() { [CreampieAim.Pussy] = 300, [CreampieAim.Ass] = 200 };
        r.Run(1);
        Assert.Equal(1, r.Save.BothWays);
        Assert.True(r.Save.HadCummyV6);
    }
}

public class CreampieCravingTests
{
    private static EngineRig Craving(CravingKind kind)
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon());
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, 2, r.Now);
        PregnancyBook.Conceive(r.Save.Pregnancy, r.Now, "Someone@W");
        r.Save.Pregnancy.Craving = kind;
        r.Save.Pregnancy.CravingDay = r.Now.Date.ToString("yyyy-MM-dd");
        r.Stages = [];
        r.Blocked = [];
        r.Creampies = [];
        r.Host.InsideHole = CreampieAim.Ass;
        r.Run(11);
        return r;
    }

    [Theory]
    [InlineData(CravingKind.Creampie)]
    [InlineData(CravingKind.Layer)]
    public void ACreampieMeetsTheCraving(CravingKind kind)
    {
        var r = Craving(kind);
        r.BobClimaxes();
        Assert.True(r.Save.Pregnancy.CravingMet);
    }

    [Fact]
    public void NeverCravedWithoutCummyVersionSix()
    {
        for (var seed = 0; seed < 300; seed++)
        {
            var p = new PregnancyState();
            PregnancyBook.Enable(p, true, false, 2, EngineRig.T0);
            PregnancyBook.Conceive(p, EngineRig.T0, "Someone@W");
            Assert.NotEqual(CravingKind.Creampie, CravingBook.Roll(p, EngineRig.T0, seed, true, creampies: false));
        }
    }

    [Fact]
    public void CravedOnceCummyVersionSixWasSeen()
    {
        var seen = Enumerable.Range(0, 300).Select(seed =>
        {
            var p = new PregnancyState();
            PregnancyBook.Enable(p, true, false, 2, EngineRig.T0);
            PregnancyBook.Conceive(p, EngineRig.T0, "Someone@W");
            return CravingBook.Roll(p, EngineRig.T0, seed, true, creampies: true);
        });
        Assert.Contains(CravingKind.Creampie, seen.Cast<CravingKind>());
    }
}

public class DrippingMarkedTests
{
    private static readonly Balance B = new();

    [Fact]
    public void ADrippingPussyCountsAsThreeStages()
        => Assert.Equal(1.12, Progression.WeightedMarkedFactor(new Dictionary<string, int> { ["gr"] = 1 }, [], B, drippingHoles: 1), 6);

    [Fact]
    public void TheCapStillHolds()
        => Assert.Equal(1.3, Progression.WeightedMarkedFactor(new Dictionary<string, int> { ["ch"] = 8 }, [], B, drippingHoles: 2), 6);

    [Fact]
    public void HoleStagesComeAfterTheFullCoat()
    {
        var coat = CummyParts.All.ToDictionary(p => p, _ => 1);
        var mods = new Modifiers { MarkedCap = 5 };
        var plain = Progression.WeightedMarkedFactor(coat, [], B, mods);
        Assert.Equal(plain + 0.03 * 3, Progression.WeightedMarkedFactor(coat, [], B, mods, drippingHoles: 1), 6);
    }

    [Fact]
    public void TheEngineCountsDrippingHolesUntilTheDripEnds()
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon());
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        r.Stages = new Dictionary<string, int> { ["gr"] = 1 };
        r.Blocked = [];
        r.Creampies = new() { [CreampieAim.Pussy] = 600 };
        r.Run(11);
        Assert.Equal(1.12, r.Engine.Status.Factors.Marked, 6);
        r.Creampies = [];
        r.Run(10);
        Assert.Equal(1.03, r.Engine.Status.Factors.Marked, 6);
    }
}

public class CreampieLickTests
{
    /// The local character climaxes with Bob, with Cummy version 6, their tip inside <paramref name="hole"/> of Bob's.
    private static EngineRig ClimaxedInside(string? hole)
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon());
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        r.Stages = [];
        r.Blocked = [];
        r.Creampies = [];
        r.Host.OntoAim = ["st", "ch"];
        r.Host.InsideOntoHole = hole;
        r.Run(1);
        r.Save.Arousal = 99.9;
        r.Run(10);
        return r;
    }

    private static double TotalXp(EngineRig r)
    {
        var total = r.Save.Xp;
        for (var level = 1; level < r.Save.Level; level++) total += Progression.XpToNext(level, r.Balance);
        return total;
    }

    private static double LickXp(EngineRig r)
    {
        Assert.Null(r.Engine.TryLickClean(r.Now));
        var before = TotalXp(r);
        r.Engine.LickAnswered("Bob B", "W", "licked");
        return TotalXp(r) - before;
    }

    [Fact]
    public void InsideTheirPussyLicksTheGroinForThirty()
    {
        var r = ClimaxedInside(CreampieAim.Pussy);
        Assert.Equal(AftercareRules.CreampieLickXp, LickXp(r), 6);
        Assert.Equal([(r.Bob.Key, "gr")], r.Host.Licks);
    }

    [Fact]
    public void InsideTheirAssLicksTheButt()
    {
        var r = ClimaxedInside(CreampieAim.Ass);
        LickXp(r);
        Assert.Equal("bt", r.Host.Licks.Single().Part);
    }

    [Fact]
    public void OutsideIsAimedAsBeforeForTwenty()
    {
        var r = ClimaxedInside(null);
        Assert.Equal(AftercareRules.LickXp, LickXp(r), 6);
        Assert.Equal("st", r.Host.Licks.Single().Part);
    }

    [Fact]
    public void AKnownBlockedHolePartFallsBackToTheAim()
    {
        var r = ClimaxedInside(CreampieAim.Pussy);
        r.Host.PartnerBlockList = ["gr"];
        Assert.Equal(AftercareRules.LickXp, LickXp(r), 6);
        Assert.Equal("st", r.Host.Licks.Single().Part);
    }
}

public class LickedOutTests
{
    private static EngineRig AfterATick()
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon());
        r.Run(11);
        r.Playing = false;
        r.Save.Arousal = 10;
        return r;
    }

    [Fact]
    public void ARecentPartnerLickingYouOutGivesTen()
    {
        var r = AfterATick();
        r.Engine.LickedOut(r.Bob, r.Now);
        Assert.Equal(20, r.Save.Arousal, 6);
    }

    [Fact]
    public void AStrangerLickingYouOutDoesNothing()
    {
        var r = AfterATick();
        r.Engine.LickedOut(new PeerRecord { Name = "Carol C", World = "W" }, r.Now);
        Assert.Equal(10, r.Save.Arousal, 6);
    }

    [Fact]
    public void ThePartChangeFirstIsToppedUpNotDoubled()
    {
        var r = AfterATick();
        r.Engine.CleanedBy(r.Bob, r.Now);
        r.Engine.LickedOut(r.Bob, r.Now);
        Assert.Equal(20, r.Save.Arousal, 6);
    }

    [Fact]
    public void TheCreampieEndFirstIsNotAddedTo()
    {
        var r = AfterATick();
        r.Engine.LickedOut(r.Bob, r.Now);
        r.Engine.CleanedBy(r.Bob, r.Now);
        Assert.Equal(20, r.Save.Arousal, 6);
    }

    [Fact]
    public void TheThirtySecondLimitIsShared()
    {
        var r = AfterATick();
        r.Engine.LickedOut(r.Bob, r.Now);
        r.Engine.LickedOut(r.Bob, r.Now.AddSeconds(20));
        Assert.Equal(20, r.Save.Arousal, 6);
        r.Engine.LickedOut(r.Bob, r.Now.AddSeconds(31));
        Assert.Equal(30, r.Save.Arousal, 6);
    }
}

public class CreampieGoalTests
{
    [Fact]
    public void CreampieGoalsWaitForCummyVersionSix()
    {
        var save = new CharacterSave { Goals = { Seed = 1 } };
        var book = new GoalBook(save);
        for (var day = 0; day < 300; day++)
        {
            book.Roll(EngineRig.T0.AddDays(day));
            Assert.DoesNotContain(save.Goals.Daily.Concat(save.Goals.Weekly), g => g.Id is "d_creampie" or "w_creampie");
        }
        save.HadCummyV6 = true;
        var seen = new HashSet<string>();
        for (var day = 300; day < 1200; day++)
        {
            book.Roll(EngineRig.T0.AddDays(day));
            foreach (var goal in save.Goals.Daily.Concat(save.Goals.Weekly)) seen.Add(goal.Id);
        }
        Assert.Contains("d_creampie", seen);
        Assert.Contains("w_creampie", seen);
    }

    [Fact]
    public void AVersionSixSampleMarksIt()
    {
        var r = new EngineRig { Playing = false };
        r.Run(1);
        Assert.False(r.Save.HadCummyV6);
        r.Creampies = [];
        r.Run(1);
        Assert.True(r.Save.HadCummyV6);
    }

    [Fact]
    public void FilledAfterTwentyFiveCreampies()
    {
        var save = new CharacterSave();
        var book = new GoalBook(save);
        book.Record(GoalKind.Creampies, 24);
        book.TakeRewards();
        Assert.DoesNotContain("filled", save.Goals.Achievements);
        book.Record(GoalKind.Creampies);
        book.TakeRewards();
        Assert.Contains("filled", save.Goals.Achievements);
    }

    [Fact]
    public void BothWaysFromBothHolesDripping()
    {
        var r = new EngineRig { Playing = false };
        r.Creampies = new() { [CreampieAim.Pussy] = 100 };
        r.Run(2);
        Assert.DoesNotContain("both_ways", r.Save.Goals.Achievements);
        r.Creampies[CreampieAim.Ass] = 50;
        r.Run(2);
        Assert.Contains("both_ways", r.Save.Goals.Achievements);
    }

    [Fact]
    public void SecondsFromARefresh()
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon());
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        r.Stages = [];
        r.Blocked = [];
        r.Creampies = [];
        r.Host.InsideHole = CreampieAim.Pussy;
        r.Host.CreampieOutcome = "refreshed";
        r.Run(11);
        r.BobClimaxes();
        r.Run(1);
        Assert.Contains("seconds", r.Save.Goals.Achievements);
    }

    [Fact]
    public void WellBredAfterFiveDifferentPartners()
    {
        var r = new EngineRig { Playing = false };
        for (var i = 0; i < 4; i++) r.Save.CreampiePartners.Add($"hash{i}");
        r.Run(2);
        Assert.DoesNotContain("well_bred", r.Save.Goals.Achievements);
        r.Save.CreampiePartners.Add("hash4");
        r.Run(2);
        Assert.Contains("well_bred", r.Save.Goals.Achievements);
    }
}

public class CreampieTitleTests
{
    private static EngineRig Wearing(string title)
    {
        var r = new EngineRig(new FixedRandom(0.99), new Balance().WithoutMoon());
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        r.Save.Titles.Unlocked.Add(title);
        r.Save.Titles.Equipped = title;
        r.Stages = [];
        r.Blocked = [];
        r.Creampies = [];
        r.Host.InsideHole = CreampieAim.Pussy;
        r.Run(11);
        return r;
    }

    private static double TotalXp(EngineRig r)
    {
        var total = r.Save.Xp;
        for (var level = 1; level < r.Save.Level; level++) total += Progression.XpToNext(level, r.Balance);
        return total;
    }

    private static Modifiers Of(string id) => TitleCatalog.Find(id)!.Bonus;

    [Fact]
    public void FilledPaysSeventyFive()
    {
        var on = Wearing("filled");
        var before = TotalXp(on);
        on.BobClimaxes();
        var off = Wearing("filled");
        off.Engine.LayerOnPartnerClimax = false;
        var offBefore = TotalXp(off);
        off.BobClimaxes();
        Assert.Equal(75, TotalXp(on) - before - (TotalXp(off) - offBefore), 6);
    }

    [Fact]
    public void BothWaysCountsFourStagesAHole()
        => Assert.Equal(1.12, Progression.WeightedMarkedFactor(new Dictionary<string, int>(), [], new Balance(), Of("both_ways"), 1), 6);

    [Fact]
    public void SecondsGivesArousalOnARefresh()
    {
        var r = Wearing("seconds");
        r.Host.CreampieOutcome = "refreshed";
        r.Save.Arousal = 20;
        var withTitle = Arousal(r);
        var plain = Wearing("filled");
        plain.Host.CreampieOutcome = "refreshed";
        plain.Save.Arousal = 20;
        Assert.Equal(10, withTitle - Arousal(plain), 6);
    }

    private static double Arousal(EngineRig r)
    {
        r.BobClimaxes();
        return r.Save.Arousal;
    }

    [Fact]
    public void WellBredRaisesTheChanceToTwentySevenAndAHalf()
    {
        var save = new CharacterSave { Setup = new ProfileSetup() };
        PregnancyBook.Enable(save.Pregnancy, true, false, 4, EngineRig.T0);
        Assert.Equal(0.275, PregnancyBook.ConceptionChance(save, EngineRig.T0.AddHours(1), false, 0, false, Of("well_bred").ConceptionOwn), 6);
    }
}

public class DrippingMoodleTests
{
    private static MoodlesInput Base => new(true, 0, 0, false, 0, false, false, false);

    [Fact]
    public void NamesTheDrippingHoles()
    {
        Assert.Equal(MoodlesPlan.DrippingAss, MoodlesPlan.Plan(Base with { Dripping = [CreampieAim.Ass] })[MoodleKind.Dripping]);
        Assert.Equal(MoodlesPlan.DrippingPussy | MoodlesPlan.DrippingAss,
            MoodlesPlan.Plan(Base with { Dripping = [CreampieAim.Pussy, CreampieAim.Ass] })[MoodleKind.Dripping]);
    }

    [Fact]
    public void GoneWhenNothingDrips()
    {
        Assert.False(MoodlesPlan.Plan(Base with { Dripping = [] }).ContainsKey(MoodleKind.Dripping));
        Assert.Empty(MoodlesPlan.Plan(Base with { Visible = false, Dripping = [CreampieAim.Pussy] }));
    }
}
