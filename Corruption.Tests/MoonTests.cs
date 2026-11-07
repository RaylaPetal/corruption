using Corruption.Core;

namespace Corruption.Tests;

public class MoonTests
{
    private static readonly Balance B = new();
    private static readonly DateTime T0 = EngineRig.T0;
    private static readonly TimeSpan Phase = TimeSpan.FromMinutes(4 * 70);
    private static readonly TimeSpan Cycle = TimeSpan.FromMinutes(32 * 70);

    /// When the next phase of this kind starts, from <paramref name="from"/>.
    private static DateTime NextStart(MoonPhase phase, DateTime from)
    {
        var at = EorzeaMoon.At(from, B).PhaseEndsUtc;
        while (EorzeaMoon.At(at, B).Phase != phase) at = EorzeaMoon.At(at, B).PhaseEndsUtc;
        return at;
    }

    [Fact]
    public void APhaseLastsFourHoursForty()
    {
        var start = EorzeaMoon.At(T0, B).PhaseEndsUtc;
        var end = EorzeaMoon.At(start, B).PhaseEndsUtc;
        Assert.Equal(Phase.TotalSeconds, (end - start).TotalSeconds, 3);
    }

    [Fact]
    public void ThePhasesGoInOrderAndRepeat()
    {
        var at = EorzeaMoon.At(T0, B).PhaseEndsUtc.AddMinutes(1);
        var first = EorzeaMoon.At(at, B);
        for (var i = 1; i <= 8; i++)
        {
            var next = EorzeaMoon.At(at + Phase * i, B);
            Assert.Equal((MoonPhase)(((int)first.Phase + i) % 8), next.Phase);
        }
        Assert.Equal(first.Phase, EorzeaMoon.At(at + Cycle, B).Phase);
        Assert.Equal(first.Cycle + 1, EorzeaMoon.At(at + Cycle, B).Cycle);
    }

    [Fact]
    public void TheSameMomentAlwaysGivesTheSameMoon()
    {
        var at = new DateTime(2026, 11, 7, 13, 37, 0, DateTimeKind.Utc);
        Assert.Equal(EorzeaMoon.At(at, B), EorzeaMoon.At(at, new Balance()));
    }

    [Fact]
    public void TheOffsetShiftsThePhase()
    {
        var at = EorzeaMoon.At(T0, B).PhaseEndsUtc.AddMinutes(1);
        var shifted = EorzeaMoon.At(at, new Balance { MoonOffsetDays = 4 });
        Assert.Equal((MoonPhase)(((int)EorzeaMoon.At(at, B).Phase + 1) % 8), shifted.Phase);
    }

    [Fact]
    public void TheNextEventIsAFullOrNewMoon()
    {
        var full = NextStart(MoonPhase.FullMoon, T0);
        var during = EorzeaMoon.At(full.AddMinutes(1), B);
        Assert.Equal(MoonPhase.FullMoon, during.Phase);
        Assert.True(during.IsEvent);
        Assert.Equal(MoonPhase.NewMoon, during.NextEvent);
        Assert.Equal(full + Cycle / 2, during.NextEventUtc);

        var before = EorzeaMoon.At(full.AddMinutes(-1), B);
        Assert.Equal(MoonPhase.WaxingGibbous, before.Phase);
        Assert.Equal(full, before.NextEventUtc);
    }

    // ── In a scene ──────────────────────────────────────────────────────────

    private static EngineRig At(DateTime start)
    {
        var r = new EngineRig(balance: new Balance()) { Now = start };
        r.Save.ArousalAnchorUtc = start;
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Where(a => a.Id != "moon_touched").Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        return r;
    }

    [Fact]
    public void AFullMoonBuildsArousalFaster()
    {
        var r = At(NextStart(MoonPhase.FullMoon, T0).AddMinutes(1));
        r.Run(11);
        Assert.Equal(1.65, r.Save.Arousal, 6);
    }

    [Fact]
    public void ANewMoonPaysMoreSinsForSceneTime()
    {
        var newMoon = At(NextStart(MoonPhase.NewMoon, T0).AddMinutes(1));
        var quarter = At(NextStart(MoonPhase.FirstQuarter, T0).AddMinutes(1));
        newMoon.Run(601);
        quarter.Run(601);
        // 15 Sins against 10 for ten minutes. Everything else pays the same to both.
        Assert.Equal(5, newMoon.Save.Wallet.Sins - quarter.Save.Wallet.Sins);
    }

    [Fact]
    public void OnlyTicksDuringTheFullMoonGetIt()
    {
        var r = At(NextStart(MoonPhase.FullMoon, T0).AddSeconds(-15));
        r.Run(11);
        Assert.Equal(1.5, r.Save.Arousal, 6);
        r.Run(10);
        Assert.Equal(1.5 + 1.65, r.Save.Arousal, 6);
    }

    [Fact]
    public void EachFullMoonCountsOnceTowardMoonTouched()
    {
        var full = NextStart(MoonPhase.FullMoon, T0);
        var r = At(full.AddMinutes(1));
        r.Run(120);
        Assert.Equal(1, r.Save.FullMoonsSeen);

        for (var i = 1; i < 4; i++)
        {
            r.Now = full + Cycle * i + TimeSpan.FromMinutes(1);
            r.Run(11);
        }
        Assert.Equal(4, r.Save.FullMoonsSeen);
        Assert.DoesNotContain("moon_touched", r.Save.Goals.Achievements);

        r.Now = full + Cycle * 4 + TimeSpan.FromMinutes(1);
        r.Run(11);
        Assert.Contains("moon_touched", r.Save.Goals.Achievements);
        Assert.True(TitleBook.IsUnlocked(r.Save, "moon_touched"));
    }

    [Fact]
    public void NoTickMeansNoFullMoonCounted()
    {
        var r = At(NextStart(MoonPhase.FullMoon, T0).AddMinutes(1));
        r.Playing = false;
        r.Run(60);
        Assert.Equal(0, r.Save.FullMoonsSeen);
    }
}
