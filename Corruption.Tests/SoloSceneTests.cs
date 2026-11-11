using Corruption.Core;

namespace Corruption.Tests;

/// Solo scenes in a real engine.
public class SoloSceneTests
{
    /// The shipped balance without the moon, a solo-marked act, nobody nearby, and nothing left to earn for the first time.
    private static EngineRig Alone(string? title = null)
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon()) { BobVisible = false };
        r.Act.Solo = true;
        r.Save.Setup!.Personality = PersonaIds.Easygoing;
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        if (title is not null)
        {
            r.Save.Titles.Unlocked.Add(title);
            r.Save.Titles.Equipped = title;
        }
        return r;
    }

    private static double TotalXp(EngineRig r)
    {
        var total = r.Save.Xp;
        for (var level = 1; level < r.Save.Level; level++) total += Progression.XpToNext(level, r.Balance);
        return total;
    }

    [Fact]
    public void APlainSoloTick()
    {
        var r = Alone();
        r.Run(11);
        Assert.True(r.Engine.IsSolo);
        Assert.True(r.Engine.Status.Solo);
        Assert.Equal(SceneState.Earning, r.Engine.Status.State);
        Assert.Null(r.Engine.Status.Partner);
        Assert.Equal(3, r.Engine.Status.LastTickXp, 6);
        Assert.Equal(1.5, r.Save.Arousal, 6);
    }

    [Fact]
    public void ADuoActAloneEarnsNothing()
    {
        var r = Alone();
        r.Act.Solo = false;
        r.Run(30);
        Assert.Equal(0, r.Save.Xp);
        Assert.Equal(SceneFailure.NoPartner, r.Engine.Status.Failure);
    }

    [Fact]
    public void ASoloOnlyDayCountsForTheStreak()
    {
        var r = Alone();
        r.Run(11);
        Assert.Equal(1, r.Save.StreakDays);
        Assert.Equal(r.Now.Date, r.Save.LastSceneDayUtc.Date);
    }

    [Fact]
    public void ASoloSummaryHasNoPartners()
    {
        var r = Alone();
        r.Run(70);
        r.Playing = false;
        r.Run(31);
        var summary = Assert.Single(r.Host.Scenes);
        Assert.Empty(summary.Partners);
        Assert.Empty(r.Save.Partners);
        Assert.Empty(r.Save.Goals.PartnerScenes);
    }

    [Fact]
    public void APartnerArrivingMakesItDuo()
    {
        var r = Alone();
        r.Save.SeenPartners.Add(r.Bob.Key);
        r.Run(11);
        Assert.Equal(3, r.Engine.Status.LastTickXp, 6);
        r.BobVisible = true;
        r.Run(11);
        Assert.False(r.Engine.IsSolo);
        Assert.Equal(6, r.Engine.Status.LastTickXp, 6);
        Assert.Equal(r.Bob.Key, r.Engine.Status.Partner!.Key);
    }

    [Fact]
    public void SoloTimeCountsForSceneAndSoloGoals()
    {
        var r = Alone();
        r.Run(61);
        Assert.Equal(60, r.Save.Goals.Stats["SceneSeconds"]);
        Assert.Equal(60, r.Save.Goals.Stats["SoloSeconds"]);
    }

    [Fact]
    public void NoBondChemistryOrPartnerEffectsAlone()
    {
        var r = Alone();
        r.Save.Setup!.Personality = PersonaIds.Dominant;
        r.Save.BondWith = r.Bob.Key;
        r.Save.BondFormed = true;
        r.Run(11);
        Assert.Equal(1, r.Engine.Status.Factors.Bonded, 6);
        Assert.Equal(1, r.Engine.Status.Factors.Persona, 6);
        Assert.Equal(1, r.Engine.Status.Factors.Partner, 6);
        Assert.Equal(0, r.Save.BondSeconds);
    }

    [Fact]
    public void AClingyPlayerAloneIsntAwayFromTheirBond()
    {
        var r = Alone();
        r.Save.Setup!.Traits = [PersonaIds.Clingy];
        r.Save.BondWith = r.Bob.Key;
        r.Save.BondFormed = true;
        r.Run(11);
        Assert.Equal(1, r.Engine.Status.Factors.Persona, 6);
    }

    [Fact]
    public void WatchersStillCount()
    {
        var r = Alone();
        r.Watchers = 2;
        r.Run(11);
        Assert.Equal(1.2, r.Engine.Status.Factors.Watchers, 6);
        Assert.Equal(3 * 1.2, r.Engine.Status.LastTickXp, 6);
    }

    // ── The budget ──────────────────────────────────────────────────────────

    [Fact]
    public void TheBudgetRunsOut()
    {
        var r = Alone();
        r.Run(45 * 60);
        var xp = TotalXp(r);
        var arousal = r.Save.Arousal;
        var sins = r.Save.Wallet.Sins;
        r.Run(60);
        Assert.Equal(SceneFailure.SoloBudgetSpent, r.Engine.Status.Failure);
        Assert.Equal(xp, TotalXp(r), 6);
        Assert.True(r.Save.Arousal <= arousal);
        Assert.Equal(sins, r.Save.Wallet.Sins);
        Assert.Equal(0, r.Engine.SoloSecondsLeft(r.Now));
    }

    [Fact]
    public void ANewDayRestoresTheBudget()
    {
        var r = Alone();
        r.Run(45 * 60 + 5);
        r.Now = r.Now.Date.AddDays(1).AddHours(1);
        r.Save.ArousalAnchorUtc = r.Now;
        r.Run(11);
        Assert.True(r.Engine.IsSolo);
        Assert.True(r.Engine.SoloSecondsLeft(r.Now) > 44 * 60);
    }

    [Fact]
    public void DuoTimeDoesntUseTheBudget()
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon());
        r.Act.Solo = true;
        r.Run(3600);
        Assert.False(r.Engine.IsSolo);
        Assert.Equal(45 * 60, r.Engine.SoloSecondsLeft(r.Now));
    }

    // ── Climaxing alone ─────────────────────────────────────────────────────

    /// XP from one automatic climax alone: the tick that brings it, less that tick.
    private static double SoloClimaxXp(EngineRig r)
    {
        r.Run(1);
        r.Save.Arousal = 99.9;
        var before = TotalXp(r);
        r.Run(10);
        Assert.Contains("You climaxed.", r.Host.Notes);
        return TotalXp(r) - before - r.Engine.Status.LastTickXp;
    }

    [Fact]
    public void AClimaxAlonePaysHalf()
    {
        var r = Alone();
        Assert.Equal(30, SoloClimaxXp(r), 6);
        Assert.Equal(20, r.Save.Arousal, 6);
        Assert.Equal(1, r.Save.Goals.Stats["SoloClimaxes"]);
    }

    [Fact]
    public void AnAfterglowCandlePaysItInFull()
    {
        var r = Alone();
        r.Save.Effects[ItemIds.AfterglowCandle] = new ActiveEffect { UsesLeft = 2 };
        Assert.Equal(60, SoloClimaxXp(r), 6);
        Assert.Equal(1, r.Save.Effects[ItemIds.AfterglowCandle].UsesLeft);
    }

    [Fact]
    public void ClimaxingByHandAlone()
    {
        var r = Alone();
        r.Save.Setup!.AutoClimax = false;
        r.Run(11);
        r.Save.Arousal = 80;
        Assert.Null(r.Engine.TryManualClimax(r.Now));
        Assert.Contains("You climaxed.", r.Host.Notes);
    }

    [Fact]
    public void AnAudienceCountsForCaughtInTheAct()
    {
        var r = Alone();
        r.Watchers = 3;
        SoloClimaxXp(r);
        Assert.Equal(1, r.Save.CaughtInTheAct);
    }

    // ── Layering yourself ───────────────────────────────────────────────────

    private static void ClimaxAlone(EngineRig r)
    {
        r.Run(1);
        r.Save.Arousal = 99.9;
        r.Run(10);
    }

    [Fact]
    public void WithAPenisTheHandNotTheGroin()
    {
        var r = Alone();
        r.Save.Anatomy = Anatomy.Penis;
        r.Host.SelfAim = ["gr", "hr", "st"];
        ClimaxAlone(r);
        Assert.Equal([("Alice A@W", "hr")], r.Host.Layers);
    }

    [Fact]
    public void WithAVaginaTheGroin()
    {
        var r = Alone();
        r.Save.Anatomy = Anatomy.Vagina;
        r.Host.SelfAim = ["hr", "st", "gr"];
        ClimaxAlone(r);
        Assert.Equal([("Alice A@W", "gr")], r.Host.Layers);
    }

    [Fact]
    public void AFullPartMovesOn()
    {
        var r = Alone();
        r.Save.Anatomy = Anatomy.Penis;
        r.Host.SelfAim = ["gr", "hr", "st"];
        r.Host.Outcomes.Enqueue("full");
        ClimaxAlone(r);
        Assert.Equal(["hr", "st"], r.Host.Layers.Select(l => l.Part));
    }

    [Fact]
    public void ALayerAlonePaysNoLayerXp()
    {
        var r = Alone();
        r.Host.SelfAim = ["hr"];
        Assert.Equal(30, SoloClimaxXp(r), 6);
        Assert.Single(r.Host.Layers);
        Assert.False(r.Save.Goals.Stats.ContainsKey("LayersReceived"));
    }

    [Fact]
    public void TheSettingTurnsItOff()
    {
        var r = Alone();
        r.Engine.LayerSelfWhenAlone = false;
        r.Host.SelfAim = ["hr"];
        ClimaxAlone(r);
        Assert.Empty(r.Host.Layers);
    }

    [Fact]
    public void WithoutCummyNothingBreaks()
    {
        var r = Alone();
        r.Host.LayerOutcome = null;
        ClimaxAlone(r);
        Assert.Contains("You climaxed.", r.Host.Notes);
        Assert.Single(r.Host.Layers);
    }

    [Theory]
    [InlineData(Anatomy.Penis)]
    [InlineData(Anatomy.Both)]
    [InlineData(Anatomy.NotSet)]
    public void WithoutBonesTheGroinIsLeftOutExceptForAVagina(Anatomy anatomy)
        => Assert.DoesNotContain(CummyParts.Groin, SceneEngine.SelfAimOrder(anatomy, null));

    [Fact]
    public void WithoutBonesAVaginaStillGetsTheGroinFirst()
        => Assert.Equal(CummyParts.Groin, SceneEngine.SelfAimOrder(Anatomy.Vagina, null)[0]);

    // ── Solo items ──────────────────────────────────────────────────────────

    private static EngineRig Holding(string id, int count = 1)
    {
        var r = Alone();
        r.Save.Inventory[id] = count;
        return r;
    }

    [Fact]
    public void ADoNotDisturbSignAddsTwentyMinutes()
    {
        var r = Holding(ItemIds.DoNotDisturb);
        r.Run(40 * 60);
        Assert.Equal(5 * 60, r.Engine.SoloSecondsLeft(r.Now), 0);
        Assert.Null(r.Engine.UseItem(ItemIds.DoNotDisturb, r.Now));
        Assert.Equal(25 * 60, r.Engine.SoloSecondsLeft(r.Now), 0);
        Assert.Equal(0, ItemBook.Owned(r.Save, ItemIds.DoNotDisturb));
    }

    [Fact]
    public void ASignWithTimeToSpareMakesSixtyFive()
    {
        var r = Holding(ItemIds.DoNotDisturb);
        r.Engine.UseItem(ItemIds.DoNotDisturb, r.Now);
        Assert.Equal(65 * 60, r.Engine.SoloSecondsLeft(r.Now), 0);
    }

    [Fact]
    public void AToySpeedsSoloArousal()
    {
        var r = Holding(ItemIds.Toy);
        r.Run(1);
        r.Engine.UseItem(ItemIds.Toy, r.Now);
        r.Run(10);
        Assert.Equal(1.5 * 1.3, r.Save.Arousal, 6);
    }

    [Fact]
    public void AToyDoesNothingWithAPartner()
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon());
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        r.Save.Inventory[ItemIds.Toy] = 1;
        r.Run(1);
        r.Engine.UseItem(ItemIds.Toy, r.Now);
        r.Run(10);
        Assert.Equal(1.5, r.Save.Arousal, 6);
        Assert.True(ItemBook.IsActive(r.Save, ItemIds.Toy, r.Now));
    }

    [Fact]
    public void AVanityMirrorCountsAWatcher()
    {
        var r = Holding(ItemIds.VanityMirror);
        r.Run(1);
        r.Engine.UseItem(ItemIds.VanityMirror, r.Now);
        r.Run(10);
        Assert.Equal(1.1, r.Engine.Status.Factors.Watchers, 6);
    }

    [Fact]
    public void TheSoloItemsSitInTheirSection()
        => Assert.All(new[] { ItemIds.Toy, ItemIds.DoNotDisturb, ItemIds.VanityMirror, ItemIds.AfterglowCandle },
            id => Assert.Equal(ItemCategory.Solo, ItemCatalog.CategoryOf(ItemCatalog.Find(id)!)));

    // ── Goals and achievements ──────────────────────────────────────────────

    [Fact]
    public void SoloGoalsWaitForASoloScene()
    {
        var save = new CharacterSave { Goals = { Seed = 1 } };
        var book = new GoalBook(save);
        string[] solo = ["d_solo_climax", "d_solo15", "w_solo_climax", "w_solo120"];
        for (var day = 0; day < 200; day++)
        {
            book.Roll(EngineRig.T0.AddDays(day));
            Assert.DoesNotContain(save.Goals.Daily.Concat(save.Goals.Weekly), g => solo.Contains(g.Id));
        }

        save.Goals.Stats["SoloSeconds"] = 10;
        var seen = new HashSet<string>();
        for (var day = 200; day < 900; day++)
        {
            book.Roll(EngineRig.T0.AddDays(day));
            foreach (var goal in save.Goals.Daily.Concat(save.Goals.Weekly)) seen.Add(goal.Id);
        }
        Assert.All(solo, id => Assert.Contains(id, seen));
    }

    [Theory]
    [InlineData("d_solo_climax")]
    [InlineData("d_solo15")]
    [InlineData("w_solo_climax")]
    [InlineData("w_solo120")]
    public void EachSoloGoalCompletesFromItsStat(string id)
    {
        var save = new CharacterSave();
        var book = new GoalBook(save);
        book.Roll(EngineRig.T0);
        var def = GoalCatalog.Find(id)!;
        save.Goals.Daily = id.StartsWith('d') ? [new GoalState { Id = id }] : [];
        save.Goals.Weekly = id.StartsWith('w') ? [new GoalState { Id = id }] : [];
        book.Record(def.Kind, def.Target);
        Assert.Contains(book.TakeRewards(), r => r.Title == def.Text);
    }

    [Theory]
    [InlineData("self_love", 1)]
    [InlineData("hands_on", 25)]
    [InlineData("self_sufficient", 100)]
    public void SoloClimaxAchievements(string id, int climaxes)
    {
        var save = new CharacterSave();
        var book = new GoalBook(save);
        book.Record(GoalKind.SoloClimaxes, climaxes - 1);
        book.TakeRewards();
        Assert.DoesNotContain(id, save.Goals.Achievements);
        book.Record(GoalKind.SoloClimaxes);
        book.TakeRewards();
        Assert.Contains(id, save.Goals.Achievements);
    }

    [Fact]
    public void MeTimeAndCaughtInTheAct()
    {
        var save = new CharacterSave { CaughtInTheAct = 1 };
        var book = new GoalBook(save);
        book.Record(GoalKind.SoloSeconds, 5 * 3600);
        book.TakeRewards();
        Assert.Contains("me_time", save.Goals.Achievements);
        Assert.Contains("caught_in_the_act", save.Goals.Achievements);
        Assert.True(GoalCatalog.FindAchievement("caught_in_the_act")!.Hidden);
        Assert.Equal("hands_on", GoalCatalog.FindAchievement("self_sufficient")!.Series);
    }

    [Fact]
    public void AFirstSoloClimaxUnlocksSelfLove()
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon()) { BobVisible = false };
        r.Act.Solo = true;
        r.Run(1);
        r.Save.Arousal = 99.9;
        r.Run(10);
        Assert.Contains("self_love", r.Save.Goals.Achievements);
    }

    // ── Solo titles: only alone ─────────────────────────────────────────────

    [Fact]
    public void MeTimeGivesAnHour() => Assert.Equal(60 * 60, Alone("me_time").Engine.SoloSecondsLeft(EngineRig.T0));

    [Fact]
    public void SelfLoveGivesFiftyMinutes() => Assert.Equal(50 * 60, Alone("self_love").Engine.SoloSecondsLeft(EngineRig.T0));

    [Fact]
    public void HandsOnPaysMoreForAClimaxAlone() => Assert.Equal(45, SoloClimaxXp(Alone("hands_on")), 6);

    [Fact]
    public void SelfSufficientPaysMoreForASoloTick()
    {
        var r = Alone("self_sufficient");
        r.Run(11);
        Assert.Equal(3 * 1.2, r.Engine.Status.LastTickXp, 6);
    }

    [Fact]
    public void CaughtInTheActWeighsWatchersAlone()
    {
        var r = Alone("caught_in_the_act");
        r.Watchers = 2;
        r.Run(11);
        Assert.Equal(1.3, r.Engine.Status.Factors.Watchers, 6);
    }

    [Fact]
    public void LoneWolfSpeedsSoloArousal()
    {
        var r = Alone("shop_lone_wolf");
        r.Run(11);
        Assert.Equal(1.5 * 1.15, r.Save.Arousal, 6);
    }

    /// A duo scene wearing a solo title: nothing changes.
    [Theory]
    [InlineData("self_sufficient")]
    [InlineData("caught_in_the_act")]
    [InlineData("shop_lone_wolf")]
    [InlineData("hands_on")]
    public void SoloTitlesDoNothingWithAPartner(string title)
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon());
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        r.Save.Titles.Unlocked.Add(title);
        r.Save.Titles.Equipped = title;
        r.Engine.LayerOnPartnerClimax = false;
        r.Watchers = 2;
        r.Run(1);
        r.Save.Arousal = 99.9;
        var before = TotalXp(r);
        r.Run(10);
        Assert.Equal(1.2, r.Engine.Status.Factors.Watchers, 6);
        Assert.Equal(6 * 1.2, r.Engine.Status.LastTickXp, 6);
        Assert.Equal(60, TotalXp(r) - before - r.Engine.Status.LastTickXp, 6);
    }

    [Fact]
    public void LoneWolfIsSold()
    {
        var save = new CharacterSave { Titles = new TitleSave { Initialized = true }, Wallet = { Sins = 900 } };
        Assert.True(TitleBook.Buy(save, TitleCatalog.Find("shop_lone_wolf")!));
        Assert.Equal(100, save.Wallet.Sins);
    }

    [Fact]
    public void ASoloSummarySaysAlone()
        => Assert.Equal(["1:05 · +3 XP · +1 Sins", "Alone"], SceneSummaries.Lines(new SceneSummary { VerifiedSeconds = 65, Xp = 3, Sins = 1 }));
}
