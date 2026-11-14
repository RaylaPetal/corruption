using Corruption.Core;

namespace Corruption.Tests;

/// A conception stays secret until a Pregnancy Test or day 5 confirms it.
public class SecretPregnancyTests
{
    private static readonly DateTime T0 = EngineRig.T0;

    /// A carrier on <paramref name="day"/> of her cycle who conceived at T0, secretly.
    private static CharacterSave Conceived(int day = 5)
    {
        var save = new CharacterSave { Setup = new ProfileSetup(), Level = 30 };
        PregnancyBook.Enable(save.Pregnancy, true, false, day, T0);
        PregnancyBook.Conceive(save.Pregnancy, T0, "Bob B@W", "Milkers");
        return save;
    }

    [Fact]
    public void AFreshConceptionIsSecret()
    {
        var save = Conceived();
        var p = save.Pregnancy;
        var later = T0.AddHours(1);
        Assert.True(PregnancyBook.IsPregnant(p));
        Assert.True(PregnancyBook.IsSecret(p));
        Assert.False(PregnancyBook.IsConfirmed(p));
        Assert.Equal(PregnancyStage.None, PregnancyBook.ShownStage(p, later));
        Assert.Equal(PregnancyStage.First, PregnancyBook.StageOf(p, later));
        Assert.True(PregnancyBook.ShowsCycle(p, later));
        Assert.False(PregnancyBook.HasCycle(p, later));
        Assert.Equal(0.40, PregnancyBook.ShownConceptionChance(save, later, false, 0, false), 6);
        Assert.Equal(0, PregnancyBook.ConceptionChance(save, later, false, 0, false));
        Assert.Equal(0, p.Conceptions);
        Assert.Equal("Milkers", p.ConceivedSource);
    }

    [Fact]
    public void TheQuietEffectsApplyFromConception()
    {
        var p = Conceived().Pregnancy;
        Assert.Equal(PregnancyBook.PregnantTickXp, PregnancyBook.TickXpFactor(p));
        Assert.Equal(0.9, PregnancyBook.ArousalFactor(p, T0.AddHours(1)), 6);
        Assert.Equal(T0.AddDays(PregnancyBook.DaysLong), p.DueUtc);
    }

    [Fact]
    public void ConfirmingCountsTheConceptionAndCravesFromTomorrow()
    {
        var p = Conceived().Pregnancy;
        var now = T0.AddHours(2);
        Assert.True(PregnancyBook.Confirm(p, now));
        Assert.Equal(1, p.Conceptions);
        Assert.Equal(now, p.ConfirmedUtc);
        Assert.False(PregnancyBook.Confirm(p, now));
        Assert.Equal(1, p.Conceptions);
        Assert.Null(CravingBook.Roll(p, now, 1, false));
        Assert.NotNull(CravingBook.Roll(p, now.Date.AddDays(1), 1, false));
    }

    [Fact]
    public void NoCravingsWhileSecret()
    {
        var p = Conceived().Pregnancy;
        Assert.Null(CravingBook.Roll(p, T0.AddDays(1), 1, false));
        p.Craving = CravingKind.Climax;
        Assert.False(CravingBook.Met(p, CravingKind.Climax));
    }

    [Fact]
    public void AnOldPregnancyIsConfirmedOnce()
    {
        var p = new PregnancyState { Enabled = true, CanCarry = true, ConceivedUtc = T0, DueUtc = T0.AddDays(14), FatherKey = "Bob B@W", Conceptions = 1 };
        PregnancyBook.Migrate(p);
        Assert.True(PregnancyBook.IsConfirmed(p));
        Assert.Equal(T0, p.ConfirmedUtc);
        Assert.Equal(1, p.Conceptions);
        Assert.Equal(PregnancyBook.StateVersion, p.Version);
    }

    [Fact]
    public void MigrationNeverConfirmsANewPregnancy()
    {
        var p = new PregnancyState { Enabled = true, CanCarry = true };
        PregnancyBook.Migrate(p);
        PregnancyBook.Conceive(p, T0, "Bob B@W");
        PregnancyBook.Migrate(p);
        Assert.True(PregnancyBook.IsSecret(p));

        // A save that never ran the migration still can't confirm a conception made since.
        var fresh = new PregnancyState { Enabled = true, CanCarry = true };
        PregnancyBook.Conceive(fresh, T0, "Bob B@W");
        PregnancyBook.Migrate(fresh);
        Assert.True(PregnancyBook.IsSecret(fresh));
    }

    [Fact]
    public void StillSecretOnDayFourPointNine()
    {
        var p = Conceived().Pregnancy;
        Assert.Equal(PregnancyEvent.None, PregnancyBook.Update(p, T0.AddDays(4.9)));
        Assert.True(PregnancyBook.IsSecret(p));
    }

    [Fact]
    public void ShowsOnDayFive()
    {
        var p = Conceived().Pregnancy;
        Assert.Equal(PregnancyEvent.Revealed, PregnancyBook.Update(p, T0.AddDays(5)));
        Assert.True(PregnancyBook.IsConfirmed(p));
        Assert.Equal(1, p.Conceptions);
        Assert.Equal(PregnancyEvent.None, PregnancyBook.Update(p, T0.AddDays(5.1)));
    }

    [Fact]
    public void ShowsOnTheFirstUpdateAfterAnOfflineDayFive()
    {
        var p = Conceived().Pregnancy;
        Assert.Equal(PregnancyEvent.Revealed, PregnancyBook.Update(p, T0.AddDays(8)));
        Assert.Equal(PregnancyStage.Second, PregnancyBook.ShownStage(p, T0.AddDays(8)));
    }

    [Fact]
    public void DueWhileOfflineAndNeverTestedStillCountsTheConception()
    {
        var p = Conceived().Pregnancy;
        Assert.Equal(PregnancyEvent.Birth, PregnancyBook.Update(p, T0.AddDays(20)));
        Assert.Equal(1, p.Conceptions);
        Assert.Equal(1, p.Births);
    }

    [Fact]
    public void TestResults()
    {
        var p = Conceived().Pregnancy;
        Assert.Equal(PregnancyBook.TestResult.Positive, PregnancyBook.Test(p, T0.AddHours(2)));
        Assert.Equal(PregnancyBook.TestResult.AlreadyKnown, PregnancyBook.Test(p, T0.AddHours(3)));
        var none = new PregnancyState { Enabled = true, CanCarry = true };
        Assert.Equal(PregnancyBook.TestResult.Negative, PregnancyBook.Test(none, T0));
    }

    // ── Items ──────────────────────────────────────────────────────────────

    [Fact]
    public void TheMorningAfterPillIsTheSameEitherWay()
    {
        var none = new PregnancyState { Enabled = true, CanCarry = true };
        Assert.Null(PregnancyBook.MorningAfter(none, T0));

        var p = Conceived().Pregnancy;
        Assert.Null(PregnancyBook.MorningAfter(p, T0.AddHours(10)));
        Assert.False(PregnancyBook.IsPregnant(p));
        Assert.Equal(1, CycleClock.DayAt(p.CycleStartUtc, T0.AddHours(10)));
    }

    [Fact]
    public void TheMorningAfterPillTooLate()
    {
        // Secret and too late: it says the same and does nothing, so it can't be used as a test.
        var secret = Conceived().Pregnancy;
        Assert.Null(PregnancyBook.MorningAfter(secret, T0.AddDays(3)));
        Assert.True(PregnancyBook.IsSecret(secret));

        var known = Conceived().Pregnancy;
        PregnancyBook.Confirm(known, T0.AddHours(1));
        Assert.NotNull(PregnancyBook.MorningAfter(known, T0.AddDays(3)));
        Assert.True(PregnancyBook.IsConfirmed(known));
    }

    [Fact]
    public void HormoneTeaMovesTheShownCycle()
    {
        var p = Conceived(day: 1).Pregnancy;
        var now = T0.AddHours(3);
        Assert.Null(PregnancyBook.HormoneTea(p, now));
        Assert.Equal(4, CycleClock.DayAt(p.CycleStartUtc, now));
        Assert.True(PregnancyBook.IsSecret(p));

        PregnancyBook.Confirm(p, now);
        Assert.Equal("You're pregnant.", PregnancyBook.HormoneTea(p, now));
    }

    [Fact]
    public void SwiftLabourNeedsAConfirmedPregnancy()
    {
        var p = Conceived().Pregnancy;
        Assert.Equal(PregnancyBook.NeedsConfirmed, PregnancyBook.SwiftLabour(p));
        Assert.Equal(PregnancyBook.NeedsConfirmed, PregnancyBook.SwiftLabour(new PregnancyState { Enabled = true, CanCarry = true }));
        PregnancyBook.Confirm(p, T0);
        Assert.Null(PregnancyBook.SwiftLabour(p));
    }

    [Fact]
    public void TurningCarryingOffEndsASecretPregnancyQuietly()
    {
        var p = Conceived().Pregnancy;
        Assert.Null(PregnancyBook.Enable(p, false, true, 1, T0));
        Assert.False(PregnancyBook.IsPregnant(p));

        var known = Conceived().Pregnancy;
        PregnancyBook.Confirm(known, T0);
        Assert.NotNull(PregnancyBook.Enable(known, false, true, 1, T0));
    }

    // ── What shows ─────────────────────────────────────────────────────────

    [Fact]
    public void MoodlesShowNoPregnancyWhileSecret()
    {
        var p = Conceived(day: 4).Pregnancy;
        var now = T0.AddHours(1);
        var input = MoodlesPlan.WithPregnancy(new MoodlesInput(true, 0, 0, false, 0, false, false, false), p, now);
        var plan = MoodlesPlan.Plan(input);
        Assert.False(plan.ContainsKey(MoodleKind.Pregnant));
        Assert.True(plan.ContainsKey(MoodleKind.Fertile));

        PregnancyBook.Confirm(p, now);
        plan = MoodlesPlan.Plan(MoodlesPlan.WithPregnancy(input, p, now));
        Assert.Equal(1, plan[MoodleKind.Pregnant]);
        Assert.False(plan.ContainsKey(MoodleKind.Fertile));
    }

    [Fact]
    public void NoTrimesterLookWhileSecret()
    {
        var save = Conceived();
        save.Pregnancy.Visuals["First"] = new TierVisual { Kind = VisualKind.Penumbra, ModDirectory = "bump" };
        Assert.Null(PregnancyVisualPlan.Wanted(save, T0.AddHours(1), false, true));
        PregnancyBook.Confirm(save.Pregnancy, T0.AddHours(1));
        Assert.Equal("bump", PregnancyVisualPlan.Wanted(save, T0.AddHours(1), false, true)!.ModDirectory);
    }

    [Fact]
    public void TheApiReadsNoneWhileSecret()
    {
        var save = Conceived();
        var now = T0.AddHours(1);
        var read = ApiSnapshots.Pregnancy(save, "Carol C@W", now);
        Assert.Equal("none", read.Stage);
        Assert.Equal("", read.FatherName);
        Assert.Equal(default, read.DueUtc);
        Assert.Equal(0, read.Conceptions);

        var cycle = ApiSnapshots.Cycle(save, "Carol C@W", now);
        Assert.True(cycle.Running);
        Assert.Equal(5, cycle.Day);
        Assert.Equal("ovulation", cycle.Phase);
        Assert.Equal(0.40, cycle.Chance, 6);
    }

    [Fact]
    public void TheApiReadsTheSecondTrimesterOnceShown()
    {
        var save = Conceived();
        var now = T0.AddDays(7);
        PregnancyBook.Update(save.Pregnancy, now);
        var read = ApiSnapshots.Pregnancy(save, "Carol C@W", now);
        Assert.Equal("second", read.Stage);
        Assert.Equal("Bob B", read.FatherName);
        Assert.False(ApiSnapshots.Cycle(save, "Carol C@W", now).Running);
    }

    [Fact]
    public void ThePregnancyTestIsInTheShop()
    {
        var save = Conceived();
        var test = ItemCatalog.Find(ItemIds.PregnancyTest)!;
        Assert.Equal(15, test.Price);
        Assert.Equal(ItemCategory.PregnancyCare, ItemCatalog.CategoryOf(test));
        Assert.Contains(test, ItemBook.Offered(save));
        Assert.DoesNotContain(test, ItemBook.Offered(new CharacterSave()));
    }
}

/// The engine's side: silence at conception, the test, the reveal and the items.
public class SecretPregnancyEngineTests
{
    /// A carrier on day 5, secretly pregnant by Bob from a plugin.
    private static EngineRig Secret()
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon()) { Playing = false };
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Where(a => a.Id != "expecting").Select(a => a.Id));
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, 5, r.Now);
        r.Run(1);
        PregnancyBook.Conceive(r.Save.Pregnancy, r.Now, r.Bob.Key, "Milkers");
        r.Run(1);
        return r;
    }

    private static string? Use(EngineRig r, string id)
    {
        r.Save.Inventory[id] = r.Save.Inventory.GetValueOrDefault(id) + 1;
        return r.Engine.UseItem(id, r.Now);
    }

    [Fact]
    public void APositiveTestConfirmsAndUnlocksExpecting()
    {
        var r = Secret();
        var conceived = new List<string>();
        r.Engine.Conceived += (name, world, source) => conceived.Add($"{name}@{world} {source}");
        Assert.Null(Use(r, ItemIds.PregnancyTest));
        Assert.Contains($"Positive. You're pregnant with {r.Bob.Name}'s child.", r.Host.Notes);
        Assert.Equal([$"{r.Bob.Key} Milkers"], conceived);
        Assert.True(PregnancyBook.IsConfirmed(r.Save.Pregnancy));
        Assert.Equal(0, r.Save.Inventory.GetValueOrDefault(ItemIds.PregnancyTest));
        r.Run(1);
        Assert.Contains("expecting", r.Save.Goals.Achievements);
    }

    [Fact]
    public void ANegativeTestIsUsedUp()
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon()) { Playing = false };
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, 5, r.Now);
        r.Run(1);
        Assert.Null(Use(r, ItemIds.PregnancyTest));
        Assert.Contains("Negative. You aren't pregnant.", r.Host.Notes);
        Assert.Equal(0, r.Save.Inventory.GetValueOrDefault(ItemIds.PregnancyTest));
    }

    [Fact]
    public void ATestIsKeptWhenYouAlreadyKnow()
    {
        var r = Secret();
        PregnancyBook.Confirm(r.Save.Pregnancy, r.Now);
        Assert.NotNull(Use(r, ItemIds.PregnancyTest));
        Assert.Equal(1, r.Save.Inventory[ItemIds.PregnancyTest]);
    }

    [Fact]
    public void ShowingAtDayFiveTellsTheCarrier()
    {
        var r = Secret();
        var conceived = 0;
        r.Engine.Conceived += (_, _, _) => conceived++;
        r.Now = r.Now.AddDays(5);
        r.Run(1);
        Assert.Contains($"Your belly is starting to show. You're pregnant with {r.Bob.Name}'s child.", r.Host.Notes);
        Assert.Equal(1, conceived);
        Assert.True(PregnancyBook.IsConfirmed(r.Save.Pregnancy));
    }

    [Fact]
    public void ThePillSaysTheSameEitherWay()
    {
        var pregnant = Secret();
        Assert.Null(Use(pregnant, ItemIds.MorningAfter));
        Assert.False(PregnancyBook.IsPregnant(pregnant.Save.Pregnancy));

        var not = new EngineRig(balance: new Balance().WithoutMoon()) { Playing = false };
        PregnancyBook.Enable(not.Save.Pregnancy, true, false, 5, not.Now);
        not.Run(1);
        Assert.Null(Use(not, ItemIds.MorningAfter));

        Assert.Equal(pregnant.Host.Notes.Where(n => n.Contains("pill")), not.Host.Notes.Where(n => n.Contains("pill")));
        Assert.Contains(PregnancyBook.PillTaken, not.Host.Notes);
    }

    [Fact]
    public void ACravingTreatNeedsAConfirmedPregnancy()
    {
        var r = Secret();
        Assert.Equal(PregnancyBook.NeedsConfirmed, Use(r, ItemIds.CravingTreat));
        Assert.Equal(1, r.Save.Inventory[ItemIds.CravingTreat]);
    }

    [Fact]
    public void NoTrimesterEventWhileSecret()
    {
        var r = Secret();
        var stages = new List<PregnancyStage>();
        r.Engine.TrimesterChanged += stages.Add;
        r.Run(3);
        Assert.Empty(stages);
    }
}

/// A plugin's "always a creampie" switch: set on the bull's side, it travels in his body tag, and the cow's client makes
/// only his credited climaxes creampie her pussy.
public class ForcedCreampieTests
{
    /// The cow: on day 5 in a scene with Bob, a tagged breeder whose tip the bones don't find inside.
    private static EngineRig Cow(bool bullForces = true, Anatomy own = Anatomy.NotSet)
    {
        var r = new EngineRig(new FixedRandom(0.1));
        r.Save.Level = 50;
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.Anatomy = own;
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, 5, r.Now);
        r.BobBody = new BodyTag(BodyFlags.CanBreed | (bullForces ? BodyFlags.AlwaysInside : 0), PregnancyStage.None, null);
        r.Host.Aim = ["gr", "st", "ll", "lr", "bt"];
        r.Creampies = [];
        r.Host.InsideHole = null;
        r.Run(11);
        return r;
    }

    /// The bull's own client.
    private static EngineRig Bull(bool consent = true)
    {
        var r = new EngineRig();
        r.Save.PluginConsent.Allowed = consent;
        r.Run(1);
        return r;
    }

    private static string Force(EngineRig r, bool on) => r.Engine.SetForceCreampieFromPlugin(r.Now, on, "Milkers");

    [Fact]
    public void TheBullsSwitchGoesOutInHisTag()
    {
        var r = Bull();
        Assert.False(r.Engine.OwnBodyAt(r.Now).AlwaysInside);
        Assert.Equal(ApiOutcomes.Ok, Force(r, true));
        Assert.True(r.Engine.ForcesCreampies);
        Assert.True(r.Engine.OwnBodyAt(r.Now).AlwaysInside);
        Assert.Equal("ForceCreampie on", r.Engine.PluginCalls[0].Call);
        Force(r, false);
        Assert.False(r.Engine.OwnBodyAt(r.Now).AlwaysInside);
    }

    [Fact]
    public void RefusedWithoutTheBullsConsent()
    {
        var r = Bull(consent: false);
        Assert.Equal(ApiOutcomes.NotAllowed, Force(r, true));
        Assert.False(r.Engine.OwnBodyAt(r.Now).AlwaysInside);
    }

    [Fact]
    public void OffAlwaysWorks()
    {
        var r = Bull();
        Force(r, true);
        r.Save.PluginConsent.Allowed = false;
        Assert.False(r.Engine.ForcesCreampies);
        Assert.Equal(ApiOutcomes.Ok, Force(r, false));
        r.Save.PluginConsent.Allowed = true;
        Assert.False(r.Engine.ForcesCreampies);
    }

    [Fact]
    public void TheTagSurvivesOlderAndNewerClients()
    {
        var tag = new BodyTag(BodyFlags.CanBreed | BodyFlags.AlwaysInside, PregnancyStage.None, null);
        Assert.True(BodyTag.Parse(tag.Format())!.Value.AlwaysInside);
        Assert.Equal(BodyFlags.CanBreed, BodyTag.Parse("P1|130|0|-")!.Value.Flags);
    }

    [Fact]
    public void ABullWhoAlwaysComesInsideCreampiesTheCow()
    {
        var r = Cow();
        r.BobClimaxes();
        Assert.Equal([(r.Bob.Key, CreampieAim.Pussy)], r.Host.Creampies);
        Assert.True(PregnancyBook.IsSecret(r.Save.Pregnancy));
    }

    [Fact]
    public void OtherPartnersArentForced()
    {
        var r = Cow(bullForces: false);
        r.BobClimaxes();
        Assert.Empty(r.Host.Creampies);
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
    }

    [Fact]
    public void TheCowsOwnSwitchDoesntForceHerPartners()
    {
        var r = Cow(bullForces: false);
        r.Save.PluginConsent.Allowed = true;
        Force(r, true);
        r.BobClimaxes();
        Assert.Empty(r.Host.Creampies);
    }

    [Fact]
    public void AHoleTheBonesFindStillWins()
    {
        var r = Cow();
        r.Host.InsideHole = CreampieAim.Ass;
        r.BobClimaxes();
        Assert.Equal([(r.Bob.Key, CreampieAim.Ass)], r.Host.Creampies);
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
    }

    [Fact]
    public void APenisSetCowIsntForced()
    {
        var r = Cow(own: Anatomy.Penis);
        r.BobClimaxes();
        Assert.Empty(r.Host.Creampies);
    }

    [Fact]
    public void CummysBlockStillStopsIt()
    {
        var r = Cow();
        r.Host.CreampieOutcome = "blocked";
        r.BobClimaxes();
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
    }
}

/// Another player creampying this character straight from Cummy's menu, with no Corruption climax.
public class CummyCreampieTests
{
    private static readonly PeerRecord Carol = new() { Name = "Carol C", World = "W" };

    /// A carrier on day 5, with Bob nearby as a tagged breeder.
    private static EngineRig Carrier(double roll = 0.1, BodyFlags bob = BodyFlags.CanBreed)
    {
        var r = new EngineRig(new FixedRandom(roll), new Balance().WithoutMoon()) { Playing = false };
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, 5, r.Now);
        r.BobBody = new BodyTag(bob, PregnancyStage.None, null);
        r.Run(2);
        return r;
    }

    [Fact]
    public void APussyCreampieFromCummyRolls()
    {
        var r = Carrier();
        r.Engine.CreampieFromCummy(r.Bob, CreampieAim.Pussy, r.Now);
        Assert.True(PregnancyBook.IsSecret(r.Save.Pregnancy));
        Assert.Equal(r.Bob.Key, r.Save.Pregnancy.FatherKey);
        Assert.Equal(ApiOutcomes.CummySource, r.Save.Pregnancy.ConceivedSource);
    }

    [Fact]
    public void AnAssCreampieDoesnt()
    {
        var r = Carrier();
        r.Engine.CreampieFromCummy(r.Bob, CreampieAim.Ass, r.Now);
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
    }

    [Fact]
    public void APartnerWithoutCorruptionCounts()
    {
        var r = Carrier();
        r.Engine.CreampieFromCummy(Carol, CreampieAim.Pussy, r.Now);
        Assert.Equal(Carol.Key, r.Save.Pregnancy.FatherKey);
    }

    [Fact]
    public void ATaggedNonBreederDoesnt()
    {
        var r = Carrier(bob: BodyFlags.None);
        r.Engine.CreampieFromCummy(r.Bob, CreampieAim.Pussy, r.Now);
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
    }

    [Fact]
    public void OncePerMinutePerPartner()
    {
        var roll = new SwitchRandom { Value = 0.99 };
        var r = new EngineRig(roll, new Balance().WithoutMoon()) { Playing = false };
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, 5, r.Now);
        r.BobBody = new BodyTag(BodyFlags.CanBreed, PregnancyStage.None, null);
        r.Run(2);
        r.Engine.CreampieFromCummy(r.Bob, CreampieAim.Pussy, r.Now);
        roll.Value = 0;
        r.Engine.CreampieFromCummy(r.Bob, CreampieAim.Pussy, r.Now.AddSeconds(30));
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
        r.Engine.CreampieFromCummy(r.Bob, CreampieAim.Pussy, r.Now.AddSeconds(61));
        Assert.True(PregnancyBook.IsPregnant(r.Save.Pregnancy));
    }

    [Fact]
    public void CorruptionsOwnCreampieIsntRolledTwice()
    {
        var roll = new SwitchRandom { Value = 0.99 };
        var r = new EngineRig(roll);
        r.Save.Level = 50;
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, 5, r.Now);
        r.BobBody = new BodyTag(BodyFlags.CanBreed, PregnancyStage.None, null);
        r.Host.Aim = ["gr", "st", "ll", "lr", "bt"];
        r.Creampies = [];
        r.Host.InsideHole = CreampieAim.Pussy;
        r.Run(11);
        r.BobClimaxes();
        Assert.Equal([(r.Bob.Key, CreampieAim.Pussy)], r.Host.Creampies);
        roll.Value = 0;
        // Cummy reports the creampie Corruption just sent: that one already rolled.
        r.Engine.CreampieFromCummy(r.Bob, CreampieAim.Pussy, r.Now);
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
    }

    [Fact]
    public void NoRollWithPregnancyOffOrPaused()
    {
        var r = Carrier();
        r.Save.Paused = true;
        r.Engine.CreampieFromCummy(r.Bob, CreampieAim.Pussy, r.Now);
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
        var off = Carrier();
        PregnancyBook.Disable(off.Save.Pregnancy);
        off.Engine.CreampieFromCummy(off.Bob, CreampieAim.Pussy, off.Now);
        Assert.False(PregnancyBook.IsPregnant(off.Save.Pregnancy));
    }
}

/// A roll that can change partway through a test.
public sealed class SwitchRandom : Random
{
    public double Value { get; set; }
    public override double NextDouble() => Value;
}
