using System.Numerics;

namespace Corruption.Core;

public enum Tier { Pure, Curious, Tempted, Tainted, Corrupted, Depraved, Fallen }

public readonly record struct XpResult(int LevelsGained, Tier? NewTier);

public static class Progression
{
    /// Whole XP needed to go from <paramref name="level"/> to the next. 0 at the cap.
    public static long XpToNext(int level, Balance b)
        => level >= b.LevelCap ? 0 : (long)Math.Round(b.CurveBase + b.CurveFactor * Math.Pow(level, b.CurveExponent), MidpointRounding.AwayFromZero);

    public static Tier TierOf(int level) => level switch
    {
        < 10 => Tier.Pure,
        < 25 => Tier.Curious,
        < 40 => Tier.Tempted,
        < 60 => Tier.Tainted,
        < 80 => Tier.Corrupted,
        < 95 => Tier.Depraved,
        _ => Tier.Fallen,
    };

    /// Applies XP, levelling as often as it pays for. XP beyond the cap isn't stored.
    public static XpResult AddXp(CharacterSave save, double amount, Balance b)
    {
        if (save.Level >= b.LevelCap)
        {
            save.Xp = 0;
            return default;
        }

        var before = TierOf(save.Level);
        var gained = 0;
        save.Xp += amount;
        while (save.Level < b.LevelCap && save.Xp >= XpToNext(save.Level, b))
        {
            save.Xp -= XpToNext(save.Level, b);
            save.Level++;
            gained++;
        }
        if (save.Level >= b.LevelCap) save.Xp = 0;
        var after = TierOf(save.Level);
        return new XpResult(gained, after != before ? after : null);
    }

    /// 1.0 through the grace period, then one step down per whole extra minute, to the floor. Modifiers can lengthen or
    /// shorten the grace by <paramref name="extraGraceMinutes"/>, but never below 0.
    public static double Fatigue(TimeSpan onThisAct, Balance b, double extraGraceMinutes = 0)
    {
        var grace = Math.Max(0, b.FatigueGraceMinutes + extraGraceMinutes);
        var extra = Math.Floor(onThisAct.TotalMinutes - grace);
        return extra <= 0 ? 1.0 : Math.Max(b.FatigueFloor, 1.0 - extra * b.FatigueStep);
    }

    /// The first level of the tier this level is in. Fading never goes below it.
    public static int TierFloor(int level) => TierOf(level) switch
    {
        Tier.Pure => 1,
        Tier.Curious => 10,
        Tier.Tempted => 25,
        Tier.Tainted => 40,
        Tier.Corrupted => 60,
        Tier.Depraved => 80,
        _ => 95,
    };

    public static bool IsFading(CharacterSave save, DateTime now)
        => save.Setup is { } setup && save.LastVerifiedUtc != default && now >= save.LastVerifiedUtc.AddHours(Math.Min(setup.DecayRule.GraceHours, 1e6))
           && setup.Decay != DecayMode.Off;

    /// Takes off whole days of fading owed since the last verified tick, covering time offline. Never below the first
    /// level of the current tier. True when the level dropped.
    public static bool ApplyFading(CharacterSave save, DateTime now, Balance b)
    {
        if (!IsFading(save, now)) return false;

        var (grace, share) = save.Setup!.DecayRule;
        var start = save.LastVerifiedUtc.AddHours(grace);
        if (save.FadedThroughUtc > start) start = save.FadedThroughUtc;
        var days = (int)Math.Min(Math.Floor((now - start).TotalDays), 3650);
        if (days < 1) return false;

        var before = save.Level;
        var floor = TierFloor(save.Level);
        for (var day = 0; day < days; day++)
        {
            save.Xp -= share * Math.Max(XpToNext(save.Level, b), XpToNext(Math.Max(1, save.Level - 1), b));
            while (save.Xp < 0 && save.Level > floor)
            {
                save.Level--;
                save.Xp += XpToNext(save.Level, b);
            }
            if (save.Xp < 0) save.Xp = 0;
        }
        save.FadedThroughUtc = start.AddDays(days);
        return save.Level < before;
    }

    /// What multiplies a verified tick's XP, from everything around the scene rather than the animation.
    /// <paramref name="bondLevel"/>: the bond's level when the partner is the formed bond and their tag still names
    /// this player, else 0. <paramref name="layersWorn"/>: Cummy layer stages worn. <paramref name="stars"/>: prestige.
    /// <paramref name="title"/>: the equipped title's bonus, which can raise caps and adds its own factor.
    public static TickFactors Factors(int level, int? partnerLevel, double watchers, int streakDays, double fatigue, Balance b,
        int bondLevel = 0, int layersWorn = 0, int stars = 0, Modifiers? title = null, bool fullMoon = false)
    {
        title ??= Modifiers.None;
        var own = (int)TierOf(level);
        var above = partnerLevel is { } other ? Math.Max(0, (int)TierOf(other) - own) : 0;
        return new TickFactors(
            1 + b.XpPerOwnTier * own,
            Math.Min(b.PartnerFactorCap, 1 + b.XpPerPartnerTierAbove * above),
            WatchFactor(watchers, b, title.WatcherCap),
            Math.Min(title.StreakCap ?? b.StreakFactorCap, 1 + b.XpPerStreakDay * Math.Max(0, streakDays - 1)),
            fatigue,
            BondFactor(bondLevel, b) + (bondLevel > 0 ? title.BondedExtra : 0),
            MarkedFactor(layersWorn, b, title.MarkedCap, title.MarkedPerLayer),
            PrestigeRules.Factor(stars, b),
            Title: title.TickXp * (fullMoon ? title.FullMoonTickXp : 1));
    }

    public static double BondFactor(int bondLevel, Balance b)
        => bondLevel <= 0 ? 1 : bondLevel >= BondLevels.DevotedLevel ? b.DevotedBondFactor : b.BondedFactor;

    public static double MarkedFactor(int layersWorn, Balance b, double? cap = null, double? perLayer = null)
        => Math.Min(cap ?? b.MarkedFactorCap, 1 + (perLayer ?? b.XpPerLayerWorn) * Math.Max(0, layersWorn));

    /// Face stages count this much by default; a title can raise it.
    public const double FaceWeight = 2;

    /// A full coat (every unblocked part layered) makes the stages count this much more.
    public const double FullCoat = 1.5;

    /// From Cummy version 5: each stage counts, face stages more, and a full coat more again, up to the same cap.
    public static double WeightedMarkedFactor(IReadOnlyDictionary<string, int> stages, IReadOnlyCollection<string> blocked, Balance b,
        Modifiers? mods = null)
    {
        mods ??= Modifiers.None;
        var face = mods.FaceWeight ?? FaceWeight;
        var weighted = stages.Sum(s => Math.Max(0, s.Value) * (s.Key == "mo" ? face : 1));
        if (IsFullCoat(stages, blocked)) weighted *= FullCoat;
        return Math.Min(mods.MarkedCap ?? b.MarkedFactorCap, 1 + (mods.MarkedPerLayer ?? b.XpPerLayerWorn) * weighted);
    }

    /// Every part the wearer doesn't block has a stage.
    public static bool IsFullCoat(IReadOnlyDictionary<string, int> stages, IReadOnlyCollection<string> blocked)
    {
        var open = CummyParts.All.Where(p => !blocked.Contains(p)).ToList();
        return open.Count > 0 && open.All(p => stages.GetValueOrDefault(p) > 0);
    }

    public static double WatchFactor(double watchers, Balance b, double? cap = null)
        => Math.Min(cap ?? b.WatcherFactorCap, 1 + b.XpPerWatcher * Math.Max(0, watchers));

    /// Counts today toward the streak. Each missed day halves it rather than ending it, unless
    /// <paramref name="protectDay"/> says a Streak Charm covers that day.
    /// <paramref name="endsOnMiss"/>: an unprotected missed day ends the streak rather than halving it.
    public static void UpdateStreak(CharacterSave save, DateTime now, Func<bool>? protectDay = null, bool endsOnMiss = false)
    {
        var today = now.Date;
        if (save.LastSceneDayUtc == today) return;

        var missed = save.LastSceneDayUtc == default ? 0 : (int)(today - save.LastSceneDayUtc).TotalDays - 1;
        for (var i = 0; i < missed && save.StreakDays > 0; i++)
        {
            if (protectDay?.Invoke() == true) continue;
            save.StreakDays = endsOnMiss ? 0 : save.StreakDays / 2;
        }
        save.StreakDays++;
        save.LastSceneDayUtc = today;
    }
}

/// Each factor of a tick's XP. 1 means no effect.
public readonly record struct TickFactors(double Level, double Partner, double Watchers, double Streak, double Fatigue,
    double Bonded = 1, double Marked = 1, double Prestige = 1, double Items = 1, double Pregnancy = 1, double Title = 1,
    double Persona = 1, double Solo = 1)
{
    public static readonly TickFactors None = new(1, 1, 1, 1, 1);

    public double Total => Level * Partner * Watchers * Streak * Fatigue * Bonded * Marked * Prestige * Items * Pregnancy * Title * Persona * Solo;
}

/// Who counts as watching: near the scene, and either targeting it or close by and facing it. Only a count is used, and
/// it never leaves the client.
public static class WatcherCheck
{
    public static bool IsWatching(Vector3 viewer, float viewerRotation, bool targetsScene, Vector3 scene, Balance b)
    {
        var distance = Vector3.Distance(viewer, scene);
        if (distance > b.WatchRadiusYalms) return false;
        if (targetsScene) return true;
        if (distance > b.WatchCloseYalms || distance < 0.01f) return false;

        var toScene = Vector3.Normalize(new Vector3(scene.X - viewer.X, 0, scene.Z - viewer.Z));
        var facing = new Vector3(MathF.Sin(viewerRotation), 0, MathF.Cos(viewerRotation));
        return Vector3.Dot(toScene, facing) >= Math.Cos(b.WatchFacingDegrees * Math.PI / 180);
    }
}

public static class Arousal
{
    public static double Gain(Tier corruption, double watchers, bool refractory, Balance b, double? watcherCap = null)
    {
        var gain = b.ArousalPerTick * (1 + b.ArousalPerCorruptionTier * (int)corruption) * Progression.WatchFactor(watchers, b, watcherCap);
        return refractory ? gain * b.RefractoryGainFactor : gain;
    }

    /// Takes off the decay owed since the anchor, keeping any part-finished step. <paramref name="factor"/> scales each step.
    public static void ApplyDecay(CharacterSave save, DateTime now, Balance b, double factor = 1)
    {
        if (save.ArousalAnchorUtc == default)
        {
            save.ArousalAnchorUtc = now;
            return;
        }
        var steps = Math.Floor((now - save.ArousalAnchorUtc).TotalSeconds / b.ArousalDecaySeconds);
        if (steps <= 0) return;
        save.Arousal = Math.Max(0, save.Arousal - steps * b.ArousalDecayStep * factor);
        save.ArousalAnchorUtc = save.ArousalAnchorUtc.AddSeconds(steps * b.ArousalDecaySeconds);
    }

    public static bool InRefractory(CharacterSave save, DateTime now) => now < save.RefractoryUntilUtc;
}
