using Corruption.Core;

namespace Corruption.Tests;

/// Each personality and trait in a real engine: its effect, and the spec's scenarios.
public class PersonaEffectTests
{
    /// The shipped balance without the moon, everything already earned and seen, so a tick is plain.
    private static EngineRig As(string personality, params string[] traits)
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon());
        r.Save.Setup = new ProfileSetup { Decay = DecayMode.Off, Personality = personality, Traits = [.. traits] };
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        return r;
    }

    private static double ArousalAfterOneTick(string personality, params string[] traits)
    {
        var r = As(personality, traits);
        r.Run(11);
        return r.Save.Arousal;
    }

    private static double TickXp(EngineRig r)
    {
        r.Run(11);
        return r.Engine.Status.LastTickXp;
    }

    // ── Tick XP ─────────────────────────────────────────────────────────────

    [Fact]
    public void ShyAloneEarnsMore() => Assert.Equal(6 * 1.1, TickXp(As(PersonaIds.Shy)), 6);

    [Fact]
    public void ShyWithAWatcherDoesnt()
    {
        var r = As(PersonaIds.Shy);
        r.Watchers = 2;
        r.Run(11);
        Assert.Equal(1, r.Engine.Status.Factors.Persona, 6);
        Assert.Equal(1.1, r.Engine.Status.Factors.Watchers, 6);
    }

    [Fact]
    public void AttentionSeekerAloneEarnsLess() => Assert.Equal(6 * 0.9, TickXp(As(PersonaIds.Easygoing, PersonaIds.AttentionSeeker)), 6);

    [Fact]
    public void GreedyEarnsSlightlyLess() => Assert.Equal(6 * 0.95, TickXp(As(PersonaIds.Easygoing, PersonaIds.Greedy)), 6);

    [Fact]
    public void ClingyAwayFromTheBondEarnsLess()
    {
        var r = As(PersonaIds.Easygoing, PersonaIds.Clingy);
        r.Save.BondWith = "Carol C@W";
        r.Save.BondFormed = true;
        Assert.Equal(6 * 0.9, TickXp(r), 6);
    }

    [Fact]
    public void RomanticAddsToTheBond()
    {
        var r = As(PersonaIds.Romantic);
        r.Save.BondWith = r.Bob.Key;
        r.Save.BondFormed = true;
        r.BobExtra = new ExtraTag(0, BondHashes.Of(r.SelfKey!));
        r.Run(11);
        Assert.Equal(1.35, r.Engine.Status.Factors.Bonded, 6);
    }

    // ── Watchers and arousal ────────────────────────────────────────────────

    [Theory]
    [InlineData(PersonaIds.Shy, 2, 1.1)]
    [InlineData(PersonaIds.Exhibitionist, 2, 1.3)]
    [InlineData(PersonaIds.Easygoing, 2, 1.2)]
    public void WatchersCountByPersonality(string personality, int watchers, double factor)
    {
        var r = As(personality);
        r.Watchers = watchers;
        r.Run(11);
        Assert.Equal(factor, r.Engine.Status.Factors.Watchers, 6);
    }

    [Fact]
    public void AttentionSeekerCountsWatchersMore()
    {
        var r = As(PersonaIds.Easygoing, PersonaIds.AttentionSeeker);
        r.Watchers = 4;
        r.Run(11);
        Assert.Equal(1.5, r.Engine.Status.Factors.Watchers, 6);
    }

    [Fact]
    public void ExhibitionistAloneBuildsSlower() => Assert.Equal(1.5 * 0.85, ArousalAfterOneTick(PersonaIds.Exhibitionist), 6);

    [Theory]
    [InlineData(PersonaIds.Lewd, 1.15)]
    [InlineData(PersonaIds.Bratty, 0.85)]
    [InlineData(PersonaIds.Needy, 1.1)]
    [InlineData(PersonaIds.Reserved, 0.9)]
    public void PersonalityArousal(string personality, double factor)
        => Assert.Equal(1.5 * factor, ArousalAfterOneTick(personality), 6);

    [Theory]
    [InlineData(PersonaIds.Hypersexual, 1.25)]
    [InlineData(PersonaIds.Sensitive, 1.2)]
    [InlineData(PersonaIds.Enduring, 0.85)]
    [InlineData(PersonaIds.Edger, 0.9)]
    public void TraitArousal(string trait, double factor)
        => Assert.Equal(1.5 * factor, ArousalAfterOneTick(PersonaIds.Easygoing, trait), 6);

    [Fact]
    public void LewdAndSensitiveStack()
        => Assert.Equal(2.07, ArousalAfterOneTick(PersonaIds.Lewd, PersonaIds.Sensitive), 6);

    // ── Climaxes ────────────────────────────────────────────────────────────

    private static double TotalXp(EngineRig r)
    {
        var total = r.Save.Xp;
        for (var level = 1; level < r.Save.Level; level++) total += Progression.XpToNext(level, r.Balance);
        return total;
    }

    /// XP from one automatic climax: the tick that brings it, less a plain tick.
    private static (double Xp, EngineRig Rig) Climax(string personality, params string[] traits)
    {
        var r = As(personality, traits);
        r.Engine.LayerOnPartnerClimax = false;
        r.Run(1);
        r.Save.Arousal = 99.9;
        var before = TotalXp(r);
        r.Run(10);
        Assert.Contains("You climaxed.", r.Host.Notes);
        return (TotalXp(r) - before - r.Engine.Status.LastTickXp, r);
    }

    [Theory]
    [InlineData(PersonaIds.Lewd, 0.85)]
    [InlineData(PersonaIds.Dominant, 0.8)]
    [InlineData(PersonaIds.Submissive, 1.25)]
    [InlineData(PersonaIds.Bratty, 1.3)]
    public void PersonalityClimaxXp(string personality, double factor)
        => Assert.Equal(60 * factor, Climax(personality).Xp, 6);

    [Theory]
    [InlineData(PersonaIds.Sensitive, 0.8)]
    [InlineData(PersonaIds.Messy, 0.9)]
    public void TraitClimaxXp(string trait, double factor)
        => Assert.Equal(60 * factor, Climax(PersonaIds.Easygoing, trait).Xp, 6);

    [Fact]
    public void MultiOrgasmicStaysAtFortyWithALongerRefractory()
    {
        var (_, r) = Climax(PersonaIds.Easygoing, PersonaIds.MultiOrgasmic);
        Assert.Equal(40, r.Save.Arousal, 6);
        Assert.Equal(90, (r.Save.RefractoryUntilUtc - r.Now.AddSeconds(-1)).TotalSeconds, 6);
    }

    [Theory]
    [InlineData(PersonaIds.Hypersexual, 45)]
    [InlineData(PersonaIds.Enduring, 30)]
    public void RefractoryTraits(string trait, double seconds)
    {
        var (_, r) = Climax(PersonaIds.Easygoing, trait);
        Assert.Equal(seconds, (r.Save.RefractoryUntilUtc - r.Now.AddSeconds(-1)).TotalSeconds, 6);
    }

    [Fact]
    public void VirileLengthensRefractoryOnlyAsABreeder()
    {
        var (_, plain) = Climax(PersonaIds.Easygoing, PersonaIds.Virile);
        Assert.Equal(60, (plain.Save.RefractoryUntilUtc - plain.Now.AddSeconds(-1)).TotalSeconds, 6);

        var r = As(PersonaIds.Easygoing, PersonaIds.Virile);
        PregnancyBook.Enable(r.Save.Pregnancy, false, true, 1, r.Now);
        r.Run(1);
        r.Save.Arousal = 99.9;
        r.Run(10);
        Assert.Equal(75, (r.Save.RefractoryUntilUtc - r.Now.AddSeconds(-1)).TotalSeconds, 6);
    }

    private static EngineRig ByHand(string personality, double arousal, string? title = null, bool hairTrigger = false)
    {
        var r = As(personality);
        r.Save.Setup!.AutoClimax = false;
        if (title is not null)
        {
            r.Save.Titles.Unlocked.Add(title);
            r.Save.Titles.Equipped = title;
        }
        if (hairTrigger) r.Save.Effects[ItemIds.HairTrigger] = new ActiveEffect { UsesLeft = 1 };
        r.Run(11);
        r.Save.Arousal = arousal;
        return r;
    }

    [Fact]
    public void SubmissiveNeedsEighty()
    {
        var r = ByHand(PersonaIds.Submissive, 75);
        Assert.Equal("Your arousal needs to be at least 80 (it's 75).", r.Engine.TryManualClimax(r.Now));
        r.Save.Arousal = 80;
        Assert.Null(r.Engine.TryManualClimax(r.Now));
    }

    [Fact]
    public void TeaseAndSubmissiveNeedSeventy()
    {
        var r = ByHand(PersonaIds.Submissive, 69, "shop_tease");
        Assert.NotNull(r.Engine.TryManualClimax(r.Now));
        r.Save.Arousal = 70;
        Assert.Null(r.Engine.TryManualClimax(r.Now));
    }

    [Fact]
    public void ASubmissivesHairTriggerNeedsFifty()
    {
        var r = ByHand(PersonaIds.Submissive, 45, hairTrigger: true);
        Assert.NotNull(r.Engine.TryManualClimax(r.Now));
        r.Save.Arousal = 50;
        Assert.Null(r.Engine.TryManualClimax(r.Now));
        Assert.False(ItemBook.IsActive(r.Save, ItemIds.HairTrigger, r.Now));
    }

    // ── Everything else ─────────────────────────────────────────────────────

    [Fact]
    public void NeedyCoolsDownTwiceAsFast()
    {
        var r = As(PersonaIds.Needy);
        r.Playing = false;
        r.Save.Arousal = 50;
        r.Save.ArousalAnchorUtc = r.Now;
        r.Run(31);
        Assert.Equal(48, r.Save.Arousal, 6);
    }

    [Fact]
    public void LoyalLosesTheStreakOnAMissedDay()
    {
        var save = new CharacterSave { StreakDays = 10, LastSceneDayUtc = EngineRig.T0.Date };
        Progression.UpdateStreak(save, EngineRig.T0.AddDays(2), endsOnMiss: true);
        Assert.Equal(1, save.StreakDays);
    }

    [Fact]
    public void AStreakCharmStillProtectsALoyalStreak()
    {
        var save = new CharacterSave { StreakDays = 10, LastSceneDayUtc = EngineRig.T0.Date };
        Progression.UpdateStreak(save, EngineRig.T0.AddDays(2), () => true, endsOnMiss: true);
        Assert.Equal(11, save.StreakDays);
    }

    [Fact]
    public void LoyalRaisesTheStreakCap()
    {
        var r = As(PersonaIds.Loyal);
        r.Save.StreakDays = 20;
        r.Save.LastSceneDayUtc = r.Now.Date;
        r.Run(11);
        Assert.Equal(1.4, r.Engine.Status.Factors.Streak, 6);
    }

    [Theory]
    [InlineData(PersonaIds.Lightweight, 45)]
    [InlineData(PersonaIds.Thrifty, 24)]
    public void ItemsLastLongerOrShorter(string trait, double minutes)
    {
        var r = As(PersonaIds.Easygoing, trait);
        r.Save.Inventory[ItemIds.Aphrodisiac] = 1;
        r.Run(1);
        Assert.Null(r.Engine.UseItem(ItemIds.Aphrodisiac, r.Now));
        Assert.Equal(minutes, (r.Save.Effects[ItemIds.Aphrodisiac].UntilUtc!.Value - r.Now).TotalMinutes, 6);
    }

    [Theory]
    [InlineData(PersonaIds.Thrifty, ItemIds.LustIncense, 68)]
    [InlineData(PersonaIds.Lightweight, ItemIds.LustIncense, 92)]
    public void TraitPrices(string trait, string item, int price)
    {
        var save = new CharacterSave { Setup = new ProfileSetup { Personality = PersonaIds.Easygoing, Traits = [trait] } };
        Assert.Equal(price, ItemBook.PriceFor(save, ItemCatalog.Find(item)!));
    }

    [Fact]
    public void FertilePillsCostDouble()
    {
        var save = new CharacterSave { Setup = new ProfileSetup { Personality = PersonaIds.Easygoing, Traits = [PersonaIds.Fertile] } };
        PregnancyBook.Enable(save.Pregnancy, true, false, 1, EngineRig.T0);
        Assert.Equal(60, ItemBook.PriceFor(save, ItemCatalog.Find(ItemIds.Contraceptive)!));
        Assert.Equal(160, ItemBook.PriceFor(save, ItemCatalog.Find(ItemIds.MorningAfter)!));
        Assert.Equal(60, ItemBook.PriceFor(save, ItemCatalog.Find(ItemIds.FertilityCharm)!));
    }

    [Fact]
    public void AnEdgersMinuteAtTheTopCountsDouble()
    {
        var r = As(PersonaIds.Easygoing, PersonaIds.Edger);
        r.Save.Setup!.AutoClimax = false;
        r.Run(1);
        r.Save.Arousal = 100;
        r.Run(60);
        Assert.Equal(120, r.Save.Goals.Stats["EdgedSeconds"]);
    }

    [Fact]
    public void MessyLayersCountMore()
    {
        var r = As(PersonaIds.Easygoing, PersonaIds.Messy);
        r.LayersWorn = 4;
        r.Run(11);
        Assert.Equal(1.18, r.Engine.Status.Factors.Marked, 6);
    }

    [Theory]
    [InlineData(PersonaIds.Romantic, null, 100 * 0.5)]
    [InlineData(PersonaIds.Curious, null, 100 * 1.5)]
    [InlineData(PersonaIds.Easygoing, PersonaIds.QuickLearner, 100 * 2.0)]
    public void NewPartnerXp(string personality, string? trait, double xp)
    {
        var r = As(personality, trait is null ? [] : [trait]);
        r.Save.SeenPartners.Clear();
        Assert.Equal(6 + xp, TickXp(r), 6);
    }

    [Theory]
    [InlineData(PersonaIds.Playful, 50)]
    [InlineData(PersonaIds.Curious, 37.5)]
    public void NewAnimationXp(string personality, double xp)
    {
        var r = As(personality);
        r.Save.SeenActs.Clear();
        Assert.Equal(6 + xp, TickXp(r), 6);
    }

    [Theory]
    [InlineData(PersonaIds.Curious, null, 8)]
    [InlineData(PersonaIds.Easygoing, PersonaIds.Clingy, 15)]
    public void BondTime(string personality, string? trait, double seconds)
    {
        var r = As(personality, trait is null ? [] : [trait]);
        r.Save.BondWith = r.Bob.Key;
        r.Save.BondFormed = true;
        r.BobExtra = new ExtraTag(0, BondHashes.Of(r.SelfKey!));
        r.Run(11);
        Assert.Equal(seconds, r.Save.BondSeconds, 6);
    }

    [Theory]
    [InlineData(PersonaIds.Reserved, null, 14, 1.0)]
    [InlineData(PersonaIds.Playful, null, 8, 0.9)]
    [InlineData(PersonaIds.Easygoing, PersonaIds.QuickLearner, 8, 0.9)]
    public void Fatigue(string personality, string? trait, int minutes, double fatigue)
    {
        var r = As(personality, trait is null ? [] : [trait]);
        r.Run(minutes * 60 + 5);
        Assert.Equal(fatigue, r.Engine.Status.Fatigue, 6);
    }

    [Fact]
    public void DominantTakesMoreOfAPartnersClimax()
    {
        var r = As(PersonaIds.Dominant);
        r.Engine.LayerOnPartnerClimax = false;
        r.Run(11);
        var before = TotalXp(r);
        r.BobClimaxes();
        Assert.Equal(45, TotalXp(r) - before, 6);
    }

    [Fact]
    public void GreedyEarnsMoreSins()
    {
        var plain = As(PersonaIds.Easygoing);
        var greedy = As(PersonaIds.Easygoing, PersonaIds.Greedy);
        plain.Run(601);
        greedy.Run(601);
        Assert.Equal(2, greedy.Save.Wallet.Sins - plain.Save.Wallet.Sins);
    }

    // ── Pent Up ─────────────────────────────────────────────────────────────

    private static EngineRig BackAfter(TimeSpan away, params string[] traits)
    {
        var r = As(PersonaIds.Easygoing, traits);
        r.Save.LastVerifiedUtc = r.Now - away;
        return r;
    }

    [Fact]
    public void BackAfterThirtyHoursAHypersexualIsPentUp()
    {
        var r = BackAfter(TimeSpan.FromHours(30), PersonaIds.Hypersexual);
        Assert.Equal(6 * 0.8, TickXp(r), 6);
        Assert.True(r.Engine.IsPentUp);
    }

    [Fact]
    public void PentUpEndsAfterFifteenVerifiedMinutes()
    {
        var r = BackAfter(TimeSpan.FromHours(30), PersonaIds.Hypersexual);
        r.Run(11);
        r.Run(15 * 60);
        Assert.False(r.Engine.IsPentUp);
        r.Run(10);
        Assert.Equal(1, r.Engine.Status.Factors.Persona, 6);
    }

    [Fact]
    public void ADayIsntLongEnough()
    {
        var r = BackAfter(TimeSpan.FromHours(23), PersonaIds.Hypersexual);
        Assert.Equal(6, TickXp(r), 6);
    }

    [Fact]
    public void OthersArentPentUp()
    {
        var r = BackAfter(TimeSpan.FromHours(30));
        Assert.Equal(6, TickXp(r), 6);
        Assert.False(r.Engine.IsPentUp);
    }
}
