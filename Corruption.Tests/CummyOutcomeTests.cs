using Corruption.Core;

namespace Corruption.Tests;

/// Cummy v5's extra outcomes end a layer attempt as nothing having happened.
public class CummyOutcomeTests
{
    private static EngineRig Partnered()
    {
        var r = new EngineRig();
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        r.Host.Aim = ["st", "ch", "gr"];
        r.Run(11);
        return r;
    }

    [Theory]
    [InlineData("blacklisted")]
    [InlineData("wrong-thread")]
    [InlineData("error")]
    public void AnUnusableOutcomeEndsTheAttempt(string outcome)
    {
        var r = Partnered();
        r.Host.LayerOutcome = outcome;
        var before = r.Save.Xp;
        r.BobClimaxes();
        Assert.Single(r.Host.Layers);
        Assert.Contains(r.Host.Notes, n => n == "Bob B climaxed.");
        Assert.False(r.Save.Goals.Stats.ContainsKey("LayersReceived"));
    }

    [Fact]
    public void BlockedStillMovesOn()
    {
        var r = Partnered();
        r.Host.Outcomes.Enqueue("blocked");
        r.BobClimaxes();
        Assert.Equal(["st", "ch"], r.Host.Layers.Select(l => l.Part));
    }
}
