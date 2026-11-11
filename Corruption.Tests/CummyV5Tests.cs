using Corruption.Core;

namespace Corruption.Tests;

/// What Cummy version 5's stages and blocks change.
public class CummyV5Tests
{
    private static readonly Balance B = new();

    private static EngineRig Partnered()
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon());
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        return r;
    }

    // ── Blocked parts ───────────────────────────────────────────────────────

    [Fact]
    public void ABlockedGroinIsNeverAskedForOnAPartnersClimax()
    {
        var r = Partnered();
        r.Blocked = ["gr"];
        r.Stages = [];
        r.Host.Aim = ["gr", "st", "ch"];
        r.Run(11);
        r.BobClimaxes();
        Assert.Equal(["st"], r.Host.Layers.Select(l => l.Part));
    }

    [Fact]
    public void ABlockedGroinIsNeverAskedForAlone()
    {
        var r = Partnered();
        r.BobVisible = false;
        r.Act.Solo = true;
        r.Save.Anatomy = Anatomy.Vagina;
        r.Blocked = ["gr"];
        r.Stages = [];
        r.Host.SelfAim = ["gr", "hr"];
        r.Run(1);
        r.Save.Arousal = 99.9;
        r.Run(10);
        Assert.Equal(["hr"], r.Host.Layers.Select(l => l.Part));
    }

    [Fact]
    public void WithoutBlocksKnownEveryPartIsTried()
    {
        var r = Partnered();
        r.Host.Aim = ["gr", "st"];
        r.Host.Outcomes.Enqueue("blocked");
        r.Run(11);
        r.BobClimaxes();
        Assert.Equal(["gr", "st"], r.Host.Layers.Select(l => l.Part));
    }

    // ── The marked factor ───────────────────────────────────────────────────

    [Fact]
    public void FaceStagesCountDouble()
        => Assert.Equal(1.15, Progression.WeightedMarkedFactor(new Dictionary<string, int> { ["mo"] = 2, ["ch"] = 1 }, [], B), 6);

    [Fact]
    public void AFullCoatIsCapped()
    {
        // Every part but the face at one stage: 15 stages, ×1.5 is 22.5, so 1.675 capped to 1.3.
        var coat = CummyParts.All.Where(p => p != "mo").ToDictionary(p => p, _ => 1);
        Assert.True(Progression.IsFullCoat(coat, ["mo"]));
        Assert.Equal(1.3, Progression.WeightedMarkedFactor(coat, ["mo"], B), 6);
        Assert.Equal(1 + 0.03 * 15 * 1.5, Progression.WeightedMarkedFactor(coat, ["mo"], B, new Modifiers { MarkedCap = 2 }), 6);
    }

    [Fact]
    public void ABlockedPartDoesntSpoilAFullCoat()
    {
        var coat = CummyParts.All.Where(p => p != "gr").ToDictionary(p => p, _ => 1);
        Assert.False(Progression.IsFullCoat(coat, []));
        Assert.True(Progression.IsFullCoat(coat, ["gr"]));
    }

    [Fact]
    public void TheEngineUsesStagesWhenCummyGivesThem()
    {
        var r = Partnered();
        r.LayersWorn = 3;
        r.Stages = new Dictionary<string, int> { ["mo"] = 2, ["ch"] = 1 };
        r.Blocked = [];
        r.Run(11);
        Assert.Equal(1.15, r.Engine.Status.Factors.Marked, 6);
    }

    [Fact]
    public void OlderCummyKeepsThePlainCount()
    {
        var r = Partnered();
        r.LayersWorn = 3;
        r.Run(11);
        Assert.Equal(1.09, r.Engine.Status.Factors.Marked, 6);
    }
}

public class CummyRewardTests
{
    private static EngineRig Rig()
    {
        var r = new EngineRig { Playing = false };
        return r;
    }

    [Fact]
    public void LickGoalsWaitForCummyV5()
    {
        var save = new CharacterSave { Goals = { Seed = 1 } };
        var book = new GoalBook(save);
        for (var day = 0; day < 300; day++)
        {
            book.Roll(EngineRig.T0.AddDays(day));
            Assert.DoesNotContain(save.Goals.Daily.Concat(save.Goals.Weekly), g => g.Id is "d_lick" or "w_lick");
        }
        save.HadCummyV5 = true;
        var seen = new HashSet<string>();
        for (var day = 300; day < 1200; day++)
        {
            book.Roll(EngineRig.T0.AddDays(day));
            foreach (var goal in save.Goals.Daily.Concat(save.Goals.Weekly)) seen.Add(goal.Id);
        }
        Assert.Contains("d_lick", seen);
        Assert.Contains("w_lick", seen);
    }

    [Fact]
    public void AV5SampleMarksIt()
    {
        var r = Rig();
        r.Run(1);
        Assert.False(r.Save.HadCummyV5);
        r.Stages = [];
        r.Run(1);
        Assert.True(r.Save.HadCummyV5);
    }

    [Fact]
    public void HeadToToeFromAFullCoat()
    {
        var r = Rig();
        r.Blocked = ["mo"];
        r.Stages = CummyParts.All.Where(p => p is not ("mo" or "gr")).ToDictionary(p => p, _ => 1);
        r.Run(2);
        Assert.DoesNotContain("head_to_toe", r.Save.Goals.Achievements);
        r.Stages["gr"] = 1;
        r.Run(2);
        Assert.Contains("head_to_toe", r.Save.Goals.Achievements);
    }

    [Fact]
    public void GlazedAtTheFacesTopStage()
    {
        var r = Rig();
        r.PartSizes = new Dictionary<string, int> { ["mo"] = 3 };
        r.Stages = new Dictionary<string, int> { ["mo"] = 2 };
        r.Run(2);
        Assert.DoesNotContain("glazed", r.Save.Goals.Achievements);
        r.Stages["mo"] = 3;
        r.Run(2);
        Assert.Contains("glazed", r.Save.Goals.Achievements);
    }

    [Fact]
    public void CleanupCrewAfterTwentyFiveLicks()
    {
        var save = new CharacterSave();
        var book = new GoalBook(save);
        book.Record(GoalKind.Licks, 25);
        book.TakeRewards();
        Assert.Contains("cleanup_crew", save.Goals.Achievements);
    }

    [Fact]
    public void TheCummyTitlesChangeTheMarkedFactor()
    {
        var b = new Balance();
        var face = new Dictionary<string, int> { ["mo"] = 2 };
        Assert.Equal(1.18, Progression.WeightedMarkedFactor(face, [], b, TitleCatalog.Find("glazed")!.Bonus), 6);
        Assert.Equal(1 + 0.04 * 4, Progression.WeightedMarkedFactor(face, [], b, TitleCatalog.Find("head_to_toe")!.Bonus), 6);
        var lots = new Dictionary<string, int> { ["ch"] = 20 };
        Assert.Equal(1.45, Progression.WeightedMarkedFactor(lots, [], b, TitleCatalog.Find("drenched")!.Bonus), 6);
    }
}

public class FullBodyTests
{
    private static readonly PeerRecord Carol = new() { Name = "Carol C", World = "W" };
    private static readonly PeerRecord Dan = new() { Name = "Dan D", World = "W" };

    /// A duo scene with Bob, two more players nearby in acts, and Cummy version 5.
    private static EngineRig Group(bool v5 = true, double othersAt = 2)
    {
        var r = new EngineRig();
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Where(a => a.Id != "drenched").Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        if (v5)
        {
            r.Stages = [];
            r.Blocked = [];
        }
        r.Host.Aim = ["st", "ch"];
        r.Others =
        [
            new VisiblePlayer(Carol, othersAt, new PoseKey(52, 0), new PresenceTag(new PoseKey(52, 0), 1, 0, 0, false), Body: BodyTag.Empty),
            new VisiblePlayer(Dan, othersAt, new PoseKey(53, 0), new PresenceTag(new PoseKey(53, 0), 1, 0, 0, false), Body: BodyTag.Empty),
        ];
        r.Run(11);
        return r;
    }

    private static void Climaxes(EngineRig r, int index)
    {
        var other = r.Others[index];
        r.Others[index] = other with { Tag = other.Tag with { ClimaxCount = other.Tag.ClimaxCount + 1 } };
        r.Run(1);
    }

    [Fact]
    public void AGroupOfThreeInAMinuteDrenches()
    {
        var r = Group();
        Climaxes(r, 0);
        Climaxes(r, 1);
        r.BobClimaxes();
        Assert.Equal([(r.Bob.Key, "fb")], r.Host.Layers);
        Assert.Contains("You were drenched head to toe.", r.Host.Notes);
        Assert.Equal(1, r.Save.Goals.Stats["FullBodyLayers"]);
        Assert.Contains("drenched", r.Save.Goals.Achievements);
    }

    [Fact]
    public void TheirClimaxesAloneDontLayerYou()
    {
        var r = Group();
        Climaxes(r, 0);
        Climaxes(r, 1);
        Assert.Empty(r.Host.Layers);
    }

    [Fact]
    public void TwoInAMinuteIsJustOnePart()
    {
        var r = Group();
        Climaxes(r, 0);
        r.BobClimaxes();
        Assert.Equal([(r.Bob.Key, "st")], r.Host.Layers);
    }

    [Fact]
    public void OlderClimaxesDontCount()
    {
        var r = Group();
        Climaxes(r, 0);
        Climaxes(r, 1);
        r.Run(61);
        r.BobClimaxes();
        Assert.Equal("st", r.Host.Layers.Single().Part);
    }

    [Fact]
    public void PlayersTooFarAwayDontCount()
    {
        var r = Group(othersAt: 5);
        Climaxes(r, 0);
        Climaxes(r, 1);
        r.BobClimaxes();
        Assert.Equal("st", r.Host.Layers.Single().Part);
    }

    [Fact]
    public void OlderCummyNeverAsksForFullBody()
    {
        var r = Group(v5: false);
        Climaxes(r, 0);
        Climaxes(r, 1);
        r.BobClimaxes();
        Assert.Equal("st", r.Host.Layers.Single().Part);
    }

    [Fact]
    public void ARefusedFullBodyFallsBackToTheAim()
    {
        var r = Group();
        Climaxes(r, 0);
        Climaxes(r, 1);
        r.Host.Outcomes.Enqueue("blocked");
        r.BobClimaxes();
        Assert.Equal(["fb", "st"], r.Host.Layers.Select(l => l.Part));
    }

    [Fact]
    public void TheWindowEmptiesAfterADrenching()
    {
        var r = Group();
        Climaxes(r, 0);
        Climaxes(r, 1);
        r.BobClimaxes();
        r.Run(61);
        Climaxes(r, 0);
        r.BobClimaxes();
        Assert.Equal(["fb", "st"], r.Host.Layers.Select(l => l.Part));
    }

    [Fact]
    public void ACumMagnetDoesntDoubleIt()
    {
        var r = Group();
        r.Save.Effects[ItemIds.CumMagnet] = new ActiveEffect { UsesLeft = 3 };
        Climaxes(r, 0);
        Climaxes(r, 1);
        r.BobClimaxes();
        Assert.Single(r.Host.Layers);
        Assert.Equal(3, r.Save.Effects[ItemIds.CumMagnet].UsesLeft);
    }
}
