using Corruption.Core;

namespace Corruption.Tests;

/// A partner's credited climax layers this player through their own Cummy: no tell and no pairing.
public class LayerSelfTests
{
    private static EngineRig Scene(Random? random = null)
    {
        var r = new EngineRig(random);
        r.Save.Goals.Achievements.Add("first_taste");
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        r.Save.Level = 50;
        r.Run(11);
        return r;
    }

    [Fact]
    public void PartnersClimaxLayersMeFromThem()
    {
        var r = Scene();
        r.BobClimaxes();
        var layer = Assert.Single(r.Host.Layers);
        Assert.Equal(r.Bob.Key, layer.From);
        Assert.Contains(layer.Part, CummyParts.All);
    }

    [Theory]
    [InlineData("layered", 75)]
    [InlineData("advanced", 75)]
    [InlineData("full", 0)]
    [InlineData("no-character", 0)]
    [InlineData(null, 0)]
    public void LayerXpOnlyWhenItLands(string? outcome, double expected)
    {
        var r = Scene();
        r.Host.LayerOutcome = outcome;
        var before = r.Save.Xp;
        r.BobClimaxes();
        var partnerShare = r.Balance.ClimaxXp * r.Balance.PartnerClimaxShare;
        Assert.InRange(r.Save.Xp - before - partnerShare - expected, -0.001, 10);
        Assert.Equal(expected > 0 ? 1 : 0, r.Save.Goals.Stats.GetValueOrDefault("LayersReceived"));
    }

    [Fact]
    public void BlockedPartsTryAnother()
    {
        var r = Scene();
        r.Host.Outcomes.Enqueue("blocked");
        r.Host.Outcomes.Enqueue("blocked");
        r.BobClimaxes();
        Assert.Equal(3, r.Host.Layers.Count);
        Assert.Equal(3, r.Host.Layers.Select(l => l.Part).Distinct().Count());
    }

    [Fact]
    public void EveryPartBlockedStopsWithNoXp()
    {
        var r = Scene();
        r.Host.LayerOutcome = "blocked";
        r.BobClimaxes();
        Assert.Equal(CummyParts.All.Count, r.Host.Layers.Count);
        Assert.Equal(0, r.Save.Goals.Stats.GetValueOrDefault("LayersReceived"));
    }

    [Fact]
    public void PartnerOnTheOlderVersionStillSendsTheirOwnTell()
    {
        var r = Scene();
        r.BobBody = null;
        r.Run(1);
        r.BobClimaxes();
        Assert.Empty(r.Host.Layers);
    }

    [Fact]
    public void TurnedOff()
    {
        var r = Scene();
        r.Engine.LayerOnPartnerClimax = false;
        r.BobClimaxes();
        Assert.Empty(r.Host.Layers);
    }

    [Fact]
    public void MyOwnClimaxSendsNothing()
    {
        var r = Scene();
        r.Save.Arousal = 98;
        r.Run(20);
        Assert.Contains("You climaxed.", r.Host.Notes);
        Assert.Empty(r.Host.Layers);
    }

    [Fact]
    public void StrayClimaxLayersNothing()
    {
        var r = new EngineRig { Playing = false };
        r.Run(2);
        r.BobClimaxes();
        Assert.Empty(r.Host.Layers);
    }

    [Fact]
    public void RandomPartsVary()
    {
        var seen = new HashSet<string>();
        for (var seed = 0; seed < 40; seed++)
        {
            var r = Scene(new Random(seed));
            r.BobClimaxes();
            seen.Add(r.Host.Layers.Single().Part);
        }
        Assert.True(seen.Count >= 8);
    }
}
