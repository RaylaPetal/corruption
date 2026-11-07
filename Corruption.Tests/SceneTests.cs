using System.Numerics;
using Corruption.Core;

namespace Corruption.Tests;

public class SceneCheckTests
{
    private static readonly PoseKey Pose = new(50, 0);
    private static readonly SceneInput Good = new(false, true, true, 1, Pose, Pose, false);

    private static SceneFailure Eval(SceneInput input) => SceneCheck.Evaluate(input, 3);

    [Fact] public void AllConditionsPass() => Assert.Equal(SceneFailure.None, Eval(Good));
    [Fact] public void ExactlyThreeYalmsPasses() => Assert.Equal(SceneFailure.None, Eval(Good with { Distance = 3 }));
    [Fact] public void NeedsSetupComesFirst() => Assert.Equal(SceneFailure.NeedsSetup, Eval(Good with { NeedsSetup = true, Paused = true }));
    [Fact] public void Paused() => Assert.Equal(SceneFailure.Paused, Eval(Good with { Paused = true }));
    [Fact] public void NoKnownAct() => Assert.Equal(SceneFailure.NoKnownAct, Eval(Good with { HasKnownAct = false }));
    [Fact] public void NoPartner() => Assert.Equal(SceneFailure.NoPartner, Eval(Good with { PartnerLoaded = false }));
    [Fact] public void TooFar() => Assert.Equal(SceneFailure.PartnerTooFar, Eval(Good with { Distance = 3.01 }));
    [Fact] public void NoDeclaration() => Assert.Equal(SceneFailure.NoPartnerDeclaration, Eval(Good with { Declared = null }));
    [Fact] public void NotLooping() => Assert.Equal(SceneFailure.PartnerEmoteMismatch, Eval(Good with { Observed = null }));
    [Fact] public void DifferentEmote() => Assert.Equal(SceneFailure.PartnerEmoteMismatch, Eval(Good with { Observed = new PoseKey(51, 0) }));
    [Fact] public void Afk() => Assert.Equal(SceneFailure.Afk, Eval(Good with { Afk = true }));
}

public class SceneTickerTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static int Run(SceneTicker t, int from, int to, Func<int, bool>? pass = null, string act = "a")
    {
        var ticks = 0;
        for (var s = from; s < to; s++)
            if (t.Sample(T0.AddSeconds(s), pass?.Invoke(s) ?? true, "p", act, 10)) ticks++;
        return ticks;
    }

    [Fact]
    public void TicksEveryTenSeconds() => Assert.Equal(6, Run(new SceneTicker(), 0, 61));

    [Fact]
    public void FailedSampleVoidsOnlyTheCurrentTick()
    {
        var t = new SceneTicker();
        Assert.Equal(0, Run(t, 0, 5));
        Assert.Equal(0, Run(t, 5, 6, _ => false));
        Assert.Equal(0, Run(t, 6, 16));
        Assert.Equal(1, Run(t, 16, 17));
    }

    [Fact]
    public void ChangingActStartsANewTick()
    {
        var t = new SceneTicker();
        Run(t, 0, 9);
        Assert.Equal(0, Run(t, 9, 18, act: "b"));
        Assert.Equal(1, Run(t, 18, 20, act: "b"));
    }
}

public class AfkTrackerTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly PoseKey Pose = new(50, 0);

    [Fact]
    public void FlaggedAfterFifteenMinutesWithoutChange()
    {
        var afk = new AfkTracker();
        afk.Update(T0, Pose, Vector3.Zero);
        afk.Update(T0.AddMinutes(14.9), Pose, Vector3.Zero);
        Assert.False(afk.IsAfk(T0.AddMinutes(14.9), 15));
        afk.Update(T0.AddMinutes(15), Pose, Vector3.Zero);
        Assert.True(afk.IsAfk(T0.AddMinutes(15), 15));
    }

    [Fact]
    public void ChangingEmoteOrMovingClearsIt()
    {
        var afk = new AfkTracker();
        afk.Update(T0, Pose, Vector3.Zero);
        afk.Update(T0.AddMinutes(20), new PoseKey(51, 0), Vector3.Zero);
        Assert.False(afk.IsAfk(T0.AddMinutes(20), 15));
        afk.Update(T0.AddMinutes(40), new PoseKey(51, 0), new Vector3(1, 0, 0));
        Assert.False(afk.IsAfk(T0.AddMinutes(40), 15));
    }
}

public class FakeHost : IEngineHost
{
    public List<string?> Tags { get; } = [];
    public List<(string To, string Part)> Layers { get; } = [];
    public List<string> Notes { get; } = [];
    public string? LayerOutcome { get; set; } = "sent";
    public bool TagsWork { get; set; } = true;

    /// Outcomes to give before falling back to LayerOutcome.
    public Queue<string> Outcomes { get; } = new();

    public int Saves { get; private set; }

    public bool PublishTag(string? value)
    {
        if (TagsWork) Tags.Add(value);
        return TagsWork;
    }

    public List<string?> ExtraTags { get; } = [];

    public bool PublishExtraTag(string? value)
    {
        if (TagsWork) ExtraTags.Add(value);
        return TagsWork;
    }

    public string? SendLayer(PeerRecord to, string partId)
    {
        Layers.Add((to.Key, partId));
        return Outcomes.Count > 0 ? Outcomes.Dequeue() : LayerOutcome;
    }

    public void Notify(string text) => Notes.Add(text);

    public List<GoalReward> Celebrated { get; } = [];

    public void Celebrate(GoalReward reward) => Celebrated.Add(reward);

    public void SaveProgress() => Saves++;

    public PresenceTag? LastTag => Tags.Count > 0 ? PresenceTag.Parse(Tags[^1]) : null;
}

/// Drives a real engine through its public surface, one simulated second at a time.
public sealed class EngineRig
{
    public static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public readonly PeerRecord Bob = new() { Name = "Bob B", World = "W" };
    public static readonly PoseKey BobPose = new(51, 0);
    public readonly CharacterSave Save = new() { ArousalAnchorUtc = T0, Setup = new ProfileSetup { Decay = DecayMode.Off } };
    public readonly Balance Balance = new();
    public readonly FakeHost Host = new();
    public readonly SceneEngine Engine;
    public ActEntry Act = new() { ModDirectory = "m", Option = "o", EmoteMode = 50 };
    public int Watchers;
    public DateTime Now = T0;
    public double Distance = 1;
    public bool BobVisible = true;
    public bool Playing = true;
    public PoseKey? BobSeenPose = BobPose;
    public PresenceTag BobTag = new(BobPose, 1, 0, 0, false);
    public ExtraTag? BobExtra;
    public int LayersWorn;
    public string? SelfKey = "Alice A@W";

    public EngineRig(Random? random = null)
    {
        // Periods already rolled with no goals in them, so a random pick can't change a test's XP.
        Save.Goals.Seed = 1;
        Save.Goals.DailyKey = T0.ToString("yyyy-MM-dd");
        Save.Goals.WeeklyKey = GoalBook.StartOfWeek(T0).ToString("yyyy-MM-dd");
        Engine = new SceneEngine(Save, Balance, Host, random);
    }

    public void Run(int seconds)
    {
        for (var i = 0; i < seconds; i++)
        {
            // Move a little so long runs aren't AFK.
            var position = new Vector3((Now - T0).Minutes % 2, 0, 0);
            Engine.Sample(Now, new SceneSample(Playing ? Act : null, Playing ? Act.Pose : null, position,
                BobVisible ? [new VisiblePlayer(Bob, Distance, BobSeenPose, BobTag, BobExtra)] : [], Watchers, LayersWorn, SelfKey));
            Now = Now.AddSeconds(1);
        }
    }

    public void BobClimaxes()
    {
        BobTag = BobTag with { ClimaxCount = BobTag.ClimaxCount + 1 };
        Run(1);
    }
}

public class SceneEngineTests
{
    private static EngineRig Plain(EngineRig? rig = null)
    {
        var r = rig ?? new EngineRig();
        r.Save.Goals.Achievements.Add("first_taste");
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        return r;
    }

    [Fact]
    public void VerifiedTickAwardsXpAndArousal()
    {
        var r = Plain();
        r.Run(11);
        Assert.Equal(6, r.Save.Xp, 6);
        Assert.Equal(3, r.Save.Arousal, 6);
        Assert.Equal(SceneState.Earning, r.Engine.Status.State);
    }

    [Fact]
    public void NoPairingIsNeededAndDifferentAnimationsEarn()
    {
        var r = new EngineRig();
        r.Run(11);
        Assert.NotEqual(r.Act.Pose, EngineRig.BobPose);
        Assert.True(r.Save.Xp > 0);
        Assert.Equal(r.Bob.Key, r.Engine.Status.Partner!.Key);
    }

    [Fact]
    public void FirstTimeBonusesArePaidOnce()
    {
        var r = new EngineRig();
        r.Save.Goals.Achievements.Add("first_taste");
        r.Run(21);
        Assert.Equal(6 + 6 + 200 + 100, TotalXp(r), 6);
    }

    private static double TotalXp(EngineRig r)
    {
        var total = r.Save.Xp;
        for (var level = 1; level < r.Save.Level; level++) total += Progression.XpToNext(level, r.Balance);
        return total;
    }

    [Fact]
    public void AloneEarnsNothing()
    {
        var r = new EngineRig { BobVisible = false };
        r.Run(120);
        Assert.Equal(0, r.Save.Xp);
        Assert.Equal(0, r.Save.Arousal);
        Assert.Equal(SceneFailure.NoPartner, r.Engine.Status.Failure);
    }

    [Fact]
    public void TooFarEarnsNothing()
    {
        var r = new EngineRig { Distance = 4 };
        r.Run(60);
        Assert.Equal(0, r.Save.Xp);
        Assert.Equal(SceneFailure.PartnerTooFar, r.Engine.Status.Failure);
        Assert.Equal(SceneState.Waiting, r.Engine.Status.State);
    }

    [Fact]
    public void NotPlayingAKnownActEarnsNothing()
    {
        var r = new EngineRig { Playing = false };
        r.Run(60);
        Assert.Equal(0, r.Save.Xp);
        Assert.Equal(SceneState.Idle, r.Engine.Status.State);
    }

    [Fact]
    public void ThemNotBeingInAnActEarnsNothing()
    {
        var r = new EngineRig();
        r.BobTag = r.BobTag with { Pose = null };
        r.Run(30);
        Assert.Equal(0, r.Save.Xp);
        Assert.Equal(SceneFailure.NoPartnerDeclaration, r.Engine.Status.Failure);
    }

    [Fact]
    public void TheirActEndingStopsTheScene()
    {
        var r = new EngineRig();
        r.Run(11);
        r.BobTag = r.BobTag with { Pose = null };
        var before = r.Save.Xp;
        r.Run(30);
        Assert.Equal(before, r.Save.Xp);
    }

    [Fact]
    public void WhatTheyDeclareMustMatchWhatIsSeen()
    {
        var r = new EngineRig { BobSeenPose = new PoseKey(99, 0) };
        r.Run(30);
        Assert.Equal(0, r.Save.Xp);
        Assert.Equal(SceneFailure.PartnerEmoteMismatch, r.Engine.Status.Failure);
    }

    [Fact]
    public void ThePartnerIsSomeoneInAnActBeforeSomeoneCloserWhoIsNot()
    {
        var r = new EngineRig();
        var idle = new PeerRecord { Name = "Idle I", World = "W" };
        for (var i = 0; i < 11; i++)
        {
            r.Engine.Sample(r.Now, new SceneSample(r.Act, r.Act.Pose, new Vector3(i, 0, 0),
                [new VisiblePlayer(idle, 0.5, null, new PresenceTag(null, 1, 0, 0, false)), new VisiblePlayer(r.Bob, 2, EngineRig.BobPose, r.BobTag)]));
            r.Now = r.Now.AddSeconds(1);
        }
        Assert.Equal(r.Bob.Key, r.Engine.Status.Partner!.Key);
        Assert.True(r.Save.Xp > 0);
    }

    [Fact]
    public void AfkStopsEarning()
    {
        var r = new EngineRig();
        for (var i = 0; i < 16 * 60; i++)
        {
            r.Engine.Sample(r.Now, new SceneSample(r.Act, r.Act.Pose, Vector3.Zero, [new VisiblePlayer(r.Bob, 1, EngineRig.BobPose, r.BobTag)]));
            r.Now = r.Now.AddSeconds(1);
        }
        Assert.Equal(SceneFailure.Afk, r.Engine.Status.Failure);
    }

    [Fact]
    public void FatigueLowersTickXpAfterTheGrace()
    {
        var r = Plain();
        r.Run(12 * 60 + 5);
        Assert.Equal(0.8, r.Engine.Status.Fatigue, 6);
    }

    // ── The tag ────────────────────────────────────────────────────────────────

    [Fact]
    public void TagSaysWhatThisCharacterIsDoing()
    {
        var r = new EngineRig();
        r.Save.Level = 12;
        r.Run(5);
        Assert.Equal(r.Act.Pose, r.Host.LastTag!.Value.Pose);
        Assert.Equal(12, r.Host.LastTag!.Value.Level);
    }

    [Fact]
    public void TagGoesOutEvenAloneSoOthersCanFindThisCharacter()
    {
        var r = new EngineRig { BobVisible = false, Playing = false };
        r.Run(1);
        Assert.NotNull(r.Host.LastTag);
        Assert.Null(r.Host.LastTag!.Value.Pose);
    }

    [Fact]
    public void AnUnchangingSceneRarelyRewritesTheTag()
    {
        var r = Plain();
        r.Save.Setup!.ArousalGain = 0.25;
        r.Run(300);
        Assert.InRange(r.Host.Tags.Count, 2, 8);
    }

    [Fact]
    public void ATagThatCouldNotBeWrittenIsTriedAgain()
    {
        var r = new EngineRig { Host = { TagsWork = false } };
        r.Run(2);
        r.Host.TagsWork = true;
        r.Run(1);
        Assert.NotNull(r.Host.LastTag);
    }

    [Fact]
    public void RepublishingWritesItAgain()
    {
        var r = new EngineRig();
        r.Run(10);
        var count = r.Host.Tags.Count;
        r.Engine.RepublishTag();
        r.Run(1);
        Assert.Equal(count + 1, r.Host.Tags.Count);
    }

    [Fact]
    public void ClimaxGoesOutInTheTagAtOnce()
    {
        var r = new EngineRig();
        r.Save.Arousal = 98;
        r.Run(11);
        Assert.Equal(1, r.Host.LastTag!.Value.ClimaxCount);
    }

    // ── Safeword ───────────────────────────────────────────────────────────────

    [Fact]
    public void SafewordStopsEverythingAtOnce()
    {
        var r = new EngineRig();
        r.Run(5);
        r.Engine.Pause();
        Assert.Equal(SceneState.Paused, r.Engine.Status.State);
        var xp = r.Save.Xp;
        r.Run(120);
        Assert.Equal(xp, r.Save.Xp);
        Assert.Equal(0, r.Save.Arousal);
        Assert.Equal(SceneState.Paused, r.Engine.Status.State);
        Assert.True(r.Host.LastTag!.Value.Paused);
        Assert.Null(r.Host.LastTag!.Value.Pose);
    }

    [Fact]
    public void SafewordWorksWithNobodyInReach()
    {
        var r = new EngineRig { BobVisible = false };
        r.Run(2);
        r.Engine.Pause();
        Assert.True(r.Engine.IsPaused);
    }

    [Fact]
    public void SafewordCancelsAQueuedLayer()
    {
        var r = new EngineRig();
        r.Save.Arousal = 80;
        r.Run(6);
        Assert.Null(r.Engine.TryManualClimax(r.Now));
        r.Engine.Pause();
        r.Run(10);
        Assert.Empty(r.Host.Layers);
    }

    [Fact]
    public void ResumeEarnsAgain()
    {
        var r = Plain();
        r.Engine.Pause();
        r.Run(5);
        r.Engine.Resume();
        r.Run(15);
        Assert.True(r.Save.Xp > 0);
        Assert.False(r.Host.LastTag!.Value.Paused);
    }

    [Fact]
    public void PauseSurvivesReload()
    {
        var r = new EngineRig();
        r.Engine.Pause();
        Assert.True(new SceneEngine(r.Save, r.Balance, r.Host).IsPaused);
    }

    [Fact]
    public void TheirSafewordStopsTheScene()
    {
        var r = new EngineRig();
        r.Run(5);
        r.BobTag = r.BobTag with { Pose = null, Paused = true };
        r.Run(60);
        Assert.Equal(0, r.Save.Xp);
        Assert.Equal(SceneFailure.NoPartnerDeclaration, r.Engine.Status.Failure);
        r.BobTag = r.BobTag with { Pose = EngineRig.BobPose, Paused = false };
        r.Run(11);
        Assert.True(r.Save.Xp > 0);
    }

    // ── Climax ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ArousalReachingOneHundredFiresOnce()
    {
        var r = Plain();
        r.Save.Arousal = 98;
        r.Run(11);
        Assert.Equal(1, r.Host.LastTag!.Value.ClimaxCount);
        Assert.Equal(20, r.Save.Arousal, 6);
        Assert.Equal(6 + 150, r.Save.Xp + (r.Save.Level > 1 ? Progression.XpToNext(1, r.Balance) : 0), 6);
        r.Run(50);
        Assert.Equal(1, r.Host.LastTag!.Value.ClimaxCount);
    }

    [Fact]
    public void LayerGoesOutAfterTheClimaxOnARandomPart()
    {
        var r = new EngineRig();
        r.Save.Arousal = 98;
        r.Run(11);
        Assert.Empty(r.Host.Layers);
        r.Run(3);
        var layer = Assert.Single(r.Host.Layers);
        Assert.Equal(r.Bob.Key, layer.To);
        Assert.Contains(layer.Part, CummyParts.All);
    }

    [Fact]
    public void BlockedPartsAreSkippedForAnother()
    {
        var r = new EngineRig();
        r.Host.Outcomes.Enqueue("blocked");
        r.Host.Outcomes.Enqueue("blocked");
        r.Save.Arousal = 98;
        r.Run(14);
        Assert.Equal(3, r.Host.Layers.Count);
        Assert.Equal(3, r.Host.Layers.Select(l => l.Part).Distinct().Count());
    }

    [Fact]
    public void EveryPartBlockedSendsNothingAndStops()
    {
        var r = new EngineRig { Host = { LayerOutcome = "blocked" } };
        r.Save.Arousal = 98;
        r.Run(20);
        Assert.Equal(CummyParts.All.Count, r.Host.Layers.Count);
    }

    [Fact]
    public void NotBeingPairedInCummyMeansNoLayerAndNoRetry()
    {
        var r = new EngineRig { Host = { LayerOutcome = "not-paired" } };
        r.Save.Arousal = 98;
        r.Run(14);
        Assert.Single(r.Host.Layers);
    }

    [Fact]
    public void RandomLayersReachDifferentParts()
    {
        var seen = new HashSet<string>();
        for (var seed = 0; seed < 40; seed++)
        {
            var r = new EngineRig(new Random(seed));
            r.Save.Arousal = 98;
            r.Run(14);
            seen.Add(r.Host.Layers.Single().Part);
        }
        Assert.True(seen.Count >= 8);
    }

    [Fact]
    public void WatchersRaiseXpAndArousal()
    {
        var r = Plain(new EngineRig { Watchers = 2 });
        r.Run(11);
        Assert.Equal(7.2, r.Save.Xp, 6);
        Assert.Equal(3.6, r.Save.Arousal, 6);
        Assert.Equal(2, r.Engine.Status.Watchers);
    }

    [Fact]
    public void AHigherTierPartnerRaisesXp()
    {
        var r = Plain();
        r.BobTag = r.BobTag with { Level = 30 };
        r.Run(11);
        Assert.Equal(7.2, r.Save.Xp, 6);
    }

    [Fact]
    public void VerifiedTickCountsTheStreak()
    {
        var r = new EngineRig();
        r.Run(11);
        Assert.Equal(1, r.Save.StreakDays);
    }

    [Theory]
    [InlineData("sent", 75)]
    [InlineData("not-paired", 0)]
    [InlineData("out-of-range", 0)]
    [InlineData("queue-full", 0)]
    [InlineData(null, 0)]
    public void GiverLayerXpOnlyWhenSent(string? outcome, double expected)
    {
        var r = Plain(new EngineRig { Host = { LayerOutcome = outcome } });
        r.Save.Level = 50;
        r.Save.Arousal = 98;
        r.Run(11);
        var afterClimax = r.Save.Xp;
        r.Run(3);
        Assert.Equal(expected, r.Save.Xp - afterClimax, 6);
    }

    [Fact]
    public void ManualClimaxRefusals()
    {
        var r = new EngineRig { Playing = false };
        r.Save.Arousal = 80;
        r.Run(1);
        Assert.Contains("no verified scene", r.Engine.TryManualClimax(r.Now));

        r.Playing = true;
        r.Save.Arousal = 50;
        r.Run(1);
        Assert.Contains("at least 70", r.Engine.TryManualClimax(r.Now));

        r.Save.Arousal = 80;
        r.Save.RefractoryUntilUtc = r.Now.AddSeconds(30);
        Assert.Contains("recovering", r.Engine.TryManualClimax(r.Now));

        r.Save.RefractoryUntilUtc = default;
        r.Engine.Pause();
        Assert.Contains("paused", r.Engine.TryManualClimax(r.Now));
    }

    [Fact]
    public void ManualClimaxFiresAtSeventy()
    {
        var r = new EngineRig();
        r.Save.Arousal = 70;
        r.Run(1);
        Assert.Null(r.Engine.TryManualClimax(r.Now));
        r.Run(1);
        Assert.Equal(1, r.Host.LastTag!.Value.ClimaxCount);
    }

    [Fact]
    public void RefractoryHalvesGainAndBlocksClimax()
    {
        var r = new EngineRig();
        r.Save.Arousal = 99;
        r.Save.RefractoryUntilUtc = r.Now.AddSeconds(30);
        r.Run(11);
        Assert.Equal(0, r.Host.LastTag!.Value.ClimaxCount);
        Assert.Equal(100, r.Save.Arousal, 6);
    }

    // ── Their climax ───────────────────────────────────────────────────────────

    private static EngineRig WithRecentTick()
    {
        var r = Plain();
        r.Save.Level = 50;
        r.Run(11);
        return r;
    }

    [Fact]
    public void TheirClimaxPaysHalf()
    {
        var r = WithRecentTick();
        var before = r.Save.Xp;
        r.BobClimaxes();
        Assert.Equal(75, r.Save.Xp - before, 6);
    }

    [Fact]
    public void AClimaxCountFromBeforeWeMetPaysNothing()
    {
        var r = new EngineRig { BobTag = new PresenceTag(EngineRig.BobPose, 1, 0, 7, false) };
        r.Save.Level = 50;
        r.Run(1);
        Assert.Equal(0, r.Save.Xp);
        Assert.Null(r.Engine.PlateSceneOf(r.Bob).LastClimaxUtc);
    }

    [Fact]
    public void AStrayClimaxWithNoRecentTickPaysNothing()
    {
        var r = Plain();
        r.Run(1);
        r.BobClimaxes();
        Assert.Equal(0, r.Save.Xp);
    }

    [Fact]
    public void AClimaxAfterTheSceneEndedPaysNothing()
    {
        var r = WithRecentTick();
        r.Distance = 10;
        r.Run(31);
        var before = r.Save.Xp;
        r.BobClimaxes();
        Assert.Equal(before, r.Save.Xp);
    }

    [Fact]
    public void RepeatedClimaxesWithinTheLimitPayOnce()
    {
        var r = WithRecentTick();
        var before = r.Save.Xp;
        r.BobClimaxes();
        r.BobClimaxes();
        Assert.Equal(75, r.Save.Xp - before, 6);
    }

    [Fact]
    public void TheirPluginRestartingIsNotAClimax()
    {
        var r = WithRecentTick();
        r.BobTag = r.BobTag with { ClimaxCount = 4 };
        r.Run(1);
        var before = r.Save.Xp;
        r.BobTag = r.BobTag with { ClimaxCount = 0 };
        r.Run(1);
        Assert.InRange(r.Save.Xp - before, 0, 10);
    }

    // ── Layers received ────────────────────────────────────────────────────────

    [Fact]
    public void LayerSoonAfterAClimaxPays()
    {
        var r = WithRecentTick();
        r.BobClimaxes();
        var before = r.Save.Xp;
        r.Now = r.Now.AddSeconds(2);
        r.Engine.HandleLayerReceived(r.Now, r.Bob.Name, r.Bob.World);
        Assert.Equal(75, r.Save.Xp - before, 6);
    }

    [Fact]
    public void LayerArrivingBeforeTheClimaxStillPays()
    {
        var r = WithRecentTick();
        var before = r.Save.Xp;
        r.Engine.HandleLayerReceived(r.Now, r.Bob.Name, r.Bob.World);
        Assert.Equal(before, r.Save.Xp);
        r.BobClimaxes();
        Assert.Equal(75 + 75, r.Save.Xp - before, 6);
    }

    [Fact]
    public void LayerOutsideTheWindowPaysNothing()
    {
        var r = WithRecentTick();
        r.BobClimaxes();
        var before = r.Save.Xp;
        r.Now = r.Now.AddSeconds(11);
        r.Engine.HandleLayerReceived(r.Now, r.Bob.Name, r.Bob.World);
        Assert.Equal(before, r.Save.Xp);
    }

    [Fact]
    public void ManualLayerWithNoClimaxPaysNothing()
    {
        var r = WithRecentTick();
        var before = r.Save.Xp;
        r.Engine.HandleLayerReceived(r.Now, r.Bob.Name, r.Bob.World);
        Assert.Equal(before, r.Save.Xp);
    }

    [Fact]
    public void LayerFromAnotherSourcePaysNothing()
    {
        var r = WithRecentTick();
        r.BobClimaxes();
        var before = r.Save.Xp;
        r.Engine.HandleLayerReceived(r.Now, "Stranger S", "W");
        Assert.Equal(before, r.Save.Xp);
    }

    // ── Their nameplate ────────────────────────────────────────────────────────

    [Fact]
    public void TheirValuesComeFromTheirTag()
    {
        var r = new EngineRig();
        Assert.Null(r.Engine.PlateOf(r.Bob));
        r.BobTag = r.BobTag with { Level = 30, Arousal = 70 };
        r.Run(1);
        Assert.Equal(new PlateValues(30, 70), r.Engine.PlateOf(r.Bob));
    }

    [Fact]
    public void OwnClimaxReachesTheNameplate()
    {
        var r = new EngineRig();
        Assert.Null(r.Engine.OwnPlateScene.LastClimaxUtc);
        r.Save.Arousal = 98;
        r.Run(11);
        Assert.NotNull(r.Engine.OwnPlateScene.LastClimaxUtc);
        Assert.True(r.Engine.OwnPlateScene.InAct);
    }

    [Fact]
    public void TheirClimaxReachesTheirNameplateEvenWithoutXp()
    {
        var r = new EngineRig();
        r.Run(1);
        var at = r.Now;
        r.BobClimaxes();
        Assert.Equal(at, r.Engine.PlateSceneOf(r.Bob).LastClimaxUtc);
        Assert.Equal(0, r.Save.Xp);
    }

    [Fact]
    public void TheirTagSaysWhetherTheyreInAnAct()
    {
        var r = new EngineRig();
        r.Run(1);
        Assert.True(r.Engine.PlateSceneOf(r.Bob).InAct);
        r.BobTag = r.BobTag with { Pose = null };
        r.Run(1);
        Assert.False(r.Engine.PlateSceneOf(r.Bob).InAct);
    }

    [Fact]
    public void SomeoneWhoLeftHasNoValues()
    {
        var r = new EngineRig();
        r.Run(1);
        r.BobVisible = false;
        r.Run(1);
        Assert.Null(r.Engine.PlateOf(r.Bob));
    }

    // ── First-time setup ───────────────────────────────────────────────────────

    [Fact]
    public void NothingEarnsOrPublishesBeforeSetup()
    {
        var r = new EngineRig();
        r.Save.Setup = null;
        r.Run(60);
        Assert.Equal(0, r.Save.Xp);
        Assert.Equal(0, r.Save.Arousal);
        Assert.Empty(r.Host.Tags);
        Assert.Equal(SceneFailure.NeedsSetup, r.Engine.Status.Failure);
    }

    [Fact]
    public void FinishingSetupStartsEarning()
    {
        var r = new EngineRig();
        r.Save.Setup = null;
        r.Run(5);
        r.Engine.CompleteSetup(new ProfileSetup(), r.Now);
        r.Run(11);
        Assert.True(r.Save.Xp > 0);
    }

    [Fact]
    public void ResettingTheProfileRemovesTheTag()
    {
        var r = new EngineRig();
        r.Run(2);
        r.Engine.ResetProgress(r.Now);
        r.Run(1);
        Assert.Null(r.Host.Tags[^1]);
    }

    [Theory]
    [InlineData(Difficulty.Relaxed, 9)]
    [InlineData(Difficulty.Normal, 6)]
    [InlineData(Difficulty.Hard, 4.2)]
    public void DifficultyScalesXp(Difficulty difficulty, double xp)
    {
        var r = Plain();
        r.Save.Setup!.Difficulty = difficulty;
        r.Run(11);
        Assert.Equal(xp, r.Save.Xp, 6);
    }

    [Fact]
    public void ArousalGainScalesArousal()
    {
        var r = new EngineRig();
        r.Save.Setup!.ArousalGain = 2;
        r.Run(11);
        Assert.Equal(6, r.Save.Arousal, 6);
    }

    [Fact]
    public void ByHandOnlyHoldsAtOneHundred()
    {
        var r = new EngineRig();
        r.Save.Setup!.AutoClimax = false;
        r.Save.Arousal = 98;
        r.Run(30);
        Assert.Equal(0, r.Host.LastTag!.Value.ClimaxCount);
        Assert.Equal(100, r.Save.Arousal, 6);
        Assert.Null(r.Engine.TryManualClimax(r.Now));
        r.Run(1);
        Assert.Equal(1, r.Host.LastTag!.Value.ClimaxCount);
    }

    [Fact]
    public void ResettingTheProfileMeansSetupAgain()
    {
        var r = new EngineRig();
        r.Engine.ResetProgress(r.Now);
        Assert.Null(r.Save.Setup);
    }

    [Fact]
    public void FadingReachesTheEngine()
    {
        var r = new EngineRig();
        r.Save.Setup!.Decay = DecayMode.Harsh;
        r.Save.Level = 12;
        r.Save.LastVerifiedUtc = r.Now.AddDays(-30);
        r.BobVisible = false;
        r.Run(1);
        Assert.Equal(10, r.Save.Level);
        Assert.Contains(r.Host.Notes, n => n.Contains("fading"));
    }

    // ── Hiding ─────────────────────────────────────────────────────────────────

    [Fact]
    public void HidingRemovesTheTagAndStopsEarning()
    {
        var r = Plain();
        r.Run(5);
        r.Engine.SetHidden(true);
        r.Run(1);
        Assert.Null(r.Host.Tags[^1]);
        var count = r.Host.Tags.Count;
        r.Run(30);
        Assert.Equal(0, r.Save.Xp);
        Assert.Equal(count, r.Host.Tags.Count);
        Assert.Equal(SceneFailure.Hidden, r.Engine.Status.Failure);
    }

    [Fact]
    public void ShowingAgainPublishesAndEarns()
    {
        var r = Plain();
        r.Engine.SetHidden(true);
        r.Run(5);
        r.Engine.SetHidden(false);
        r.Run(15);
        Assert.NotNull(r.Host.LastTag);
        Assert.True(r.Save.Xp > 0);
    }

    [Fact]
    public void HidingCancelsAQueuedLayer()
    {
        var r = new EngineRig();
        r.Save.Arousal = 80;
        r.Run(6);
        Assert.Null(r.Engine.TryManualClimax(r.Now));
        r.Engine.SetHidden(true);
        r.Run(10);
        Assert.Empty(r.Host.Layers);
    }

    [Fact]
    public void BeingHiddenSurvivesAReloadAndAReset()
    {
        var r = new EngineRig();
        r.Engine.SetHidden(true);
        r.Engine.ResetProgress(r.Now);
        Assert.True(r.Save.Hidden);
    }

    [Fact]
    public void HiddenStartsWithNoTag()
    {
        var r = new EngineRig();
        r.Save.Hidden = true;
        r.Run(5);
        Assert.Empty(r.Host.Tags);
    }

    // ── Goals ──────────────────────────────────────────────────────────────────

    [Fact]
    public void AScenePaysTheFirstTasteAchievementOnce()
    {
        var r = new EngineRig();
        r.Run(25);
        Assert.Single(r.Host.Notes, n => n.Contains("First Taste"));
        Assert.Equal(RewardKind.Achievement, Assert.Single(r.Host.Celebrated).Kind);
        Assert.Contains("first_taste", r.Save.Goals.Achievements);
    }

    [Fact]
    public void NoGoalsOrAchievementsBeforeSetup()
    {
        var r = new EngineRig();
        r.Save.Setup = null;
        r.Run(30);
        Assert.Empty(r.Save.Goals.Daily);
        Assert.Empty(r.Save.Goals.Achievements);
    }

    [Fact]
    public void ScenesCountTowardGoals()
    {
        var r = new EngineRig { Watchers = 3 };
        r.Run(21);
        Assert.Equal(20, r.Save.Goals.Stats["SceneSeconds"]);
        Assert.Equal(20, r.Save.Goals.Stats["WatchedSeconds"]);
        Assert.Equal(20, r.Save.Goals.Stats["CrowdSeconds"]);
        Assert.Equal(1, r.Save.Goals.Stats["ActiveDays"]);
        Assert.Equal(1, r.Save.Goals.PartnerScenes[r.Bob.Key]);
    }

    [Fact]
    public void ACompletedGoalPaysXpScaledByDifficulty()
    {
        var r = Plain();
        r.Save.Setup!.Difficulty = Difficulty.Relaxed;
        r.Save.Level = 50;
        r.Save.Goals.Achievements.Add("first_taste");
        r.Run(1);
        r.Save.Goals.Daily = [new GoalState { Id = "d_climax1" }];
        r.Save.Goals.Weekly = [];
        r.Save.Arousal = 70;
        r.Run(1);
        var before = r.Save.Xp;
        Assert.Null(r.Engine.TryManualClimax(r.Now));
        r.Run(1);
        Assert.Contains(r.Host.Notes, n => n.Contains("Daily goal done"));
        Assert.Equal((150 + 100) * 1.5, r.Save.Xp - before, 6);
        var reward = Assert.Single(r.Host.Celebrated);
        Assert.Equal((RewardKind.Daily, "Climax", 150.0), (reward.Kind, reward.Title, reward.Xp));
    }

    [Fact]
    public void ClimaxesAndLayersReachTheStats()
    {
        var r = WithRecentTick();
        r.BobClimaxes();
        r.Engine.HandleLayerReceived(r.Now, r.Bob.Name, r.Bob.World);
        Assert.Equal(1, r.Save.Goals.Stats["PartnerClimaxes"]);
        Assert.Equal(1, r.Save.Goals.Stats["LayersReceived"]);

        r.Save.Arousal = 98;
        r.Run(14);
        Assert.Equal(1, r.Save.Goals.Stats["Climaxes"]);
        Assert.Equal(1, r.Save.Goals.Stats["LayersGiven"]);
    }

    [Fact]
    public void HoldingAtOneHundredCountsAsEdging()
    {
        var r = new EngineRig();
        r.Save.Setup!.AutoClimax = false;
        r.Save.Arousal = 100;
        r.Run(21);
        Assert.Equal(20, r.Save.Goals.Stats["EdgedSeconds"]);
    }

    // ── Saving ─────────────────────────────────────────────────────────────────

    [Fact]
    public void SavesAtLeastEverySixtySecondsDuringAScene()
    {
        var r = Plain();
        r.Run(5);
        var before = r.Host.Saves;
        r.Run(130);
        Assert.InRange(r.Host.Saves - before, 2, 6);
    }

    [Fact]
    public void SceneEndSaves()
    {
        var r = Plain();
        r.Run(11);
        var before = r.Host.Saves;
        r.Distance = 10;
        r.Run(35);
        Assert.True(r.Host.Saves > before);
        Assert.Null(r.Engine.Status.SceneStartedUtc);
    }

    [Fact]
    public void ResetProgressClearsRecords()
    {
        var r = WithRecentTick();
        r.Save.Arousal = 40;
        r.Engine.ResetProgress(r.Now);
        Assert.Equal(1, r.Save.Level);
        Assert.Equal(0, r.Save.Xp);
        Assert.Equal(0, r.Save.Arousal);
        Assert.Empty(r.Save.SeenActs);
        Assert.Empty(r.Save.SeenPartners);
    }
}
