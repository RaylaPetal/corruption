using Corruption.Core;

namespace Corruption.Tests;

public class SceneSummaryTests
{
    private static EngineRig Plain()
    {
        var r = new EngineRig(balance: TestBalance.Classic());
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        return r;
    }

    private static void End(EngineRig r)
    {
        r.Playing = false;
        r.Run(31);
    }

    [Fact]
    public void ASceneAddsUpWhenItEnds()
    {
        var r = Plain();
        r.Watchers = 3;
        r.Run(14 * 60);
        r.Watchers = 1;
        r.Run(10);
        End(r);

        var summary = Assert.Single(r.Host.Scenes);
        Assert.Equal(14 * 60 + 10, summary.VerifiedSeconds);
        Assert.Equal(3, summary.PeakWatchers);
        Assert.Equal([r.Bob.Key], summary.Partners);
        Assert.True(summary.Xp > 0);
        Assert.True(summary.Sins >= 14);
        Assert.Same(summary, r.Save.LastScene);
    }

    [Fact]
    public void ItCountsClimaxesBothWays()
    {
        var r = Plain();
        r.Run(11);
        r.Save.Arousal = 99.9;
        r.Run(10);
        r.Run(20);
        r.BobClimaxes();
        End(r);
        var summary = Assert.Single(r.Host.Scenes);
        Assert.Equal(1, summary.OwnClimaxes);
        Assert.Equal(1, summary.PartnerClimaxes);
    }

    [Fact]
    public void XpIncludesClimaxesAndMatchesTheSave()
    {
        var r = Plain();
        r.Save.Level = 50;
        var before = r.Save.Xp;
        r.Run(11);
        r.Save.Arousal = 99.9;
        r.Run(10);
        End(r);
        var summary = Assert.Single(r.Host.Scenes);
        Assert.Equal(r.Save.Xp - before, summary.Xp, 6);
        Assert.True(summary.Xp > r.Balance.ClimaxXp);
    }

    [Fact]
    public void ANewPartnerMidSceneIsListedToo()
    {
        var r = Plain();
        r.Run(60);
        var carol = new PeerRecord { Name = "Carol C", World = "W" };
        r.Bob.Name = carol.Name;
        r.Run(60);
        End(r);
        Assert.Equal(["Bob B@W", "Carol C@W"], Assert.Single(r.Host.Scenes).Partners);
    }

    [Fact]
    public void AShortPauseIsntCounted()
    {
        var r = Plain();
        r.Run(60);
        r.Engine.Pause();
        r.Run(20);
        r.Engine.Resume();
        r.Run(60);
        End(r);
        var summary = Assert.Single(r.Host.Scenes);
        Assert.Equal(120, summary.VerifiedSeconds);
    }

    [Fact]
    public void ALongPauseEndsTheScene()
    {
        var r = Plain();
        r.Run(60);
        r.Engine.Pause();
        r.Run(40);
        var summary = Assert.Single(r.Host.Scenes);
        Assert.Equal(60, summary.VerifiedSeconds);
    }

    [Fact]
    public void PrestigeKeepsTheLastSceneAndResetClearsIt()
    {
        var r = Plain();
        r.Run(30);
        End(r);
        r.Save.Level = 100;
        Assert.True(PrestigeRules.Apply(r.Save, r.Now, r.Balance));
        Assert.NotNull(r.Save.LastScene);
        r.Save.ResetProgress(r.Now);
        Assert.Null(r.Save.LastScene);
    }

    // ── Partner history ─────────────────────────────────────────────────────

    [Fact]
    public void ASecondSceneAddsToTheRecord()
    {
        var r = Plain();
        r.Run(10 * 60);
        End(r);
        r.Playing = true;
        r.Run(20 * 60);
        End(r);

        var record = PartnerBook.Of(r.Save, r.Bob.Key)!;
        Assert.Equal(2, record.Scenes);
        Assert.Equal(30 * 60, record.Seconds);
        Assert.Equal(EngineRig.T0, record.FirstUtc);
        Assert.True(record.LastUtc > EngineRig.T0.AddMinutes(30));
    }

    [Fact]
    public void OlderCountsAreSeededOnce()
    {
        var r = Plain();
        r.Save.Goals.PartnerScenes["Dan D@W"] = 12;
        r.Playing = false;
        r.Run(1);
        var record = PartnerBook.Of(r.Save, "Dan D@W")!;
        Assert.Equal(12, record.Scenes);
        Assert.Null(record.LastUtc);

        r.Save.Goals.PartnerScenes["Dan D@W"] = 13;
        r.Run(1);
        Assert.Equal(12, PartnerBook.Of(r.Save, "Dan D@W")!.Scenes);
    }

    [Fact]
    public void AResetClearsPartnerHistory()
    {
        var r = Plain();
        r.Run(30);
        r.Save.ResetProgress(r.Now);
        Assert.Empty(r.Save.Partners);
    }
}
