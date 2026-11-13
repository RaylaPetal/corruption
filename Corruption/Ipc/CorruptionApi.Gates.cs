using System;
using System.Collections.Generic;
using System.Linq;
using Corruption.Core;
using ProfileTuple = (bool Ready, string Name, string World, int Level, string Tier, double Xp, double XpToNext, int Prestige,
    string Title, string Personality, string Anatomy, long Sins, int StreakDays, bool Paused, bool Hidden);
using ArousalTuple = (bool Ready, int Arousal, double RefractorySeconds, bool Edging, string ClimaxMode);
using SceneTuple = (bool Ready, string State, bool Solo, string PartnerName, string PartnerWorld, int Watchers, double Seconds);
using BondTuple = (bool Ready, string Name, string World, bool Formed, int Level);
using CycleTuple = (bool Ready, bool Enabled, bool CanCarry, bool CanBreed, bool Running, int Day, string Phase, double Chance,
    bool Protected, System.DateTime NextPhaseUtc);
using PregnancyTuple = (bool Ready, string Stage, double DaysPregnant, System.DateTime ConceivedUtc, System.DateTime DueUtc,
    string FatherName, string FatherWorld, System.DateTime RecoveryUntilUtc, string Craving, bool CravingMet, bool Unsatisfied,
    int Conceptions, int Births, int Fathered);
using BirthTuple = (System.DateTime BornUtc, System.DateTime ConceivedUtc, string FatherName, string FatherWorld);
using NearbyTuple = (string Name, string World, double Distance, int Level, int Arousal, bool InAct, int ClimaxCount,
    bool HasBodyTag, bool CanCarry, bool CanBreed, bool Virile, string Anatomy, string Stage);

namespace Corruption.Ipc;

/// The reads and writes. Reads come from ApiSnapshots in Core, writes from the engine's API entry points.
public sealed partial class CorruptionApi
{
    private void RegisterReads()
    {
        Read<ProfileTuple>("Corruption.GetProfile", () => Profile(), Of(ApiSnapshots.NoProfile));
        Read<ArousalTuple>("Corruption.GetArousal", () => Of(Engine is { } e ? ApiSnapshots.Arousal(e.Save, e.SelfKey, DateTime.UtcNow) : ApiSnapshots.NoArousal),
            Of(ApiSnapshots.NoArousal));
        Read<SceneTuple>("Corruption.GetScene", () => Of(Engine is { } e ? ApiSnapshots.Scene(e.Save, e.SelfKey, e.Status, DateTime.UtcNow) : ApiSnapshots.NoScene),
            Of(ApiSnapshots.NoScene));
        Read<BondTuple>("Corruption.GetBond", () => Of(Engine is { } e ? ApiSnapshots.Bond(e.Save, e.SelfKey, e.Bonds.Level) : ApiSnapshots.NoBond),
            Of(ApiSnapshots.NoBond));
        Read<CycleTuple>("Corruption.GetCycle", () => Of(Cycle()), Of(ApiSnapshots.NoCycle));
        Read<PregnancyTuple>("Corruption.GetPregnancy", () => Of(Engine is { } e ? ApiSnapshots.Pregnancy(e.Save, e.SelfKey, DateTime.UtcNow) : ApiSnapshots.NoPregnancy),
            Of(ApiSnapshots.NoPregnancy));
        Read<List<BirthTuple>>("Corruption.GetBirths",
            () => Engine is { } e ? ApiSnapshots.Births(e.Save, e.SelfKey).Select(Of).ToList() : [], []);
        Read<List<NearbyTuple>>("Corruption.GetNearby",
            () => Engine is { } e ? ApiSnapshots.Nearby(e.Save, e.SelfKey, scene.Nearby).Select(Of).ToList() : [], []);
        Read<bool>("Corruption.GetForceCreampie", () => Engine?.ForcesCreampies == true, false);
        Read<string, string, double, double>("Corruption.BreedChance",
            (name, world, factor) => Engine is { } e ? e.BreedChanceFor(DateTime.UtcNow, Player(name, world), factor) : 0, 0);
    }

    private void RegisterWrites()
    {
        Outcome<string, string, string, double>("Corruption.Breed", (name, world, plugin, factor) => Engine is not { } e ? ApiOutcomes.NoCharacter
            : string.IsNullOrWhiteSpace(name) ? ApiOutcomes.NoCharacter
            : e.BreedFromPlugin(DateTime.UtcNow, Player(name, world), plugin, factor));
        Outcome<string, bool>("Corruption.SetForceCreampie", (plugin, on) => Engine is not { } e ? ApiOutcomes.NotSetUp
            : e.SetForceCreampieFromPlugin(DateTime.UtcNow, on, plugin));
        Outcome<double, string>("Corruption.AddArousal", (amount, plugin) => Engine is not { } e ? ApiOutcomes.NotSetUp
            : double.IsFinite(amount) ? e.AddArousalFromPlugin(DateTime.UtcNow, amount, plugin) : ApiOutcomes.Error);
        Outcome<string>("Corruption.Climax", plugin => Engine is not { } e ? ApiOutcomes.NotSetUp : e.ClimaxFromPlugin(DateTime.UtcNow, plugin));
    }

    private ProfileInfo ProfileInfo() => Engine is { } e ? ApiSnapshots.Profile(e.Save, e.SelfKey, e.Balance) : ApiSnapshots.NoProfile;

    private ProfileTuple Profile() => Of(ProfileInfo());

    private CycleInfo Cycle() => Engine is { } e
        ? ApiSnapshots.Cycle(e.Save, e.SelfKey, DateTime.UtcNow, ModifierBook.For(e.Save).ConceptionOwn)
        : ApiSnapshots.NoCycle;

    /// Every read at once, for change detection. Durations that tick every second (scene seconds, refractory left, days
    /// pregnant, next phase) are left out, so "state changed" means something did; refractory only counts as on or off.
    private string Fingerprint(DateTime now)
    {
        if (Engine is not { } e) return "";
        var s = e.Save;
        var arousal = ApiSnapshots.Arousal(s, e.SelfKey, now);
        return string.Join("|",
            ProfileInfo(),
            arousal with { RefractorySeconds = arousal.RefractorySeconds > 0 ? 1 : 0 },
            ApiSnapshots.Scene(s, e.SelfKey, e.Status, now) with { Seconds = 0 },
            ApiSnapshots.Bond(s, e.SelfKey, e.Bonds.Level),
            Cycle() with { NextPhaseUtc = default },
            ApiSnapshots.Pregnancy(s, e.SelfKey, now) with { DaysPregnant = 0 },
            s.Pregnancy.BirthLog.Count,
            string.Join(",", ApiSnapshots.Nearby(s, e.SelfKey, scene.Nearby).Select(n => n with { Distance = 0 })));
    }

    private static ProfileTuple Of(ProfileInfo p) => (p.Ready, p.Name, p.World, p.Level, p.Tier, p.Xp, p.XpToNext, p.Prestige, p.Title,
        p.Personality, p.Anatomy, p.Sins, p.StreakDays, p.Paused, p.Hidden);

    private static ArousalTuple Of(ArousalInfo a) => (a.Ready, a.Arousal, a.RefractorySeconds, a.Edging, a.ClimaxMode);

    private static SceneTuple Of(SceneInfo s) => (s.Ready, s.State, s.Solo, s.PartnerName, s.PartnerWorld, s.Watchers, s.Seconds);

    private static BondTuple Of(BondInfo b) => (b.Ready, b.Name, b.World, b.Formed, b.Level);

    private static CycleTuple Of(CycleInfo c) => (c.Ready, c.Enabled, c.CanCarry, c.CanBreed, c.Running, c.Day, c.Phase, c.Chance, c.Protected,
        c.NextPhaseUtc);

    private static PregnancyTuple Of(PregnancyInfo p) => (p.Ready, p.Stage, p.DaysPregnant, p.ConceivedUtc, p.DueUtc, p.FatherName, p.FatherWorld,
        p.RecoveryUntilUtc, p.Craving, p.CravingMet, p.Unsatisfied, p.Conceptions, p.Births, p.Fathered);

    private static BirthTuple Of(BirthInfo b) => (b.BornUtc, b.ConceivedUtc, b.FatherName, b.FatherWorld);

    private static NearbyTuple Of(NearbyInfo n) => (n.Name, n.World, n.Distance, n.Level, n.Arousal, n.InAct, n.ClimaxCount, n.HasBodyTag,
        n.CanCarry, n.CanBreed, n.Virile, n.Anatomy, n.Stage);
}
