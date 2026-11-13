using Corruption.Core;

namespace Corruption.Tests;

/// Always rolls low, so any chance above 0 conceives; or high, so none does.
public sealed class FixedRandom(double value) : Random
{
    public override double NextDouble() => value;
}

public class CycleTests
{
    private static readonly DateTime T0 = EngineRig.T0;

    [Theory]
    [InlineData(0, 1, CyclePhase.Period)]
    [InlineData(1.5, 2, CyclePhase.Period)]
    [InlineData(2, 3, CyclePhase.Follicular)]
    [InlineData(3, 4, CyclePhase.Fertile)]
    [InlineData(4, 5, CyclePhase.Ovulation)]
    [InlineData(5, 6, CyclePhase.Luteal)]
    [InlineData(6.9, 7, CyclePhase.Luteal)]
    [InlineData(7, 1, CyclePhase.Period)]
    public void Phases(double days, int day, CyclePhase phase)
    {
        Assert.Equal(day, CycleClock.DayAt(T0, T0.AddDays(days)));
        Assert.Equal(phase, CycleClock.PhaseOf(day).Phase);
    }

    [Theory]
    [InlineData(1, 0.8, 0)]
    [InlineData(3, 1.0, 0.05)]
    [InlineData(4, 1.2, 0.25)]
    [InlineData(5, 1.3, 0.40)]
    [InlineData(7, 1.0, 0.05)]
    public void PhaseEffects(int day, double gain, double chance)
        => Assert.Equal((gain, chance), (CycleClock.PhaseOf(day).ArousalFactor, CycleClock.PhaseOf(day).ConceptionChance));

    [Fact]
    public void AWeekOfflineComesBackToTheSameDay()
        => Assert.Equal(CycleClock.DayAt(T0, T0.AddDays(1.2)), CycleClock.DayAt(T0, T0.AddDays(8.2)));

    [Theory]
    [InlineData(0.5, 2)]
    [InlineData(1.5, 2)]
    [InlineData(2.5, 3)]
    [InlineData(5.5, 7)]
    [InlineData(6.5, 7)]
    public void NextPhase(double days, double nextAtDays)
        => Assert.Equal(T0.AddDays(nextAtDays), CycleClock.NextPhaseAt(T0, T0.AddDays(days)));

    [Fact]
    public void OptingInStartsOnTheChosenDay()
    {
        var p = new PregnancyState();
        Assert.Null(PregnancyBook.Enable(p, true, false, 3, T0.AddHours(5)));
        Assert.Equal(3, CycleClock.DayAt(p.CycleStartUtc, T0.AddHours(5)));
        Assert.Equal(4, CycleClock.DayAt(p.CycleStartUtc, T0.AddDays(1).AddHours(1)));
    }

    [Fact]
    public void ARoleIsNeeded() => Assert.NotNull(PregnancyBook.Enable(new PregnancyState(), false, false, 3, T0));
}

public class PregnancyTests
{
    private static readonly DateTime T0 = EngineRig.T0;

    private static CharacterSave Carrier(int day)
    {
        var save = new CharacterSave { Setup = new ProfileSetup() };
        PregnancyBook.Enable(save.Pregnancy, true, false, day, T0);
        return save;
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(4, 0.25)]
    [InlineData(5, 0.40)]
    public void ChanceFollowsThePhase(int day, double chance)
        => Assert.Equal(chance, PregnancyBook.ConceptionChance(Carrier(day), T0.AddHours(1), false, 0, false), 6);

    [Fact]
    public void EveryFactorMultipliesUpToTheCap()
    {
        var save = Carrier(4);
        save.Effects[ItemIds.FertilityCharm] = new ActiveEffect { UntilUtc = T0.AddDays(1) };
        Assert.Equal(0.5, PregnancyBook.ConceptionChance(save, T0.AddHours(1), false, 0, false), 6);
        Assert.Equal(0.75, PregnancyBook.ConceptionChance(save, T0.AddHours(1), true, 0, false), 6);
        Assert.Equal(0.9, PregnancyBook.ConceptionChance(save, T0.AddHours(1), true, 4, false), 6);
        Assert.Equal(0.375, PregnancyBook.ConceptionChance(Carrier(4), T0.AddHours(1), false, 4, false), 6);
    }

    [Fact]
    public void NoChanceWhenBlocked()
    {
        var save = Carrier(5);
        Assert.Equal(0, PregnancyBook.ConceptionChance(save, T0.AddHours(1), false, 0, blocked: true));
        save.Effects[ItemIds.Contraceptive] = new ActiveEffect { UntilUtc = T0.AddDays(1) };
        Assert.Equal(0, PregnancyBook.ConceptionChance(save, T0.AddHours(1), false, 0, false));
        save.Effects.Clear();
        KnownPregnancy.Conceive(save.Pregnancy, T0, "Bob B@W");
        Assert.Equal(0, PregnancyBook.ConceptionChance(save, T0.AddHours(1), false, 0, false));
        save.Pregnancy.RecoveryUntilUtc = T0.AddDays(30);
        save.Pregnancy.ConceivedUtc = null;
        save.Pregnancy.DueUtc = null;
        Assert.Equal(0, PregnancyBook.ConceptionChance(save, T0.AddDays(1), false, 0, false));
    }

    [Theory]
    [InlineData(1, PregnancyStage.First, 0.9)]
    [InlineData(7, PregnancyStage.Second, 1.25)]
    [InlineData(12, PregnancyStage.Third, 1.1)]
    public void Trimesters(double days, PregnancyStage stage, double gain)
    {
        var p = Carrier(4).Pregnancy;
        KnownPregnancy.Conceive(p, T0, "Bob B@W");
        Assert.Equal(stage, PregnancyBook.StageOf(p, T0.AddDays(days)));
        Assert.Equal(gain, PregnancyBook.ArousalFactor(p, T0.AddDays(days)), 6);
        Assert.Equal(1.1, PregnancyBook.TickXpFactor(p), 6);
    }

    [Fact]
    public void BirthWhileOfflineThenRecoveryThenCycle()
    {
        var p = Carrier(4).Pregnancy;
        KnownPregnancy.Conceive(p, T0, "Bob B@W");
        Assert.Equal(PregnancyEvent.None, PregnancyBook.Update(p, T0.AddDays(13.9)));
        Assert.Equal(PregnancyEvent.Birth, PregnancyBook.Update(p, T0.AddDays(20)));
        Assert.Equal(1, p.Births);
        Assert.Equal(PregnancyStage.Recovering, PregnancyBook.StageOf(p, T0.AddDays(21)));
        Assert.Equal(0.8, PregnancyBook.ArousalFactor(p, T0.AddDays(21)), 6);
        Assert.Equal(PregnancyEvent.RecoveryEnded, PregnancyBook.Update(p, T0.AddDays(23)));
        Assert.Equal(1, CycleClock.DayAt(p.CycleStartUtc, T0.AddDays(22.5)));
    }

    [Fact]
    public void SwiftLabourMovesTheDueDate()
    {
        var p = Carrier(4).Pregnancy;
        Assert.NotNull(PregnancyBook.SwiftLabour(p));
        KnownPregnancy.Conceive(p, T0, "Bob B@W");
        Assert.Null(PregnancyBook.SwiftLabour(p));
        Assert.Equal(T0.AddDays(12), p.DueUtc);
        Assert.Equal(PregnancyStage.Third, PregnancyBook.StageOf(p, T0.AddDays(8.5)));
    }

    [Fact]
    public void MorningAfterOnlyWithin24Hours()
    {
        var p = Carrier(4).Pregnancy;
        KnownPregnancy.Conceive(p, T0, "Bob B@W");
        Assert.NotNull(PregnancyBook.MorningAfter(p, T0.AddHours(25)));
        Assert.Null(PregnancyBook.MorningAfter(p, T0.AddHours(10)));
        Assert.False(PregnancyBook.IsPregnant(p));
        Assert.Equal(1, CycleClock.DayAt(p.CycleStartUtc, T0.AddHours(11)));
        Assert.Equal(0, p.Births);
    }

    [Fact]
    public void HormoneTeaJumpsToFertile()
    {
        var p = Carrier(1).Pregnancy;
        Assert.Null(PregnancyBook.HormoneTea(p, T0.AddHours(3)));
        Assert.Equal(CyclePhase.Fertile, CycleClock.PhaseOf(CycleClock.DayAt(p.CycleStartUtc, T0.AddHours(3))).Phase);
    }

    [Fact]
    public void TurningOffEndsEverything()
    {
        var p = Carrier(4).Pregnancy;
        KnownPregnancy.Conceive(p, T0, "Bob B@W");
        PregnancyBook.Disable(p);
        Assert.False(PregnancyBook.IsPregnant(p));
        Assert.Equal(1, PregnancyBook.ArousalFactor(p, T0.AddDays(1)), 6);
        Assert.Equal(PregnancyStage.None, PregnancyBook.StageOf(p, T0.AddDays(1)));
    }

    [Fact]
    public void RolesCantChangeWhilePregnant()
    {
        var p = Carrier(4).Pregnancy;
        KnownPregnancy.Conceive(p, T0, "Bob B@W");
        Assert.NotNull(PregnancyBook.Enable(p, true, true, 3, T0));
    }

    // ── In the engine ──────────────────────────────────────────────────────────

    private static EngineRig Pair(double roll, int day = 5)
    {
        var r = new EngineRig(new FixedRandom(roll));
        r.Save.Level = 50;
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, day, r.Now);
        r.BobBody = new BodyTag(BodyFlags.CanBreed, PregnancyStage.None, null);
        // Their genitals are nearest the groin, and with Cummy version 6 their tip is inside the pussy.
        r.Host.Aim = ["gr", "st", "ll", "lr", "bt"];
        r.Creampies = [];
        r.Host.InsideHole = CreampieAim.Pussy;
        r.Run(11);
        return r;
    }

    [Fact]
    public void BreedersCreampieCanConceive()
    {
        var r = Pair(0.1);
        r.BobClimaxes();
        Assert.True(PregnancyBook.IsSecret(r.Save.Pregnancy));
        Assert.Equal(r.Bob.Key, r.Save.Pregnancy.FatherKey);
        Assert.Equal(ApiOutcomes.SceneSource, r.Save.Pregnancy.ConceivedSource);
        Assert.DoesNotContain(r.Host.Notes, n => n.Contains("conceived") || n.Contains("pregnant"));
        Assert.Equal([(r.Bob.Key, CreampieAim.Pussy)], r.Host.Creampies);

        // Secret: the tag says not pregnant, so the father isn't told, and nothing is craved.
        r.Run(2);
        var tag = BodyTag.Parse(r.Host.BodyTags[^1])!.Value;
        Assert.Equal(PregnancyStage.None, tag.Stage);
        Assert.Null(tag.FatherHash);
        Assert.Null(r.Save.Pregnancy.Craving);
        Assert.Equal(0, r.Save.Pregnancy.Conceptions);

        // A positive test turns it all on.
        string? source = null;
        r.Engine.Conceived += (name, _, s) => source = $"{name}/{s}";
        r.Save.Inventory[ItemIds.PregnancyTest] = 1;
        Assert.Null(r.Engine.UseItem(ItemIds.PregnancyTest, r.Now));
        Assert.Contains(r.Host.Notes, n => n == $"Positive. You're pregnant with {r.Bob.Name}'s child.");
        Assert.Equal($"{r.Bob.Name}/scene", source);
        Assert.Equal(1, r.Save.Pregnancy.Conceptions);
        r.Run(2);
        tag = BodyTag.Parse(r.Host.BodyTags[^1])!.Value;
        Assert.Equal(PregnancyStage.First, tag.Stage);
        Assert.Equal(BondHashes.Of(r.Bob.Key), tag.FatherHash);
    }

    [Fact]
    public void AShotElsewhereCantConceive()
    {
        var r = Pair(0);
        r.Host.InsideHole = null;
        r.Host.Aim = ["mo", "ch"];
        r.BobClimaxes();
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
    }

    [Fact]
    public void AnOutsideShotOnTheGroinCantConceive()
    {
        var r = Pair(0);
        r.Host.InsideHole = null;
        r.BobClimaxes();
        Assert.Equal("gr", r.Host.Layers[0].Part);
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
    }

    [Fact]
    public void AnAssCreampieCantConceive()
    {
        var r = Pair(0);
        r.Host.InsideHole = CreampieAim.Ass;
        r.BobClimaxes();
        Assert.Equal([(r.Bob.Key, CreampieAim.Ass)], r.Host.Creampies);
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
    }

    [Fact]
    public void ABlockedPussyLandsOutsideAndCantConceive()
    {
        var r = Pair(0);
        r.Host.CreampieOutcome = "blocked";
        r.BobClimaxes();
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
        Assert.Equal("gr", r.Host.Layers[0].Part);
    }

    [Fact]
    public void ARefreshedCreampieRollsAgain()
    {
        // Day 5 is 40%: a roll of 0.3 conceives, so a refresh still rolls.
        var r = Pair(0.3);
        r.Host.CreampieOutcome = "refreshed";
        r.BobClimaxes();
        Assert.True(PregnancyBook.IsPregnant(r.Save.Pregnancy));
    }

    [Fact]
    public void OnOvulationDayTheChanceIsFortyPercent()
    {
        var low = Pair(0.39);
        low.BobClimaxes();
        Assert.True(PregnancyBook.IsPregnant(low.Save.Pregnancy));
        var high = Pair(0.41);
        high.BobClimaxes();
        Assert.False(PregnancyBook.IsPregnant(high.Save.Pregnancy));
    }

    [Fact]
    public void NoLayeringMeansNoConception()
    {
        var r = Pair(0);
        r.Engine.LayerOnPartnerClimax = false;
        r.BobClimaxes();
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
        Assert.Empty(r.Host.Creampies);
    }

    [Fact]
    public void BelowCummyVersionSixNoConception()
    {
        var r = Pair(0);
        r.Creampies = null;
        r.BobClimaxes();
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
        Assert.Empty(r.Host.Creampies);
        Assert.Equal("gr", r.Host.Layers[0].Part);
    }

    [Fact]
    public void WithoutCummyNoConception()
    {
        var r = Pair(0);
        r.Host.CreampieOutcome = null;
        r.BobClimaxes();
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
    }

    [Theory]
    [InlineData(1, 0.40)]
    [InlineData(0.5, 0.20)]
    [InlineData(2, 0.80)]
    [InlineData(5, 1.0)]
    [InlineData(0.01, 0.04)]
    public void ChanceSlider(double factor, double chance)
    {
        var save = new CharacterSave { Setup = new ProfileSetup() };
        PregnancyBook.Enable(save.Pregnancy, true, false, 5, EngineRig.T0);
        save.Pregnancy.ChanceFactor = factor;
        Assert.Equal(chance, PregnancyBook.ConceptionChance(save, EngineRig.T0.AddHours(1), false, 0, false), 6);
    }

    [Fact]
    public void AHighRollDoesntConceive()
    {
        var r = Pair(0.5);
        r.BobClimaxes();
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
    }

    [Fact]
    public void NotABreederNoRoll()
    {
        var r = Pair(0);
        r.BobBody = BodyTag.Empty;
        r.Run(1);
        r.BobClimaxes();
        Assert.False(PregnancyBook.IsPregnant(r.Save.Pregnancy));
    }

    [Fact]
    public void EnginePaysTheBirth()
    {
        var r = Pair(0.1);
        r.BobClimaxes();
        var sins = r.Save.Wallet.Sins;
        r.Now = r.Now.AddDays(15);
        r.Run(1);
        Assert.Contains(r.Host.Notes, n => n.Contains("gave birth"));
        // A new day, so the check-in pays too.
        Assert.Equal(sins + Sins.Birth + Sins.CheckInBase, r.Save.Wallet.Sins);
    }

    [Fact]
    public void PregnantTicksPayMore()
    {
        var r = Pair(0.1);
        r.BobClimaxes();
        r.Run(11);
        Assert.Equal(1.1, r.Engine.Status.Factors.Pregnancy, 6);
    }

    [Fact]
    public void ResetEndsPregnancy()
    {
        var save = Carrier(4);
        KnownPregnancy.Conceive(save.Pregnancy, T0, "Bob B@W");
        save.ResetProgress(T0);
        Assert.False(save.Pregnancy.Enabled);
        Assert.False(PregnancyBook.IsPregnant(save.Pregnancy));
    }
}

public class CravingTests
{
    private static readonly DateTime T0 = EngineRig.T0;

    private static PregnancyState Pregnant()
    {
        var p = new PregnancyState();
        PregnancyBook.Enable(p, true, false, 4, T0);
        KnownPregnancy.Conceive(p, T0, "Bob B@W");
        return p;
    }

    [Fact]
    public void OneCravingPerDay()
    {
        var p = Pregnant();
        Assert.NotNull(CravingBook.Roll(p, T0, 1, true));
        Assert.Null(CravingBook.Roll(p, T0.AddHours(5), 1, true));
        Assert.NotNull(CravingBook.Roll(p, T0.AddDays(1), 1, true));
    }

    [Fact]
    public void NoBondedCravingWithoutABond()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var p = Pregnant();
            Assert.NotEqual(CravingKind.BondedScene, CravingBook.Roll(p, T0, seed, false));
        }
    }

    [Fact]
    public void MetOnceAndOnlyByItsKind()
    {
        var p = Pregnant();
        p.Craving = CravingKind.Climax;
        p.CravingDay = "x";
        Assert.False(CravingBook.Met(p, CravingKind.Layer));
        Assert.True(CravingBook.Met(p, CravingKind.Climax));
        Assert.False(CravingBook.Met(p, CravingKind.Climax));
    }

    [Fact]
    public void UnmetLeavesYouUnsatisfiedUntilTheNextIsMet()
    {
        var p = Pregnant();
        CravingBook.Roll(p, T0, 1, true);
        CravingBook.Roll(p, T0.AddDays(1), 1, true);
        Assert.True(p.Unsatisfied);
        Assert.Equal(0.9 * 0.85, PregnancyBook.ArousalFactor(p, T0.AddDays(1)), 6);
        Assert.True(CravingBook.Treat(p));
        Assert.False(p.Unsatisfied);
    }

    private static EngineRig PregnantScene(CravingKind craving)
    {
        var r = new EngineRig();
        r.Engine.LayerOnPartnerClimax = true;
        r.Save.Level = 50;
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, 4, r.Now);
        KnownPregnancy.Conceive(r.Save.Pregnancy, r.Now, "Bob B@W");
        r.Save.Pregnancy.CravingDay = r.Now.Date.ToString("yyyy-MM-dd");
        r.Save.Pregnancy.Craving = craving;
        return r;
    }

    [Theory]
    [InlineData(CravingKind.Watched)]
    [InlineData(CravingKind.NewAnimation)]
    [InlineData(CravingKind.Layer)]
    [InlineData(CravingKind.Climax)]
    [InlineData(CravingKind.TenMinutes)]
    [InlineData(CravingKind.BondedScene)]
    public void EachKindCanBeMet(CravingKind craving)
    {
        var r = PregnantScene(craving);
        r.Watchers = craving == CravingKind.Watched ? 1 : 0;
        if (craving == CravingKind.BondedScene)
        {
            r.Save.BondWith = r.Bob.Key;
            r.Save.BondFormed = true;
            r.BobExtra = new ExtraTag(0, BondHashes.Of(r.SelfKey!));
        }
        if (craving == CravingKind.Climax) r.Save.Arousal = 99;
        r.Run(craving == CravingKind.TenMinutes ? 611 : 11);
        if (craving == CravingKind.Layer) r.BobClimaxes();
        Assert.True(r.Save.Pregnancy.CravingMet, craving.ToString());
        Assert.Contains(r.Host.Notes, n => n.Contains("craving is satisfied"));
    }

    [Fact]
    public void CravingTreatFromTheShop()
    {
        var r = PregnantScene(CravingKind.Watched);
        r.Save.Wallet.Sins = 100;
        Assert.Null(r.Engine.BuyItem(ItemIds.CravingTreat));
        Assert.Null(r.Engine.UseItem(ItemIds.CravingTreat, r.Now));
        Assert.True(r.Save.Pregnancy.CravingMet);
        Assert.Equal(100 - 25 + Sins.Craving, r.Save.Wallet.Sins);
    }
}

public class FatherAndAchievementTests
{
    private static BodyTag Carrying(string father) => new(BodyFlags.CanCarry, PregnancyStage.Second, BondHashes.Of(father));

    [Fact]
    public void FatherIsToldOncePerPregnancy()
    {
        var r = new EngineRig { Playing = false, BobBody = Carrying("Alice A@W") };
        r.Run(5);
        Assert.Single(r.Host.Notes, n => n.Contains("carrying your child"));
        Assert.Equal(1, r.Save.Pregnancy.Fathered);

        r.BobBody = BodyTag.Empty;
        r.Run(1);
        r.BobBody = Carrying("Alice A@W");
        r.Run(1);
        Assert.Equal(2, r.Host.Notes.Count(n => n.Contains("carrying your child")));
    }

    [Fact]
    public void SomeoneElsesChildSaysNothing()
    {
        var r = new EngineRig { Playing = false, BobBody = Carrying("Carol C@W") };
        r.Run(5);
        Assert.DoesNotContain(r.Host.Notes, n => n.Contains("carrying your child"));
    }

    [Theory]
    [InlineData("expecting")]
    [InlineData("labour_of_love")]
    [InlineData("brood_mother")]
    [InlineData("sire")]
    [InlineData("soulmates")]
    [InlineData("big_spender")]
    [InlineData("devout")]
    public void NewAchievementsUnlock(string id)
    {
        var save = new CharacterSave { Setup = new ProfileSetup() };
        var book = new GoalBook(save);
        Assert.DoesNotContain(book.TakeRewards(), r => r.Kind == RewardKind.Achievement && GoalCatalog.Achievements.First(a => a.Name == r.Title).Id == id);
        save.Goals.Achievements.Clear();
        switch (id)
        {
            case "expecting": save.Pregnancy.Conceptions = 1; break;
            case "labour_of_love": save.Pregnancy.Births = 1; break;
            case "brood_mother": save.Pregnancy.Births = 3; break;
            case "sire": save.Pregnancy.Fathered = 1; break;
            case "soulmates": save.BondFormed = true; save.BondSeconds = 12 * 3600; break;
            case "big_spender": save.Wallet.SpentTotal = 1000; break;
            default: save.Wallet.CheckInStreak = 14; break;
        }
        Assert.Contains(id, book.TakeRewards().Select(r => GoalCatalog.Achievements.First(a => a.Name == r.Title).Id));
    }
}

public class StoryTests
{
    private static readonly DateTime T0 = EngineRig.T0;

    [Fact]
    public void BirthIsRecordedWithTheFatherOnItsDueDate()
    {
        var p = new PregnancyState();
        PregnancyBook.Enable(p, true, false, 4, T0);
        KnownPregnancy.Conceive(p, T0, "Bob B@W");
        PregnancyBook.Update(p, T0.AddDays(20));
        var birth = Assert.Single(p.BirthLog);
        Assert.Equal(("Bob B@W", T0, T0.AddDays(14)), (birth.FatherKey, birth.ConceivedUtc, birth.BornUtc));
    }

    [Fact]
    public void EngineNamesTheFatherAtBirth()
    {
        var r = new EngineRig { Playing = false };
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, 4, r.Now);
        KnownPregnancy.Conceive(r.Save.Pregnancy, r.Now, "Bob B@W");
        r.Now = r.Now.AddDays(15);
        r.Run(1);
        Assert.Contains(r.Host.Notes, n => n.Contains("gave birth to Bob B's child"));
    }

    [Fact]
    public void SinsEarnedIsCountedEveryWay()
    {
        var wallet = new Wallet();
        Sins.CheckIn(wallet, T0);
        Sins.ForScene(wallet, 120);
        Sins.Earn(wallet, 15);
        Sins.TrySpend(wallet, 30);
        Assert.Equal((7, 37, 30), (wallet.Sins, wallet.EarnedTotal, wallet.SpentTotal));
    }

    [Fact]
    public void LongestSceneIsKeptWhenItEnds()
    {
        var r = new EngineRig();
        r.Run(120);
        r.Playing = false;
        r.Run(40);
        Assert.InRange(r.Save.LongestSceneSeconds, 115, 120);
        r.Playing = true;
        r.Run(30);
        r.Playing = false;
        r.Run(40);
        Assert.InRange(r.Save.LongestSceneSeconds, 115, 120);
    }

    [Fact]
    public void ItemsUsedAreCounted()
    {
        var r = new EngineRig();
        r.Save.Wallet.Sins = 100;
        r.Engine.BuyItem(ItemIds.Aphrodisiac);
        r.Engine.UseItem(ItemIds.Aphrodisiac, r.Now);
        Assert.Equal(1, r.Save.Goals.Stats[nameof(GoalKind.ItemsUsed)]);
    }
}

/// A pregnancy everyone can see: conceived and confirmed at once, as a test or day 5 would.
internal static class KnownPregnancy
{
    public static void Conceive(PregnancyState p, DateTime now, string fatherKey)
    {
        // Confirmed the way a save from before secret pregnancies is, so today's craving still rolls.
        PregnancyBook.Conceive(p, now, fatherKey);
        p.ConfirmedUtc = now;
        p.Conceptions++;
    }
}
