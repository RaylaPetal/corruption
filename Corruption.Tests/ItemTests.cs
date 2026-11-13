using Corruption.Core;

namespace Corruption.Tests;

public class ItemTests
{
    private static readonly DateTime T0 = EngineRig.T0;

    private static CharacterSave Rich() => new() { Setup = new ProfileSetup(), Wallet = { Sins = 1000 } };

    private static CharacterSave With(params string[] items)
    {
        var save = Rich();
        foreach (var id in items)
        {
            Assert.Null(ItemBook.Buy(save, id));
            Assert.Null(ItemBook.Use(save, id, T0));
        }
        return save;
    }

    [Fact]
    public void TwentyOneItems() => Assert.Equal(21, ItemCatalog.All.Count);

    [Fact]
    public void BuyingTakesThePrice()
    {
        var save = new CharacterSave { Setup = new ProfileSetup(), Wallet = { Sins = 100 } };
        Assert.Null(ItemBook.Buy(save, ItemIds.Aphrodisiac));
        Assert.Equal(60, save.Wallet.Sins);
        Assert.Equal(1, ItemBook.Owned(save, ItemIds.Aphrodisiac));
    }

    [Fact]
    public void TooFewSins()
    {
        var save = new CharacterSave { Setup = new ProfileSetup(), Wallet = { Sins = 20 } };
        Assert.Contains("60 more Sins", ItemBook.Buy(save, ItemIds.LustIncense));
        Assert.Equal(20, save.Wallet.Sins);
        Assert.Empty(save.Inventory);
    }

    [Fact]
    public void PregnancyItemsNeedTheRole()
    {
        var save = Rich();
        Assert.Equal(13, ItemBook.Offered(save).Count());
        Assert.NotNull(ItemBook.Buy(save, ItemIds.FertilityCharm));
        save.Pregnancy = new PregnancyState { Enabled = true, CanBreed = true };
        Assert.Equal([ItemIds.VirilityTonic], ItemBook.Offered(save).Where(i => i.Role != ItemRole.Anyone).Select(i => i.Id));
        save.Pregnancy.CanCarry = true;
        Assert.Equal(21, ItemBook.Offered(save).Count());
    }

    [Fact]
    public void UsingStartsAndExtendsUpToFourTimes()
    {
        var save = Rich();
        for (var i = 0; i < 6; i++) ItemBook.Buy(save, ItemIds.Aphrodisiac);
        ItemBook.Use(save, ItemIds.Aphrodisiac, T0);
        Assert.Equal(T0.AddMinutes(30), save.Effects[ItemIds.Aphrodisiac].UntilUtc);
        ItemBook.Use(save, ItemIds.Aphrodisiac, T0.AddMinutes(20));
        Assert.Equal(T0.AddMinutes(60), save.Effects[ItemIds.Aphrodisiac].UntilUtc);
        for (var i = 0; i < 4; i++) ItemBook.Use(save, ItemIds.Aphrodisiac, T0.AddMinutes(20));
        Assert.Equal(T0.AddMinutes(20 + 120), save.Effects[ItemIds.Aphrodisiac].UntilUtc);
    }

    [Fact]
    public void OppositesCancel()
    {
        var save = With(ItemIds.Aphrodisiac, ItemIds.NumbingBalm);
        Assert.False(ItemBook.IsActive(save, ItemIds.Aphrodisiac, T0));
        Assert.Equal(0.6, ItemBook.ArousalFactor(save, T0), 6);
    }

    [Fact]
    public void ExpiresWhileLoggedOut()
    {
        var save = With(ItemIds.LustIncense);
        Assert.Equal(1.25, ItemBook.TickXpFactor(save, T0.AddMinutes(59)), 6);
        ItemBook.Expire(save, T0.AddHours(2));
        Assert.Empty(save.Effects);
        Assert.Equal(1, ItemBook.TickXpFactor(save, T0.AddHours(2)), 6);
    }

    [Fact]
    public void RefusedInstantStaysInTheInventory()
    {
        var save = Rich();
        save.Pregnancy = new PregnancyState { Enabled = true, CanCarry = true };
        ItemBook.Buy(save, ItemIds.SwiftLabour);
        Assert.Equal("Not pregnant.", ItemBook.Use(save, ItemIds.SwiftLabour, T0, _ => "Not pregnant."));
        Assert.Equal(1, ItemBook.Owned(save, ItemIds.SwiftLabour));
        Assert.Null(ItemBook.Use(save, ItemIds.SwiftLabour, T0, _ => null));
        Assert.Equal(0, ItemBook.Owned(save, ItemIds.SwiftLabour));
    }

    [Fact]
    public void CountedUsesStackAndRunOut()
    {
        var save = With(ItemIds.CumMagnet, ItemIds.CumMagnet);
        Assert.Equal(6, save.Effects[ItemIds.CumMagnet].UsesLeft);
        for (var i = 0; i < 6; i++) Assert.True(ItemBook.TrySpendUse(save, ItemIds.CumMagnet));
        Assert.False(ItemBook.TrySpendUse(save, ItemIds.CumMagnet));
        Assert.False(save.Effects.ContainsKey(ItemIds.CumMagnet));
    }

    [Fact]
    public void ResetClearsItems()
    {
        var save = With(ItemIds.LustIncense);
        ItemBook.Buy(save, ItemIds.Aphrodisiac);
        save.ResetProgress(T0);
        Assert.Empty(save.Inventory);
        Assert.Empty(save.Effects);
    }

    // ── Effects in scenes ──────────────────────────────────────────────────────

    private static EngineRig Scene(params string[] items)
    {
        var r = new EngineRig();
        r.Engine.LayerOnPartnerClimax = false;
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        r.Save.Wallet.Sins = 2000;
        foreach (var id in items)
        {
            Assert.Null(r.Engine.BuyItem(id));
            Assert.Null(r.Engine.UseItem(id, r.Now));
        }
        return r;
    }

    [Fact]
    public void AphrodisiacAndBalm()
    {
        var fast = Scene(ItemIds.Aphrodisiac);
        fast.Run(11);
        Assert.Equal(4.5, fast.Save.Arousal, 6);
        var slow = Scene(ItemIds.NumbingBalm);
        slow.Run(11);
        Assert.Equal(1.8, slow.Save.Arousal, 6);
    }

    [Fact]
    public void IncenseRaisesTickXp()
    {
        var r = Scene(ItemIds.LustIncense);
        r.Run(11);
        Assert.Equal(7.5, r.Save.Xp, 6);
        Assert.Equal(1.25, r.Engine.Status.Factors.Items, 6);
    }

    [Fact]
    public void PerfumeDoublesWatchers()
    {
        var r = Scene(ItemIds.Perfume);
        r.Watchers = 1;
        r.Run(11);
        Assert.Equal(1.2, r.Engine.Status.Factors.Watchers, 6);
    }

    [Fact]
    public void SalveStopsFatigue()
    {
        var r = Scene(ItemIds.FatigueSalve);
        r.Run(15 * 60);
        Assert.Equal(1, r.Engine.Status.Fatigue);
    }

    [Fact]
    public void WineHalvesRefractory()
    {
        var r = Scene(ItemIds.AfterglowWine);
        r.Save.Level = 50;
        r.Save.Arousal = 99;
        r.Run(11);
        Assert.InRange((r.Save.RefractoryUntilUtc - r.Now).TotalSeconds, 20, 30);
    }

    [Fact]
    public void HairTriggerClimaxesFromFortyOnce()
    {
        var r = Scene(ItemIds.HairTrigger);
        r.Save.Level = 50;
        r.Run(2);
        r.Save.Arousal = 45;
        Assert.Null(r.Engine.TryManualClimax(r.Now));
        Assert.False(ItemBook.IsActive(r.Save, ItemIds.HairTrigger, r.Now));
        r.Now = r.Now.AddMinutes(2);
        r.Run(2);
        r.Save.Arousal = 45;
        Assert.Contains("at least 70", r.Engine.TryManualClimax(r.Now));
    }

    [Fact]
    public void StreakCharmCoversAMissedDay()
    {
        var save = With(ItemIds.StreakCharm);
        save.StreakDays = 8;
        save.LastSceneDayUtc = T0.Date;
        Progression.UpdateStreak(save, T0.AddDays(2), () => ItemBook.TrySpendUse(save, ItemIds.StreakCharm));
        Assert.Equal(9, save.StreakDays);
        Assert.False(save.Effects.ContainsKey(ItemIds.StreakCharm));
    }

    [Fact]
    public void CumMagnetLayersTwoPartsAndSpendsAUse()
    {
        var r = Scene(ItemIds.CumMagnet);
        r.Engine.LayerOnPartnerClimax = true;
        r.Save.Level = 50;
        r.Run(11);
        r.BobClimaxes();
        Assert.Equal(2, r.Host.Layers.Select(l => l.Part).Distinct().Count());
        Assert.Equal(2, r.Save.Effects[ItemIds.CumMagnet].UsesLeft);
    }
}

public class ShopCategoryTests
{
    [Theory]
    [InlineData(ItemIds.Aphrodisiac, ItemCategory.Scene)]
    [InlineData(ItemIds.FatigueSalve, ItemCategory.Scene)]
    [InlineData(ItemIds.AfterglowWine, ItemCategory.Climax)]
    [InlineData(ItemIds.StreakCharm, ItemCategory.Climax)]
    [InlineData(ItemIds.VirilityTonic, ItemCategory.Fertility)]
    [InlineData(ItemIds.CravingTreat, ItemCategory.PregnancyCare)]
    [InlineData(ItemIds.Toy, ItemCategory.Solo)]
    [InlineData(ItemIds.DoNotDisturb, ItemCategory.Solo)]
    [InlineData(ItemIds.VanityMirror, ItemCategory.Solo)]
    [InlineData(ItemIds.AfterglowCandle, ItemCategory.Solo)]
    public void ItemsSitInTheirSection(string id, ItemCategory category)
        => Assert.Equal(category, ItemCatalog.CategoryOf(ItemCatalog.Find(id)!));

    [Fact]
    public void EverySectionHasItems()
        => Assert.All(ItemCatalog.Categories, c => Assert.Contains(ItemCatalog.All, i => ItemCatalog.CategoryOf(i) == c.Category));
}
