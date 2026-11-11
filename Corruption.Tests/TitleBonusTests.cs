using Corruption.Core;

namespace Corruption.Tests;

/// Each title's bonus changes the one value it names, and nothing else.
public class TitleBonusTests
{
    private static readonly Balance B = new();

    private static Modifiers Of(string id) => TitleCatalog.Find(id)!.Bonus;

    /// The shipped balance, every achievement already earned and the partner and act already seen, so a tick is plain.
    private static EngineRig Wearing(string? title, Random? random = null, bool seen = true)
    {
        var r = new EngineRig(random, new Balance().WithoutMoon());
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        if (seen)
        {
            r.Save.SeenActs.Add(r.Act.Key);
            r.Save.SeenPartners.Add(r.Bob.Key);
        }
        if (title is not null)
        {
            r.Save.Titles.Unlocked.Add(title);
            r.Save.Titles.Equipped = title;
        }
        return r;
    }

    private static void Bond(EngineRig r, double seconds)
    {
        r.Save.BondWith = r.Bob.Key;
        r.Save.BondFormed = true;
        r.Save.BondSeconds = seconds;
        r.BobExtra = new ExtraTag(0, BondHashes.Of(r.SelfKey!));
    }

    // ── Tick factors ────────────────────────────────────────────────────────

    [Fact]
    public void FallenStarMultipliesTickXp()
    {
        var r = Wearing("fallen_star");
        r.Run(11);
        Assert.Equal(6 * 1.05, r.Engine.Status.LastTickXp, 6);
        Assert.Equal(1.05, r.Engine.Status.Factors.Title, 6);
    }

    [Fact]
    public void ShamelessRaisesTheWatcherCap()
    {
        Assert.Equal(1.5, Progression.Factors(1, null, 7, 1, 1, B).Watchers, 6);
        Assert.Equal(1.6, Progression.Factors(1, null, 7, 1, 1, B, title: Of("shameless")).Watchers, 6);
        Assert.Equal(1.8, Progression.Factors(1, null, 9, 1, 1, B, title: Of("shameless_3")).Watchers, 6);
    }

    [Fact]
    public void HabitRaisesTheStreakCap()
    {
        Assert.Equal(1.3, Progression.Factors(1, null, 0, 10, 1, B).Streak, 6);
        Assert.Equal(1.35, Progression.Factors(1, null, 0, 10, 1, B, title: Of("habit")).Streak, 6);
    }

    [Fact]
    public void PaintedAndCumDumpRaiseTheMarkedCap()
    {
        Assert.Equal(1.3, Progression.Factors(1, null, 0, 1, 1, B, layersWorn: 20).Marked, 6);
        Assert.Equal(1.35, Progression.Factors(1, null, 0, 1, 1, B, layersWorn: 20, title: Of("painted")).Marked, 6);
        Assert.Equal(1.4, Progression.Factors(1, null, 0, 1, 1, B, layersWorn: 20, title: Of("cum_dump")).Marked, 6);
    }

    [Fact]
    public void MoonTouchedOnlyPaysDuringAFullMoon()
    {
        Assert.Equal(1, Progression.Factors(1, null, 0, 1, 1, B, title: Of("moon_touched")).Title, 6);
        Assert.Equal(1.08, Progression.Factors(1, null, 0, 1, 1, B, title: Of("moon_touched"), fullMoon: true).Title, 6);
    }

    [Fact]
    public void ACapTitleChangesNothingElse()
    {
        var plain = Progression.Factors(30, 50, 7, 10, 0.8, B, 1, 5, 3);
        var shameless = Progression.Factors(30, 50, 7, 10, 0.8, B, 1, 5, 3, Of("shameless"));
        Assert.Equal(plain with { Watchers = shameless.Watchers }, shameless);
    }

    [Theory]
    [InlineData("shop_tireless", 13, 1.0)]
    [InlineData("marathon", 14, 1.0)]
    [InlineData("marathon", 16, 0.9)]
    [InlineData(null, 14, 0.6)]
    public void FatigueTitlesDelayFatigue(string? id, int minutes, double fatigue)
        => Assert.Equal(fatigue, Progression.Fatigue(TimeSpan.FromMinutes(minutes), B, id is null ? 0 : Of(id).FatigueGraceMinutes), 6);

    [Fact]
    public void MarathonKeepsAFourteenMinuteSceneFresh()
    {
        var r = Wearing("marathon");
        r.Run(14 * 60 + 5);
        Assert.Equal(1.0, r.Engine.Status.Fatigue, 6);
    }

    [Fact]
    public void CrowdPleaserCountsEachWatcherMore()
    {
        var r = Wearing("crowd_pleaser");
        r.Watchers = 2;
        r.Run(11);
        Assert.Equal(1.25, r.Engine.Status.Factors.Watchers, 6);
    }

    // ── Arousal, refractory and climax ──────────────────────────────────────

    [Fact]
    public void WorkedUpBuildsArousalFaster()
    {
        var r = Wearing("worked_up");
        r.Run(11);
        Assert.Equal(1.5 * 1.05, r.Save.Arousal, 6);
    }

    [Fact]
    public void EdgedSlowsArousalFromEighty()
    {
        var r = Wearing("edged");
        r.Save.Arousal = 85;
        r.Run(11);
        Assert.Equal(85 + 1.5 * 0.9, r.Save.Arousal, 6);

        var low = Wearing("edged");
        low.Save.Arousal = 50;
        low.Run(11);
        Assert.Equal(51.5, low.Save.Arousal, 6);
    }

    private static TimeSpan RefractoryAfterClimax(EngineRig r)
    {
        r.Run(1);
        r.Save.Arousal = 99.9;
        r.Run(10);
        Assert.Contains("You climaxed.", r.Host.Notes);
        return r.Save.RefractoryUntilUtc - (r.Now.AddSeconds(-1));
    }

    [Fact]
    public void InsatiableWithAfterglowWine()
    {
        var r = Wearing("insatiable");
        r.Save.Effects[ItemIds.AfterglowWine] = new ActiveEffect { UntilUtc = r.Now.AddHours(1) };
        Assert.Equal(22.5, RefractoryAfterClimax(r).TotalSeconds, 6);
    }

    [Fact]
    public void RefractoryNeverGoesBelowFifteenSeconds()
    {
        var r = Wearing("insatiable_3");
        r.Save.Effects[ItemIds.AfterglowWine] = new ActiveEffect { UntilUtc = r.Now.AddHours(1) };
        Bond(r, BondLevels.Thresholds[BondLevels.DevotedLevel - 1]);
        Assert.Equal(15, RefractoryAfterClimax(r).TotalSeconds, 6);
    }

    private static EngineRig ByHand(string? title, double arousal, bool hairTrigger)
    {
        var r = Wearing(title);
        r.Save.Setup = new ProfileSetup { Decay = DecayMode.Off, AutoClimax = false };
        if (hairTrigger) r.Save.Effects[ItemIds.HairTrigger] = new ActiveEffect { UsesLeft = 1 };
        r.Run(11);
        r.Save.Arousal = arousal;
        return r;
    }

    [Fact]
    public void TeaseClimaxesFromSixtyWithoutSpendingAHairTrigger()
    {
        var r = ByHand("shop_tease", 65, hairTrigger: true);
        Assert.Null(r.Engine.TryManualClimax(r.Now));
        Assert.True(ItemBook.IsActive(r.Save, ItemIds.HairTrigger, r.Now));
    }

    [Fact]
    public void TeaseStillSpendsAHairTriggerBelowSixty()
    {
        var r = ByHand("shop_tease", 45, hairTrigger: true);
        Assert.Null(r.Engine.TryManualClimax(r.Now));
        Assert.False(ItemBook.IsActive(r.Save, ItemIds.HairTrigger, r.Now));
    }

    [Fact]
    public void WithoutTeaseSixtyFiveIsTooLow()
    {
        var r = ByHand(null, 65, hairTrigger: false);
        Assert.NotNull(r.Engine.TryManualClimax(r.Now));
    }

    // ── Event XP and social bonuses ─────────────────────────────────────────

    [Fact]
    public void WellTravelledDoublesNewPartnerXp()
    {
        var r = Wearing("well_travelled", seen: false);
        r.Save.SeenActs.Add(r.Act.Key);
        r.Run(11);
        Assert.Equal(6 + 100 * 2, r.Engine.Status.LastTickXp, 6);
    }

    [Fact]
    public void CollectorDoublesNewAnimationXp()
    {
        var r = Wearing("collector", seen: false);
        r.Save.SeenPartners.Add(r.Bob.Key);
        r.Run(11);
        Assert.Equal(6 + 50, r.Engine.Status.LastTickXp, 6);
    }

    private static double TotalXp(EngineRig r)
    {
        var total = r.Save.Xp;
        for (var level = 1; level < r.Save.Level; level++) total += Progression.XpToNext(level, r.Balance);
        return total;
    }

    [Fact]
    public void GenerousTakesMoreOfAPartnersClimax()
    {
        var r = Wearing("generous");
        r.Engine.LayerOnPartnerClimax = false;
        r.Run(11);
        var before = TotalXp(r);
        r.BobClimaxes();
        Assert.Equal(45, TotalXp(r) - before, 6);
    }

    [Fact]
    public void DevotedGrowsTheBondFaster()
    {
        var r = Wearing("devoted");
        Bond(r, 0);
        r.Run(11);
        Assert.Equal(12, r.Save.BondSeconds, 6);
    }

    private static EngineRig ClimaxTogether(string title, int apartSeconds)
    {
        var r = Wearing(title);
        Bond(r, BondLevels.Thresholds[BondLevels.TogetherLevel - 1]);
        r.Run(1);
        r.Save.Arousal = 99.9;
        r.Run(10);
        r.Run(apartSeconds);
        r.BobClimaxes();
        return r;
    }

    [Fact]
    public void SoulmatesDoublesTheTogetherPay()
    {
        var r = ClimaxTogether("soulmates", 5);
        Assert.Contains(r.Host.Notes, n => n.Contains("climaxed together. +60 Sins."));
    }

    [Fact]
    public void InSyncWidensTheTogetherWindow()
    {
        Assert.Contains(ClimaxTogether("in_sync", 45).Host.Notes, n => n.Contains("climaxed together"));
        Assert.DoesNotContain(ClimaxTogether("first_taste", 45).Host.Notes, n => n.Contains("climaxed together"));
    }

    // ── Sins and the shop ───────────────────────────────────────────────────

    [Fact]
    public void DevoutAddsToTheCheckIn()
    {
        var r = Wearing("devout");
        r.Run(1);
        Assert.Contains("Daily check-in: +30 Sins.", r.Host.Notes);
        Assert.Equal(30, r.Save.Wallet.Sins);
    }

    [Fact]
    public void HarlotPaysMoreForSceneTime()
    {
        var plain = Wearing(null);
        var harlot = Wearing("shop_harlot");
        plain.Run(601);
        harlot.Run(601);
        // Ten minutes pay 10 Sins plainly and 12 with Harlot. Levels pay the same Sins to both.
        Assert.Equal(2, harlot.Save.Wallet.Sins - plain.Save.Wallet.Sins);
    }

    [Fact]
    public void BigSpenderTakesFivePercentOffItems()
    {
        var save = new CharacterSave { Setup = new ProfileSetup() };
        var incense = ItemCatalog.Find(ItemIds.LustIncense)!;
        Assert.Equal(80, ItemBook.PriceFor(save, incense));
        save.Titles.Unlocked.Add("big_spender");
        save.Titles.Equipped = "big_spender";
        Assert.Equal(76, ItemBook.PriceFor(save, incense));
        Assert.Equal(38, ItemBook.PriceFor(save, ItemCatalog.Find(ItemIds.Aphrodisiac)!));

        save.Wallet.Sins = 76;
        Assert.Null(ItemBook.Buy(save, ItemIds.LustIncense));
        Assert.Equal(0, save.Wallet.Sins);
    }

    private static EngineRig Pregnant(string? title)
    {
        var r = Wearing(title);
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, 1, r.Now);
        r.Save.Pregnancy.ConceivedUtc = r.Now.AddDays(-1);
        r.Save.Pregnancy.DueUtc = r.Now.AddDays(13);
        r.Save.Pregnancy.CravingDay = r.Now.Date.ToString("yyyy-MM-dd");
        r.Save.Pregnancy.Craving = CravingKind.Watched;
        return r;
    }

    [Fact]
    public void GluttonDoublesCravingSins()
    {
        var r = Pregnant("glutton");
        r.Save.Inventory[ItemIds.CravingTreat] = 1;
        r.Run(1);
        Assert.Null(r.Engine.UseItem(ItemIds.CravingTreat, r.Now));
        Assert.Contains("Your craving is satisfied. +20 Sins.", r.Host.Notes);
    }

    [Fact]
    public void LabourOfLovePaysMoreForABirth()
    {
        var r = Pregnant("labour_of_love");
        r.Save.Pregnancy.DueUtc = r.Now.AddSeconds(-1);
        r.Run(1);
        Assert.Contains(r.Host.Notes, n => n.Contains("You gave birth") && n.Contains("+225 Sins."));
    }

    // ── Conception ──────────────────────────────────────────────────────────

    private static EngineRig Breeding(string? ownTitle, string? partnerTitle, double roll)
    {
        var r = Wearing(ownTitle, new FixedRandom(roll));
        r.Save.Level = 50;
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, 4, r.Now);
        r.BobBody = new BodyTag(BodyFlags.CanBreed, PregnancyStage.None, null);
        r.BobTitle = partnerTitle;
        r.Host.Aim = ["gr", "st", "ll", "lr", "bt"];
        // Cummy version 6, and their tip inside the pussy: only a pussy creampie can conceive.
        r.Creampies = [];
        r.Host.InsideHole = CreampieAim.Pussy;
        r.Run(11);
        r.BobClimaxes();
        return r;
    }

    [Fact]
    public void BroodMotherAndSireMultiplyTheOdds()
    {
        var save = new CharacterSave { Setup = new ProfileSetup() };
        PregnancyBook.Enable(save.Pregnancy, true, false, 4, EngineRig.T0);
        var chance = PregnancyBook.ConceptionChance(save, EngineRig.T0.AddHours(1), false, 0, false,
            Of("brood_mother").ConceptionOwn * Of("sire").ConceptionPartner);
        Assert.Equal(0.25 * 1.25 * 1.15, chance, 6);
    }

    [Fact]
    public void APartnersSireTitleIsReadFromTheirTag()
    {
        // 0.25 × 1.25 = 0.3125 alone, 0.359 with Sire: a roll of 0.33 only conceives with it.
        Assert.True(PregnancyBook.IsPregnant(Breeding("brood_mother", "sire", 0.33).Save.Pregnancy));
        Assert.False(PregnancyBook.IsPregnant(Breeding("brood_mother", null, 0.33).Save.Pregnancy));
    }

    [Fact]
    public void AnUnknownPartnerTitleGivesNoFactor()
        => Assert.False(PregnancyBook.IsPregnant(Breeding("brood_mother", "from_the_future", 0.33).Save.Pregnancy));
}
