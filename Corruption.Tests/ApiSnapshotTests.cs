using Corruption.Core;

namespace Corruption.Tests;

public class ApiSnapshotTests
{
    private static readonly DateTime T0 = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private const string Me = "Alice A@W";

    private static CharacterSave Ready() => new() { Setup = new ProfileSetup() };

    [Fact]
    public void BeforeSetupEverythingIsEmpty()
    {
        var save = new CharacterSave();
        Assert.False(ApiSnapshots.Profile(save, Me, new Balance()).Ready);
        Assert.Equal(ApiSnapshots.NoArousal, ApiSnapshots.Arousal(save, Me, T0));
        Assert.Equal(ApiSnapshots.NoCycle, ApiSnapshots.Cycle(save, Me, T0));
        Assert.Empty(ApiSnapshots.Births(save, Me));
        Assert.False(ApiSnapshots.Profile(Ready(), null, new Balance()).Ready);
    }

    [Fact]
    public void LevelTwentySevenWithTwoStars()
    {
        var save = Ready();
        save.Level = 27;
        save.Prestige = 2;
        var profile = ApiSnapshots.Profile(save, Me, new Balance());
        Assert.True(profile.Ready);
        Assert.Equal(("Alice A", "W"), (profile.Name, profile.World));
        Assert.Equal(27, profile.Level);
        Assert.Equal(Progression.TierOf(27).ToString(), profile.Tier);
        Assert.Equal(2, profile.Prestige);
    }

    [Fact]
    public void RefractoryLeft()
    {
        var save = Ready();
        save.Arousal = 30;
        save.RefractoryUntilUtc = T0.AddSeconds(40);
        var arousal = ApiSnapshots.Arousal(save, Me, T0);
        Assert.Equal(40, arousal.RefractorySeconds, 6);
        Assert.Equal(30, arousal.Arousal);
        Assert.Equal("auto", arousal.ClimaxMode);
        Assert.False(arousal.Edging);
    }

    [Fact]
    public void EarningDuoScene()
    {
        var bob = new PeerRecord { Name = "Bob B", World = "W" };
        var status = SceneStatus.Empty with { State = SceneState.Earning, Partner = bob, SceneStartedUtc = T0.AddMinutes(-5), Watchers = 2 };
        var scene = ApiSnapshots.Scene(Ready(), Me, status, T0);
        Assert.Equal(("earning", false, "Bob B", "W", 2, 300.0), (scene.State, scene.Solo, scene.PartnerName, scene.PartnerWorld, scene.Watchers, scene.Seconds));
    }

    [Fact]
    public void NoBond()
    {
        var bond = ApiSnapshots.Bond(Ready(), Me, 0);
        Assert.True(bond.Ready);
        Assert.Equal(("", false), (bond.Name, bond.Formed));
    }

    private static CharacterSave Carrier(int day)
    {
        var save = Ready();
        PregnancyBook.Enable(save.Pregnancy, true, false, day, T0);
        return save;
    }

    [Fact]
    public void Ovulation()
    {
        var cycle = ApiSnapshots.Cycle(Carrier(5), Me, T0.AddHours(1));
        Assert.Equal((true, 5, "ovulation"), (cycle.Running, cycle.Day, cycle.Phase));
        Assert.Equal(0.40, cycle.Chance, 6);
        Assert.False(cycle.Protected);
        Assert.True(cycle.NextPhaseUtc > T0);
    }

    [Fact]
    public void PregnantHasNoCycle()
    {
        var save = Carrier(5);
        KnownPregnancy.Conceive(save.Pregnancy, T0, "Bob B@W");
        var cycle = ApiSnapshots.Cycle(save, Me, T0.AddHours(1));
        Assert.Equal((false, 0, 0.0), (cycle.Running, cycle.Day, cycle.Chance));
    }

    [Fact]
    public void SecondTrimester()
    {
        var save = Carrier(5);
        KnownPregnancy.Conceive(save.Pregnancy, T0, "Bob B@W");
        var now = T0.AddDays(7);
        var p = ApiSnapshots.Pregnancy(save, Me, now);
        Assert.Equal("second", p.Stage);
        Assert.Equal(7, p.DaysPregnant, 6);
        Assert.Equal(now.AddDays(7), p.DueUtc);
        Assert.Equal(("Bob B", "W"), (p.FatherName, p.FatherWorld));
        Assert.Equal(1, p.Conceptions);
    }

    [Fact]
    public void BirthsListTheFather()
    {
        var save = Ready();
        save.Pregnancy.BirthLog.Add(new BirthRecord { BornUtc = T0, ConceivedUtc = T0.AddDays(-14), FatherKey = "Bob B@W" });
        Assert.Equal([new BirthInfo(T0, T0.AddDays(-14), "Bob B", "W")], ApiSnapshots.Births(save, Me));
    }

    [Fact]
    public void NearbyOnlyShowsWhatTagsPublish()
    {
        var bob = new PeerRecord { Name = "Bob B", World = "W" };
        var body = new BodyTag(BodyFlags.CanCarry | BodyFlags.Vagina, PregnancyStage.Second, "a1b2c3d4");
        var seen = new VisiblePlayer(bob, 2.5, new PoseKey(50, 0), new PresenceTag(new PoseKey(50, 0), 40, 70, 3, false), Body: body);
        var entry = Assert.Single(ApiSnapshots.Nearby(Ready(), Me, [seen]));
        Assert.Equal(("Bob B", 40, 70, true, 3), (entry.Name, entry.Level, entry.Arousal, entry.InAct, entry.ClimaxCount));
        Assert.Equal((true, true, false, "second", "vagina"), (entry.HasBodyTag, entry.CanCarry, entry.CanBreed, entry.Stage, entry.Anatomy));
    }

    [Fact]
    public void ConsentIsOffAndSurvivesAReset()
    {
        var save = Ready();
        Assert.False(save.PluginConsent.Allowed);
        save.PluginConsent.Allowed = true;
        save.ResetProgress(T0);
        Assert.True(save.PluginConsent.Allowed);
    }

    [Theory]
    [InlineData("""{"Breed":false,"Arousal":true,"Climax":false}""", true)]
    [InlineData("""{"Breed":false,"Arousal":false,"Climax":false}""", false)]
    [InlineData("""{"Allowed":true}""", true)]
    public void OlderSavesCarryAnyConsentOver(string json, bool allowed)
    {
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var consent = System.Text.Json.JsonSerializer.Deserialize<PluginConsent>(json, options)!;
        Assert.Equal(allowed, consent.Allowed);
        Assert.Equal($$"""{"Allowed":{{(allowed ? "true" : "false")}}}""", System.Text.Json.JsonSerializer.Serialize(consent));
    }

    [Fact]
    public void ArousalAllowanceLimitsPerCallAndPerMinute()
    {
        var a = new ArousalAllowance();
        Assert.Equal(25, a.Allow("M", 40, T0));
        a.Spend("M", 25, T0);
        a.Spend("M", 25, T0.AddSeconds(10));
        Assert.Equal(10, a.Allow("M", 20, T0.AddSeconds(20)));
        Assert.Equal(-10, a.Allow("M", -20, T0.AddSeconds(20)));
        Assert.Equal(20, a.Allow("Other", 20, T0.AddSeconds(20)));
        Assert.Equal(20, a.Allow("M", 20, T0.AddSeconds(61)));
    }
}
