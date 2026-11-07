using Corruption.Core;

namespace Corruption.Tests;

public class PrestigeTests
{
    private static readonly DateTime T0 = EngineRig.T0;
    private readonly Balance b = new();

    private static CharacterSave Capped() => new()
    {
        Setup = new ProfileSetup(), Level = 100, Arousal = 55, StreakDays = 6,
        Goals = { Achievements = { "habit" } },
    };

    [Fact]
    public void RefusedBelowTheCap()
    {
        var r = new EngineRig();
        r.Save.Level = 80;
        var refusal = r.Engine.Prestige(r.Now);
        Assert.Contains("level 100", refusal);
        Assert.Equal(80, r.Save.Level);
        Assert.Equal(0, r.Save.Prestige);
    }

    [Fact]
    public void ResetsLevelXpAndArousalAndKeepsTheRest()
    {
        var save = Capped();
        save.SeenPartners.Add("x@y");
        save.BondWith = "x@y";
        save.BondFormed = true;
        Assert.True(PrestigeRules.Apply(save, T0, b));
        Assert.Equal((1, 0.0, 0.0, 1), (save.Level, save.Xp, save.Arousal, save.Prestige));
        Assert.Equal(6, save.StreakDays);
        Assert.Contains("habit", save.Goals.Achievements);
        Assert.Single(save.SeenPartners);
        Assert.NotNull(save.Setup);
        Assert.True(save.BondFormed);
    }

    [Fact]
    public void EngineTellsThePlayer()
    {
        var r = new EngineRig();
        r.Save.Level = 100;
        Assert.Null(r.Engine.Prestige(r.Now));
        Assert.Equal(1, r.Save.Level);
        Assert.Contains(r.Host.Notes, n => n.Contains("prestiged"));
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(3, 1.15)]
    [InlineData(10, 1.5)]
    [InlineData(12, 1.5)]
    public void Factor(int stars, double expected) => Assert.Equal(expected, PrestigeRules.Factor(stars, b), 6);

    [Theory]
    [InlineData(0, "")]
    [InlineData(3, "★3")]
    [InlineData(10, "★10")]
    [InlineData(12, "★ Eternal 2")]
    public void Label(int stars, string expected) => Assert.Equal(expected, PrestigeRules.Label(stars));

    [Fact]
    public void FadingKeepsStars()
    {
        var save = new CharacterSave
        {
            Setup = new ProfileSetup { Decay = DecayMode.Harsh }, Level = 30, Prestige = 4,
            LastVerifiedUtc = T0, FadedThroughUtc = T0,
        };
        Progression.ApplyFading(save, T0.AddDays(30), b);
        Assert.Equal(4, save.Prestige);
    }

    [Fact]
    public void ReachingTheCapSaysPrestigeIsAvailable()
    {
        var r = new EngineRig();
        r.Save.Level = 99;
        r.Save.Xp = Progression.XpToNext(99, r.Balance) - 1;
        r.Run(11);
        Assert.Equal(100, r.Save.Level);
        Assert.Single(r.Host.Notes, n => n.Contains("prestige"));
        r.Run(20);
        Assert.Single(r.Host.Notes, n => n.Contains("prestige"));
    }
}

public class BonusFactorTests
{
    private readonly Balance b = new();

    private static EngineRig Plain()
    {
        var r = new EngineRig();
        r.Save.Goals.Achievements.Add("first_taste");
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        return r;
    }

    [Fact]
    public void BondedPartnerPays125()
    {
        var r = Plain();
        r.Save.BondWith = r.Bob.Key;
        r.Save.BondFormed = true;
        r.BobExtra = new ExtraTag(0, BondHashes.Of(r.SelfKey!));
        r.Run(11);
        Assert.Equal(1.25, r.Engine.Status.Factors.Bonded, 6);
        Assert.Equal(7.5, r.Save.Xp, 6);
    }

    [Fact]
    public void SomeoneElseIsNotBonded()
    {
        var r = Plain();
        r.Save.BondWith = "Carol C@W";
        r.Save.BondFormed = true;
        r.BobExtra = new ExtraTag(0, BondHashes.Of(r.SelfKey!));
        r.Run(11);
        Assert.Equal(1.0, r.Engine.Status.Factors.Bonded, 6);
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(5, 1.15)]
    [InlineData(10, 1.3)]
    [InlineData(40, 1.3)]
    public void Marked(int layers, double expected) => Assert.Equal(expected, Progression.MarkedFactor(layers, b), 6);

    [Fact]
    public void EngineUsesTheLayerCount()
    {
        var r = Plain();
        r.LayersWorn = 5;
        r.Run(11);
        Assert.Equal(1.15, r.Engine.Status.Factors.Marked, 6);
        r.LayersWorn = 0;
        r.Run(10);
        Assert.Equal(1.0, r.Engine.Status.Factors.Marked, 6);
    }

    [Fact]
    public void AllFactorsMultiply()
    {
        var f = Progression.Factors(1, null, 0, 1, 1, b, bonded: true, layersWorn: 5, stars: 3);
        Assert.Equal(1.25 * 1.15 * 1.15, f.Total, 6);
    }
}

public class ExtraTagTests
{
    [Theory]
    [InlineData(0, null)]
    [InlineData(12, "a1b2c3d4")]
    public void RoundTrips(int stars, string? hash)
    {
        var tag = new ExtraTag(stars, hash);
        Assert.Equal(tag, ExtraTag.Parse(tag.Format()));
    }

    [Fact] public void Format() => Assert.Equal("E1|3|-", new ExtraTag(3, null).Format());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("E1|3")]
    [InlineData("E2|3|-")]
    [InlineData("E1|-1|-")]
    [InlineData("E1|3|ABCDEF12")]
    [InlineData("E1|3|abc")]
    [InlineData("E1|3|-|x")]
    [InlineData("C1|-|1|0|0|0")]
    public void Malformed(string? text) => Assert.Null(ExtraTag.Parse(text));

    [Fact]
    public void FirstTagUnchanged()
    {
        var r = new EngineRig { Playing = false };
        r.Save.Prestige = 3;
        r.Run(1);
        Assert.Equal("C1|-|1|0|0|0", r.Host.Tags[^1]);
        Assert.Equal("E1|3|-", r.Host.ExtraTags[^1]);
    }

    [Fact]
    public void WrittenOnlyOnChange()
    {
        var r = new EngineRig { Playing = false };
        r.Run(30);
        Assert.Single(r.Host.ExtraTags);
        r.Save.Prestige = 1;
        r.Run(1);
        Assert.Equal(2, r.Host.ExtraTags.Count);
    }

    [Fact]
    public void BothRemovedWhenHidden()
    {
        var r = new EngineRig { Playing = false };
        r.Run(1);
        r.Engine.SetHidden(true);
        r.Run(1);
        Assert.Null(r.Host.Tags[^1]);
        Assert.Null(r.Host.ExtraTags[^1]);
    }

    [Fact]
    public void NeitherBeforeSetup()
    {
        var r = new EngineRig { Playing = false };
        r.Save.Setup = null;
        r.Run(5);
        Assert.Empty(r.Host.Tags);
        Assert.Empty(r.Host.ExtraTags);
    }

    [Fact]
    public void StarsReachThePlate()
    {
        var r = new EngineRig { BobExtra = new ExtraTag(3, null) };
        r.Run(1);
        Assert.Equal(3, r.Engine.PlateOf(r.Bob)!.Value.Prestige);
    }

    [Fact]
    public void HashIsShortStableAndCaseBlind()
    {
        var hash = BondHashes.Of("Bob B@W");
        Assert.True(BondHashes.IsHash(hash));
        Assert.Equal(hash, BondHashes.Of("bob b@w"));
        Assert.NotEqual(hash, BondHashes.Of("Carol C@W"));
    }
}

public class BondTests
{
    private const string Me = "Alice A@W";
    private static readonly PeerRecord Bob = new() { Name = "Bob B", World = "W" };
    private static readonly PeerRecord Carol = new() { Name = "Carol C", World = "W" };

    private static (PeerRecord, ExtraTag?) Naming(PeerRecord who, string? target) => (who, new ExtraTag(0, target is null ? null : BondHashes.Of(target)));

    private static (CharacterSave, BondBook) Book()
    {
        var save = new CharacterSave { Level = 40 };
        return (save, new BondBook(save));
    }

    [Fact]
    public void RequestingNamesThem()
    {
        var (save, book) = Book();
        Assert.Null(book.Request(Bob.Key, 40, 40));
        Assert.Equal(Bob.Key, save.BondWith);
        Assert.False(save.BondFormed);
        Assert.Equal(BondHashes.Of(Bob.Key), book.OwnTagHash);
        Assert.True(book.IsRequesting(Bob.Key));
    }

    [Fact]
    public void FormsWhenTheyNameMe()
    {
        var (save, book) = Book();
        book.Request(Bob.Key, 40, 40);
        var news = book.Observe(Me, [Naming(Bob, Me)]);
        Assert.True(save.BondFormed);
        Assert.Contains("bonded", news);
        Assert.True(book.PaysWith(Bob.Key));
    }

    [Fact]
    public void TheirRequestShowsAsWantsToBond()
    {
        var (save, book) = Book();
        Assert.Null(book.Observe(Me, [Naming(Bob, Me)]));
        Assert.Contains(Bob.Key, book.WantsToBond);
        Assert.Null(save.BondWith);
    }

    [Fact]
    public void EndsWhenTheyNameNobody()
    {
        var (save, book) = Book();
        save.BondWith = Bob.Key;
        save.BondFormed = true;
        var news = book.Observe(Me, [Naming(Bob, null)]);
        Assert.Null(save.BondWith);
        Assert.False(save.BondFormed);
        Assert.Contains("ended", news);
    }

    [Fact]
    public void EndsWhenTheyNameSomeoneElse()
    {
        var (save, book) = Book();
        save.BondWith = Bob.Key;
        save.BondFormed = true;
        book.Observe(Me, [Naming(Bob, Carol.Key)]);
        Assert.Null(save.BondWith);
    }

    [Fact]
    public void WaitingRequestSurvivesThemNamingSomeoneElse()
    {
        var (save, book) = Book();
        book.Request(Bob.Key, 40, 40);
        Assert.Null(book.Observe(Me, [Naming(Bob, Carol.Key)]));
        Assert.Equal(Bob.Key, save.BondWith);
        Assert.False(save.BondFormed);
    }

    [Fact]
    public void OldClientsWithoutExtraTagChangeNothing()
    {
        var (save, book) = Book();
        save.BondWith = Bob.Key;
        save.BondFormed = true;
        book.Observe(Me, [(Bob, null)]);
        Assert.True(save.BondFormed);
        Assert.False(book.PaysWith(Bob.Key));
    }

    [Theory]
    [InlineData(39, 40, "Bonding opens")]
    [InlineData(40, 39, "They need")]
    public void TaintedRule(int mine, int theirs, string reason)
    {
        var (save, book) = Book();
        Assert.Contains(reason, book.Request(Bob.Key, mine, theirs));
        Assert.Null(save.BondWith);
    }

    [Fact]
    public void AskingSomeoneElseReplacesTheRequest()
    {
        var (save, book) = Book();
        book.Request(Bob.Key, 40, 40);
        book.Request(Carol.Key, 40, 40);
        Assert.Equal(Carol.Key, save.BondWith);
    }

    [Fact]
    public void FormedBondHasToEndFirst()
    {
        var (save, book) = Book();
        save.BondWith = Bob.Key;
        save.BondFormed = true;
        Assert.Contains("Unbond", book.Request(Carol.Key, 40, 40));
        Assert.Equal(Bob.Key, save.BondWith);
    }

    [Fact]
    public void BondFormsBetweenTwoEngines()
    {
        var r = new EngineRig { Playing = false };
        r.Save.Level = 40;
        r.BobTag = r.BobTag with { Level = 40 };
        r.Run(1);
        Assert.Null(r.Engine.RequestBond(r.Bob));
        r.Run(1);
        Assert.Equal($"E1|0|{BondHashes.Of(r.Bob.Key)}", r.Host.ExtraTags[^1]);
        r.BobExtra = new ExtraTag(0, BondHashes.Of(r.SelfKey!));
        r.Run(1);
        Assert.True(r.Save.BondFormed);
        r.Engine.Unbond();
        r.Run(1);
        Assert.Equal("E1|0|-", r.Host.ExtraTags[^1]);
    }
}

public class ProfileTimerTests
{
    private static readonly DateTime T0 = EngineRig.T0;

    private static CharacterSave Played(DecayMode decay, DateTime last) => new()
    {
        Setup = new ProfileSetup { Decay = decay }, LastVerifiedUtc = last, FadedThroughUtc = last,
        Goals = { Stats = { [nameof(GoalKind.SceneSeconds)] = 600 } },
    };

    [Fact]
    public void RecentlyPlayedOnNormal()
    {
        var t = ProfileTimers.Compute(Played(DecayMode.Normal, T0), T0.AddHours(2));
        Assert.Equal(TimeSpan.FromHours(2), t.SinceLastScene);
        Assert.Equal(TimeSpan.FromHours(70), t.UntilDecay);
        Assert.False(t.Fading);
    }

    [Theory]
    [InlineData(DecayMode.Gentle, 167)]
    [InlineData(DecayMode.Normal, 71)]
    [InlineData(DecayMode.Harsh, 23)]
    public void UntilDecayPerMode(DecayMode mode, int hours)
        => Assert.Equal(TimeSpan.FromHours(hours), ProfileTimers.Compute(Played(mode, T0), T0.AddHours(1)).UntilDecay);

    [Fact]
    public void FadingShowsTheNextFade()
    {
        // Normal: fading from 72h, so 4 days and 2 hours away is 1 day and 2 hours into fading.
        var t = ProfileTimers.Compute(Played(DecayMode.Normal, T0), T0.AddDays(4).AddHours(2));
        Assert.True(t.Fading);
        Assert.Null(t.UntilDecay);
        Assert.Equal(TimeSpan.FromHours(22), t.UntilNextFade);
    }

    [Fact]
    public void FadingAfterFadeApplied()
    {
        var save = Played(DecayMode.Normal, T0);
        save.Level = 30;
        Progression.ApplyFading(save, T0.AddDays(4).AddHours(2), new Balance());
        Assert.Equal(TimeSpan.FromHours(22), ProfileTimers.Compute(save, T0.AddDays(4).AddHours(2)).UntilNextFade);
    }

    [Fact]
    public void DecayOff()
    {
        var t = ProfileTimers.Compute(Played(DecayMode.Off, T0), T0.AddDays(30));
        Assert.True(t.DecayOff);
        Assert.Null(t.UntilDecay);
        Assert.Null(t.UntilNextFade);
        Assert.Equal(TimeSpan.FromDays(30), t.SinceLastScene);
    }

    [Fact]
    public void NeverPlayed()
    {
        var save = Played(DecayMode.Normal, T0);
        save.Goals.Stats.Clear();
        var t = ProfileTimers.Compute(save, T0.AddHours(1));
        Assert.True(t.NeverPlayed);
        Assert.Null(t.SinceLastScene);
    }

    [Fact]
    public void Status()
    {
        var save = Played(DecayMode.Normal, T0);
        Assert.Equal(ProfileStatus.Earning, ProfileTimers.StatusOf(save, SceneState.Earning, T0));
        Assert.Equal(ProfileStatus.Fading, ProfileTimers.StatusOf(save, SceneState.Idle, T0.AddDays(4)));
        save.Paused = true;
        Assert.Equal(ProfileStatus.Paused, ProfileTimers.StatusOf(save, SceneState.Paused, T0));
        save.Hidden = true;
        Assert.Equal(ProfileStatus.Hidden, ProfileTimers.StatusOf(save, SceneState.Paused, T0));
    }

    [Theory]
    [InlineData(45, "45s")]
    [InlineData(125, "2m 5s")]
    [InlineData(3 * 3600 + 720, "3h 12m")]
    [InlineData(2 * 86400 + 4 * 3600, "2d 4h")]
    public void Describe(int seconds, string expected) => Assert.Equal(expected, ProfileTimers.Describe(TimeSpan.FromSeconds(seconds)));
}

public class ToyControllerTests
{
    private static readonly DateTime T0 = EngineRig.T0;

    [Fact]
    public void FollowsArousal()
    {
        var toy = new ToyController();
        Assert.Equal(0.6, toy.Update(T0, true, 60, 1, null)!.Value, 6);
        Assert.Equal(0.3, toy.Update(T0.AddSeconds(1), true, 60, 0.5, null)!.Value, 6);
    }

    [Fact]
    public void ClimaxBurstsForThreeSeconds()
    {
        var toy = new ToyController();
        toy.Update(T0, true, 50, 0.8, null);
        Assert.Equal(0.8, toy.Update(T0.AddSeconds(0.2), true, 20, 0.8, T0.AddSeconds(0.2))!.Value, 6);
        Assert.Null(toy.Update(T0.AddSeconds(2.9), true, 20, 0.8, T0.AddSeconds(0.2)));
        Assert.Equal(0.16, toy.Update(T0.AddSeconds(3.3), true, 20, 0.8, T0.AddSeconds(0.2))!.Value, 6);
    }

    [Fact]
    public void StopsWhenTheSceneEnds()
    {
        var toy = new ToyController();
        toy.Update(T0, true, 50, 1, null);
        Assert.Equal(0, toy.Update(T0.AddSeconds(0.1), false, 50, 1, null));
    }

    [Fact]
    public void SmallChangesAndFastChangesWait()
    {
        var toy = new ToyController();
        toy.Update(T0, true, 50, 1, null);
        Assert.Null(toy.Update(T0.AddSeconds(2), true, 51, 1, null));
        var toy2 = new ToyController();
        toy2.Update(T0, true, 50, 1, null);
        Assert.Null(toy2.Update(T0.AddSeconds(0.5), true, 60, 1, null));
        Assert.Equal(0.6, toy2.Update(T0.AddSeconds(1), true, 60, 1, null)!.Value, 6);
    }

    [Fact]
    public void NothingWhenUnchanged()
    {
        var toy = new ToyController();
        Assert.Null(toy.Update(T0, false, 0, 1, null));
    }
}

public class BridgePlanTests
{
    private static MoodlesInput Input(double arousal = 0) => new(true, arousal, 0, false, 0, false, false, false);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(20, 1)]
    [InlineData(30, 2)]
    [InlineData(45, 3)]
    [InlineData(100, 5)]
    public void ArousedSteps(double arousal, int step) => Assert.Equal(step, MoodlesPlan.Plan(Input(arousal))[MoodleKind.Aroused]);

    [Fact] public void NothingAtZero() => Assert.Empty(MoodlesPlan.Plan(Input()));

    [Fact]
    public void EverythingThatApplies()
    {
        var plan = MoodlesPlan.Plan(new MoodlesInput(true, 50, 4, true, 2, true, true, true));
        Assert.Equal(6, plan.Count);
        Assert.Equal(4, plan[MoodleKind.Marked]);
    }

    [Fact]
    public void WatchedAndBondedOnlyInAScene()
        => Assert.Empty(MoodlesPlan.Plan(new MoodlesInput(true, 0, 0, false, 3, true, false, false)));

    [Fact] public void HiddenShowsNothing() => Assert.Empty(MoodlesPlan.Plan(Input(80) with { Visible = false }));

    private static CharacterSave WithVisuals(int level)
    {
        var save = new CharacterSave { Setup = new ProfileSetup(), Level = level };
        save.TierVisuals["Curious"] = new TierVisual { Kind = VisualKind.Glamourer, DesignId = Guid.NewGuid(), Name = "c" };
        save.TierVisuals["Tempted"] = new TierVisual { Kind = VisualKind.Penumbra, ModDirectory = "m", Options = { ["g"] = "o" } };
        return save;
    }

    [Fact]
    public void TierChangesTheVisual()
    {
        Assert.Equal("c", TierVisualPlan.Wanted(WithVisuals(12), false)!.Name);
        Assert.Equal("m", TierVisualPlan.Wanted(WithVisuals(30), false)!.ModDirectory);
        Assert.Null(TierVisualPlan.Wanted(WithVisuals(45), false));
    }

    [Fact]
    public void PrestigeGoesBackToPure()
    {
        var save = WithVisuals(100);
        save.TierVisuals["Fallen"] = new TierVisual { Name = "f" };
        Assert.NotNull(TierVisualPlan.Wanted(save, false));
        PrestigeRules.Apply(save, EngineRig.T0, new Balance());
        Assert.Null(TierVisualPlan.Wanted(save, false));
    }

    [Fact]
    public void PausedRemovesOnlyWhenChosen()
    {
        var save = WithVisuals(12);
        save.Paused = true;
        Assert.NotNull(TierVisualPlan.Wanted(save, false));
        Assert.Null(TierVisualPlan.Wanted(save, true));
    }

    [Fact]
    public void SameAs()
    {
        var a = new TierVisual { Kind = VisualKind.Penumbra, ModDirectory = "m", Options = { ["g"] = "o" } };
        Assert.True(a.SameAs(new TierVisual { Kind = VisualKind.Penumbra, ModDirectory = "m", Options = { ["g"] = "o" } }));
        Assert.False(a.SameAs(new TierVisual { Kind = VisualKind.Penumbra, ModDirectory = "m", Options = { ["g"] = "p" } }));
    }
}

public class PrestigeExtrasTests
{
    [Fact]
    public void PrestigeToken()
    {
        Assert.Equal("Lv. 4 ★3", PlateComposer.Render("Lv. {level} {prestige}", new PlateValues(4, 0, 3)));
        Assert.Equal("Lv. 4 ", PlateComposer.Render("Lv. {level} {prestige}", new PlateValues(4, 0)));
        Assert.Empty(PlateComposer.UnknownTokens("{prestige}"));
    }

    [Fact]
    public void FallenStarAtFive()
    {
        var save = new CharacterSave { Setup = new ProfileSetup(), Prestige = 4 };
        var book = new GoalBook(save);
        Assert.DoesNotContain(book.TakeRewards(), r => r.Title == "Fallen Star");
        save.Prestige = 5;
        Assert.Contains(book.TakeRewards(), r => r.Title == "Fallen Star");
    }

    [Fact]
    public void ResetClearsStarsAndBondAndRemovesTheVisual()
    {
        var save = new CharacterSave { Setup = new ProfileSetup(), Level = 50, Prestige = 3, BondWith = "b@w", BondFormed = true };
        save.TierVisuals["Tainted"] = new TierVisual { Name = "t" };
        save.ResetProgress(EngineRig.T0);
        Assert.Equal(0, save.Prestige);
        Assert.Null(save.BondWith);
        Assert.False(save.BondFormed);
        Assert.Null(TierVisualPlan.Wanted(save, false));
    }
}
