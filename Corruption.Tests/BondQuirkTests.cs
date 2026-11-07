using Corruption.Core;

namespace Corruption.Tests;

public class BondQuirkTests
{
    private static EngineRig Bonded(double hours, bool layering = false)
    {
        var r = new EngineRig();
        r.Engine.LayerOnPartnerClimax = layering;
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(x => x.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        r.Save.BondWith = r.Bob.Key;
        r.Save.BondFormed = true;
        r.Save.BondSeconds = hours * 3600;
        r.BobExtra = new ExtraTag(0, BondHashes.Of(r.SelfKey!));
        return r;
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3599, 1)]
    [InlineData(3600, 2)]
    [InlineData(3 * 3600, 3)]
    [InlineData(6 * 3600, 4)]
    [InlineData(12 * 3600, 5)]
    [InlineData(100 * 3600, 5)]
    public void Thresholds(double seconds, int level) => Assert.Equal(level, BondLevels.LevelOf(seconds));

    [Fact]
    public void LevelOnePlayersCanBond()
    {
        var r = new EngineRig { Playing = false };
        r.Run(1);
        Assert.Null(r.Engine.RequestBond(r.Bob));
        Assert.Equal(r.Bob.Key, r.Save.BondWith);
    }

    [Fact]
    public void TimeTogetherRaisesTheLevelAndSaysSo()
    {
        var r = Bonded(1 - 5 / 3600.0);
        Assert.Equal(1, r.Engine.Bonds.Level);
        r.Run(11);
        Assert.Equal(2, r.Engine.Bonds.Level);
        Assert.Contains(r.Host.Notes, n => n.Contains("reached level 2"));
    }

    [Fact]
    public void InTuneSpeedsArousalOnlyWithThem()
    {
        var r = Bonded(1);
        r.Run(11);
        Assert.Equal(3 * 1.1, r.Save.Arousal, 6);

        var other = Bonded(1);
        other.Save.BondWith = "Carol C@W";
        other.Run(11);
        Assert.Equal(3, other.Save.Arousal, 6);
    }

    [Fact]
    public void DevotedPaysMoreAndHalvesRefractory()
    {
        var r = Bonded(12);
        r.Run(11);
        Assert.Equal(6 * 1.35, r.Save.Xp, 6);
        r.Save.Arousal = 99;
        r.Run(10);
        Assert.Contains("You climaxed.", r.Host.Notes);
        // A 30 second refractory instead of 60, measured from a climax up to 10 seconds ago.
        Assert.InRange((r.Save.RefractoryUntilUtc - r.Now).TotalSeconds, 20, 30);
    }

    [Fact]
    public void LevelFourAndBelowPayTheUsualBond()
    {
        var r = Bonded(6);
        r.Run(11);
        Assert.Equal(6 * 1.25, r.Save.Xp, 6);
        Assert.Equal(1.25, r.Engine.Status.Factors.Bonded, 6);
    }

    [Fact]
    public void TogetherPaysOncePerPair()
    {
        var r = Bonded(3);
        r.Save.Level = 50;
        r.Run(11);
        r.Save.Arousal = 99;
        r.Run(10);
        Assert.Contains("You climaxed.", r.Host.Notes);
        r.BobClimaxes();
        Assert.Single(r.Host.Notes, n => n.Contains("climaxed together"));
        r.Run(20);
        Assert.Single(r.Host.Notes, n => n.Contains("climaxed together"));
    }

    [Fact]
    public void NotTogetherBelowLevelThree()
    {
        var r = Bonded(2);
        r.Save.Level = 50;
        r.Run(11);
        r.Save.Arousal = 99;
        r.Run(10);
        r.BobClimaxes();
        Assert.DoesNotContain(r.Host.Notes, n => n.Contains("climaxed together"));
    }

    [Fact]
    public void UnbondingAndResetClearTheTime()
    {
        var r = Bonded(5);
        r.Engine.Unbond();
        Assert.Equal(0, r.Save.BondSeconds);
        r.Save.BondSeconds = 100;
        r.Save.ResetProgress(r.Now);
        Assert.Equal(0, r.Save.BondSeconds);
    }

    [Fact]
    public void ANewBondStartsAtLevelOne()
    {
        var save = new CharacterSave { BondSeconds = 99999 };
        var book = new BondBook(save);
        book.Request("Bob B@W");
        book.Observe("Alice A@W", [(new PeerRecord { Name = "Bob B", World = "W" }, new ExtraTag(0, BondHashes.Of("Alice A@W")))]);
        Assert.Equal(1, book.Level);
    }
}
