namespace Corruption.Core;

// What Corruption's plugin API reads out. Each record is turned into a value tuple at the IPC boundary. Enums cross as
// lower-case strings so new values never break a caller, and "no time" is default(DateTime).

public sealed record ProfileInfo(bool Ready, string Name, string World, int Level, string Tier, double Xp, double XpToNext,
    int Prestige, string Title, string Personality, string Anatomy, long Sins, int StreakDays, bool Paused, bool Hidden);

public sealed record ArousalInfo(bool Ready, int Arousal, double RefractorySeconds, bool Edging, string ClimaxMode);

public sealed record SceneInfo(bool Ready, string State, bool Solo, string PartnerName, string PartnerWorld, int Watchers, double Seconds);

public sealed record BondInfo(bool Ready, string Name, string World, bool Formed, int Level);

public sealed record CycleInfo(bool Ready, bool Enabled, bool CanCarry, bool CanBreed, bool Running, int Day, string Phase,
    double Chance, bool Protected, DateTime NextPhaseUtc);

public sealed record PregnancyInfo(bool Ready, string Stage, double DaysPregnant, DateTime ConceivedUtc, DateTime DueUtc,
    string FatherName, string FatherWorld, DateTime RecoveryUntilUtc, string Craving, bool CravingMet, bool Unsatisfied,
    int Conceptions, int Births, int Fathered);

public sealed record BirthInfo(DateTime BornUtc, DateTime ConceivedUtc, string FatherName, string FatherWorld);

/// Only what a player's own tags broadcast to everyone: never a cycle day or a father.
public sealed record NearbyInfo(string Name, string World, double Distance, int Level, int Arousal, bool InAct, int ClimaxCount,
    bool HasBodyTag, bool CanCarry, bool CanBreed, bool Virile, string Anatomy, string Stage);

public static class ApiSnapshots
{
    public static readonly ProfileInfo NoProfile = new(false, "", "", 0, "", 0, 0, 0, "", "", "", 0, 0, false, false);
    public static readonly ArousalInfo NoArousal = new(false, 0, 0, false, "");
    public static readonly SceneInfo NoScene = new(false, "", false, "", "", 0, 0);
    public static readonly BondInfo NoBond = new(false, "", "", false, 0);
    public static readonly CycleInfo NoCycle = new(false, false, false, false, false, 0, "", 0, false, default);
    public static readonly PregnancyInfo NoPregnancy = new(false, "", 0, default, default, "", "", default, "", false, false, 0, 0, 0);

    /// The local character counts as ready once logged in (a key) and set up.
    public static bool IsReady(CharacterSave save, string? selfKey) => save.Setup is not null && selfKey is not null;

    public static string Lower<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();

    /// `Name@World` split in two; both empty for none.
    public static (string Name, string World) Split(string? key)
    {
        if (string.IsNullOrEmpty(key)) return ("", "");
        var at = key.IndexOf('@');
        return at < 0 ? (key, "") : (key[..at], key[(at + 1)..]);
    }

    public static ProfileInfo Profile(CharacterSave save, string? selfKey, Balance b)
    {
        if (!IsReady(save, selfKey)) return NoProfile;
        var (name, world) = Split(selfKey);
        return new ProfileInfo(true, name, world, save.Level, Progression.TierOf(save.Level).ToString(), save.Xp,
            save.Level >= b.LevelCap ? 0 : Progression.XpToNext(save.Level, b), save.Prestige,
            TitleBook.Equipped(save)?.Name ?? "", PersonaBook.Personality(save).Name, Lower(save.Anatomy),
            save.Wallet.Sins, save.StreakDays, save.Paused, save.Hidden);
    }

    public static ArousalInfo Arousal(CharacterSave save, string? selfKey, DateTime now)
    {
        if (!IsReady(save, selfKey)) return NoArousal;
        var shown = PlateComposer.ArousalNumber(save.Arousal);
        var refractory = Math.Max(0, (save.RefractoryUntilUtc - now).TotalSeconds);
        return new ArousalInfo(true, shown, refractory, shown >= 100 && refractory <= 0, save.Setup!.AutoClimax ? "auto" : "manual");
    }

    public static SceneInfo Scene(CharacterSave save, string? selfKey, SceneStatus status, DateTime now)
    {
        if (!IsReady(save, selfKey)) return NoScene;
        return new SceneInfo(true, Lower(status.State), status.Solo, status.Partner?.Name ?? "", status.Partner?.World ?? "",
            status.Watchers, status.SceneStartedUtc is { } started ? Math.Max(0, (now - started).TotalSeconds) : 0);
    }

    public static BondInfo Bond(CharacterSave save, string? selfKey, int level)
    {
        if (!IsReady(save, selfKey) || save.BondWith is not { } with) return IsReady(save, selfKey) ? NoBond with { Ready = true } : NoBond;
        var (name, world) = Split(with);
        return new BondInfo(true, name, world, save.BondFormed, save.BondFormed ? level : 0);
    }

    /// The chance is what a breeding would have now from this character's side alone: no partner factors. A secret
    /// pregnancy reads as the cycle it started in.
    public static CycleInfo Cycle(CharacterSave save, string? selfKey, DateTime now, double ownTitleFactor = 1)
    {
        if (!IsReady(save, selfKey)) return NoCycle;
        var p = save.Pregnancy;
        var running = PregnancyBook.ShowsCycle(p, now);
        var day = running ? CycleClock.DayAt(p.CycleStartUtc, now) : 0;
        return new CycleInfo(true, p.Enabled, p.Enabled && p.CanCarry, p.Enabled && p.CanBreed, running, day,
            running ? Lower(CycleClock.PhaseOf(day).Phase) : "",
            running ? PregnancyBook.ShownConceptionChance(save, now, false, 0, false, ownTitleFactor) : 0,
            ItemBook.PreventsConception(save, now),
            running ? CycleClock.NextPhaseAt(p.CycleStartUtc, now) : default);
    }

    public static PregnancyInfo Pregnancy(CharacterSave save, string? selfKey, DateTime now)
    {
        if (!IsReady(save, selfKey)) return NoPregnancy;
        var p = save.Pregnancy;
        // A secret pregnancy reads as none. The conception count only moves at confirmation.
        var pregnant = PregnancyBook.IsConfirmed(p);
        var (fatherName, fatherWorld) = Split(pregnant ? p.FatherKey : null);
        return new PregnancyInfo(true, Lower(PregnancyBook.ShownStage(p, now)), pregnant ? PregnancyBook.DaysPregnant(p, now) : 0,
            pregnant ? p.ConceivedUtc!.Value : default, pregnant ? p.DueUtc!.Value : default, fatherName, fatherWorld,
            PregnancyBook.IsRecovering(p, now) ? p.RecoveryUntilUtc : default,
            pregnant && p.Craving is { } craving ? Lower(craving) : "", pregnant && p.CravingMet, pregnant && p.Unsatisfied,
            p.Conceptions, p.Births, p.Fathered);
    }

    public static IReadOnlyList<BirthInfo> Births(CharacterSave save, string? selfKey)
        => !IsReady(save, selfKey) ? [] : save.Pregnancy.BirthLog.Select(b =>
        {
            var (name, world) = Split(b.FatherKey);
            return new BirthInfo(b.BornUtc, b.ConceivedUtc, name, world);
        }).ToList();

    public static IReadOnlyList<NearbyInfo> Nearby(CharacterSave save, string? selfKey, IEnumerable<VisiblePlayer> visible)
        => !IsReady(save, selfKey) ? [] : visible.Select(v => new NearbyInfo(v.Player.Name, v.Player.World, v.Distance,
            v.Tag.Level, v.Tag.Arousal, v.Tag.Pose is not null, v.Tag.ClimaxCount,
            v.Body is not null, v.Body?.CanCarry == true, v.Body?.CanBreed == true, v.Body?.Virile == true,
            v.Body is { } body ? Lower(body.Anatomy) : "", v.Body is { } b ? Lower(b.Stage) : "")).ToList();
}
