using Corruption.Core;

namespace Corruption.Tests;

/// The title catalogue, unlocking, buying, equipping and the scene lock.
public class TitleTests
{
    private static readonly DateTime T0 = EngineRig.T0;

    [Fact]
    public void EveryAchievementHasATitleWithItsIdAndName()
    {
        foreach (var achievement in GoalCatalog.Achievements)
        {
            var title = TitleCatalog.Find(achievement.Id);
            Assert.NotNull(title);
            Assert.Equal(achievement.Name, title.Name);
            Assert.Equal(achievement.Id, title.Achievement);
        }
    }

    [Fact]
    public void IdsAreUniqueAndSafeToSend()
    {
        var ids = TitleCatalog.All.Select(t => t.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.True(TitleCatalog.IsValidId(id), id));
        Assert.False(TitleCatalog.IsValidId("Has Spaces"));
        Assert.False(TitleCatalog.IsValidId("a|b"));
        Assert.False(TitleCatalog.IsValidId(new string('a', 25)));
    }

    [Fact]
    public void HiddenAchievementsGiveHiddenTitles()
    {
        foreach (var achievement in GoalCatalog.Achievements.Where(a => a.Hidden))
            Assert.Equal(TitleSource.Hidden, TitleCatalog.Find(achievement.Id)!.Source);
    }

    private static CharacterSave Ready() => new() { Titles = new TitleSave { Initialized = true } };

    [Fact]
    public void ReachingLevel25UnlocksLibertine()
    {
        var save = Ready();
        save.Level = 24;
        TitleBook.Sync(save);
        Assert.False(TitleBook.IsUnlocked(save, "lv_25"));
        save.Level = 25;
        var unlocked = TitleBook.Sync(save);
        Assert.Contains(unlocked, t => t.Name == "Libertine");
        Assert.Empty(TitleBook.Sync(save));
    }

    [Fact]
    public void TheFirstSyncUnlocksEarlierProgressSilently()
    {
        var save = new CharacterSave { Level = 3, Prestige = 1 };
        save.Goals.Achievements.Add("insatiable");
        save.Goals.Achievements.Add("devout");

        Assert.Empty(TitleBook.Sync(save));
        Assert.True(save.Titles.Initialized);
        foreach (var id in new[] { "insatiable", "devout", "star_1", "lv_10", "lv_25", "lv_50", "lv_75", "lv_100" })
            Assert.True(TitleBook.IsUnlocked(save, id), id);
        Assert.False(TitleBook.IsUnlocked(save, "star_10"));
    }

    [Fact]
    public void PrestigeKeepsTitlesAndAddsReborn()
    {
        var save = Ready();
        save.Setup = new ProfileSetup();
        save.Level = 100;
        TitleBook.Sync(save);
        save.Titles.Equipped = "lv_100";
        Assert.True(PrestigeRules.Apply(save, T0, new Balance()));

        var unlocked = TitleBook.Sync(save);
        Assert.Contains(unlocked, t => t.Id == "star_1");
        Assert.True(TitleBook.IsUnlocked(save, "lv_100"));
        Assert.Equal("lv_100", save.Titles.Equipped);
    }

    [Fact]
    public void AResetClearsEveryTitle()
    {
        var save = Ready();
        save.Wallet.Sins = 2000;
        Assert.True(TitleBook.Buy(save, TitleCatalog.Find(TitleCatalog.Unholy)!));
        save.Titles.Equipped = TitleCatalog.Unholy;
        save.ResetProgress(T0);
        Assert.Empty(save.Titles.Unlocked);
        Assert.Null(save.Titles.Equipped);
    }

    [Fact]
    public void BuyingATitleUnlocksItForGood()
    {
        var save = Ready();
        save.Wallet.Sins = 600;
        var harlot = TitleCatalog.Find("shop_harlot")!;
        Assert.True(TitleBook.Buy(save, harlot));
        Assert.Equal(100, save.Wallet.Sins);
        Assert.Equal(500, save.Wallet.SpentTotal);
        Assert.True(TitleBook.IsUnlocked(save, "shop_harlot"));
        Assert.Equal("You already own it.", TitleBook.BuyRefusal(save, harlot));
    }

    [Fact]
    public void NotEnoughSinsBuysNothing()
    {
        var save = Ready();
        save.Wallet.Sins = 300;
        var tease = TitleCatalog.Find("shop_tease")!;
        Assert.False(TitleBook.Buy(save, tease));
        Assert.Equal(300, save.Wallet.Sins);
        Assert.Equal("You need 100 more Sins.", TitleBook.BuyRefusal(save, tease));
    }

    [Fact]
    public void OnlyShopTitlesAreForSale()
        => Assert.NotNull(TitleBook.BuyRefusal(Ready(), TitleCatalog.Find("insatiable")!));

    [Fact]
    public void OnlyTheEquippedTitlesBonusApplies()
    {
        var save = Ready();
        save.Titles.Unlocked.UnionWith(["shameless", "devout"]);
        save.Titles.Equipped = "devout";
        Assert.Equal(10, TitleBook.Bonus(save).CheckInBonus);
        Assert.Null(TitleBook.Bonus(save).WatcherCap);
        save.Titles.Equipped = "shameless";
        Assert.Equal(1.6, TitleBook.Bonus(save).WatcherCap);
        Assert.Equal(0, TitleBook.Bonus(save).CheckInBonus);
    }

    [Fact]
    public void AnEquippedTitleThatIsntUnlockedGivesNothing()
    {
        var save = Ready();
        save.Titles.Equipped = "fallen_star";
        Assert.Same(TitleBonus.None, TitleBook.Bonus(save));
    }

    // ── Equipping through the engine ────────────────────────────────────────

    private static EngineRig Rig()
    {
        var r = new EngineRig();
        r.Save.Titles.Unlocked.UnionWith(["insatiable", "devout"]);
        r.Save.Titles.Initialized = true;
        return r;
    }

    [Fact]
    public void ALockedTitleCantBeEquipped()
    {
        var r = Rig();
        Assert.Equal("You haven't unlocked that title yet.", r.Engine.TryEquipTitle("fallen_star", r.Now));
        Assert.Null(r.Save.Titles.Equipped);
    }

    [Fact]
    public void TitlesCantChangeMidScene()
    {
        var r = Rig();
        r.Run(15);
        Assert.Equal("Titles can only change outside a scene.", r.Engine.TryEquipTitle("devout", r.Now));
        Assert.Null(r.Save.Titles.Equipped);
    }

    [Fact]
    public void TitlesCantChangeDuringRefractory()
    {
        var r = Rig();
        r.Playing = false;
        r.Run(1);
        r.Save.RefractoryUntilUtc = r.Now.AddSeconds(50);
        Assert.Equal("Titles can't change while you recover. 50 seconds left.", r.Engine.TryEquipTitle("insatiable", r.Now));
    }

    [Fact]
    public void LeftoverArousalAfterASceneDoesntLockTitles()
    {
        var r = Rig();
        r.Run(15);
        r.Playing = false;
        r.Run(120);
        r.Save.Arousal = 60;
        Assert.Null(r.Engine.TitleLock(r.Now));
        Assert.Null(r.Engine.TryEquipTitle("insatiable", r.Now));
        Assert.Equal("insatiable", r.Save.Titles.Equipped);
        Assert.Null(r.Engine.TryEquipTitle(null, r.Now));
        Assert.Null(r.Save.Titles.Equipped);
    }

    [Fact]
    public void UnlockingDuringPlayTellsThePlayer()
    {
        var r = new EngineRig();
        r.Run(1);
        r.Save.Goals.Stats["Climaxes"] = 10;
        r.Run(1);
        Assert.Contains(r.Engine.LastUnlockedTitles, t => t.Id == "worked_up");
        Assert.Contains("Title unlocked: Worked Up. Arousal builds ×1.05 as fast.", r.Host.Notes);
    }
}
