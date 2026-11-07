namespace Corruption.Core;

public enum CyclePhase { Period, Follicular, Fertile, Ovulation, Luteal }

/// A 7-day cycle in real time, counted from its start.
public static class CycleClock
{
    public const int Length = 7;

    public readonly record struct PhaseInfo(CyclePhase Phase, double ArousalFactor, double ConceptionChance);

    public static int DayAt(DateTime start, DateTime now)
    {
        var days = (int)Math.Floor((now - start).TotalDays);
        return ((days % Length) + Length) % Length + 1;
    }

    public static PhaseInfo PhaseOf(int day) => day switch
    {
        1 or 2 => new(CyclePhase.Period, 0.8, 0),
        3 => new(CyclePhase.Follicular, 1.0, 0.05),
        4 => new(CyclePhase.Fertile, 1.2, 0.25),
        5 => new(CyclePhase.Ovulation, 1.3, 0.40),
        _ => new(CyclePhase.Luteal, 1.0, 0.05),
    };

    /// The first day of each phase after day 1, then day 1 of the next cycle.
    private static readonly int[] PhaseStarts = [3, 4, 5, 6, Length + 1];

    /// When the next phase starts.
    public static DateTime NextPhaseAt(DateTime start, DateTime now)
    {
        var day = DayAt(start, now);
        var cycleStart = start.AddDays(Math.Floor((now - start).TotalDays) - (day - 1));
        return cycleStart.AddDays(PhaseStarts.First(d => d > day) - 1);
    }
}

public enum PregnancyEvent { None, Birth, RecoveryEnded }

/// Pregnancy rules for one character. Everything is derived from the stored timestamps.
public static class PregnancyBook
{
    public const double DaysLong = 14;
    public const double RecoveryDays = 2;
    public const double MorningAfterHours = 24;
    public const double SwiftLabourDays = 2;
    public const double ConceptionCap = 0.9;
    public const double VirileFactor = 1.5;
    public const double FertileBondFactor = 1.5;
    public const double PregnantTickXp = 1.1;
    public const double UnsatisfiedFactor = 0.85;
    public const int DefaultStartDay = 3;
    public const double MinChanceFactor = 0.1;
    public const double MaxChanceFactor = 5;

    /// Cummy's part id for the groin: only a cum shot there can lead to conception.
    public const string GroinPart = "gr";

    public static bool IsPregnant(PregnancyState p) => p.Enabled && p.ConceivedUtc is not null && p.DueUtc is not null;

    public static bool IsRecovering(PregnancyState p, DateTime now) => p.Enabled && now < p.RecoveryUntilUtc;

    /// The cycle runs for carriers who are neither pregnant nor recovering.
    public static bool HasCycle(PregnancyState p, DateTime now) => p.Enabled && p.CanCarry && !IsPregnant(p) && !IsRecovering(p, now);

    public static double DaysPregnant(PregnancyState p, DateTime now)
        => p.DueUtc is { } due ? Math.Clamp(DaysLong - (due - now).TotalDays, 0, DaysLong) : 0;

    public static PregnancyStage StageOf(PregnancyState p, DateTime now)
    {
        if (IsPregnant(p))
        {
            var days = DaysPregnant(p, now);
            return days < 5 ? PregnancyStage.First : days < 10 ? PregnancyStage.Second : PregnancyStage.Third;
        }
        return IsRecovering(p, now) ? PregnancyStage.Recovering : PregnancyStage.None;
    }

    /// Turns pregnancy on with these roles. A carrier's cycle starts so that today is <paramref name="startDay"/>.
    public static string? Enable(PregnancyState p, bool carry, bool breed, int startDay, DateTime now)
    {
        if (!carry && !breed) return "Choose at least one role.";
        if (IsPregnant(p)) return "Roles can't change while you're pregnant.";
        p.Enabled = true;
        p.CanCarry = carry;
        p.CanBreed = breed;
        if (carry) p.CycleStartUtc = now.Date.AddDays(-(Math.Clamp(startDay, 1, CycleClock.Length) - 1));
        return null;
    }

    /// Ends everything: any pregnancy, the cycle and cravings. Births and visual choices are kept.
    public static void Disable(PregnancyState p)
    {
        p.Enabled = false;
        EndPregnancy(p, default);
        p.RecoveryUntilUtc = default;
        ClearCraving(p);
    }

    private static void EndPregnancy(PregnancyState p, DateTime restartCycleAt)
    {
        p.ConceivedUtc = null;
        p.DueUtc = null;
        p.FatherKey = null;
        if (restartCycleAt != default) p.CycleStartUtc = restartCycleAt;
    }

    private static void ClearCraving(PregnancyState p)
    {
        p.Craving = null;
        p.CravingDay = "";
        p.CravingMet = false;
        p.Unsatisfied = false;
        p.CravingSeconds = 0;
    }

    /// The chance a breeder's credited climax leads to conception now, from 0 to 0.9. <paramref name="titleFactor"/> is the
    /// carrier's own title and the partner's title together.
    public static double ConceptionChance(CharacterSave save, DateTime now, bool partnerVirile, int bondLevel, bool blocked,
        double titleFactor = 1)
    {
        var p = save.Pregnancy;
        if (blocked || !HasCycle(p, now) || ItemBook.PreventsConception(save, now)) return 0;
        var chance = CycleClock.PhaseOf(CycleClock.DayAt(p.CycleStartUtc, now)).ConceptionChance
                     * ItemBook.ConceptionFactor(save, now)
                     * (partnerVirile ? VirileFactor : 1)
                     * (bondLevel >= BondLevels.FertileLevel ? FertileBondFactor : 1)
                     * titleFactor;
        // The natural odds stop at 90%; the player's own multiplier can take them to certainty.
        var factor = Math.Clamp(p.ChanceFactor, MinChanceFactor, MaxChanceFactor);
        return Math.Min(1, Math.Min(ConceptionCap, chance) * factor);
    }

    public static void Conceive(PregnancyState p, DateTime now, string fatherKey)
    {
        p.ConceivedUtc = now;
        p.DueUtc = now.AddDays(DaysLong);
        p.FatherKey = fatherKey;
        p.Conceptions++;
    }

    /// Births when due, and the cycle starting again when recovery ends.
    public static PregnancyEvent Update(PregnancyState p, DateTime now)
    {
        if (!p.Enabled) return PregnancyEvent.None;
        if (IsPregnant(p) && now >= p.DueUtc)
        {
            p.Births++;
            p.BirthLog.Add(new BirthRecord
            {
                // Due while offline still counts as born on the due date.
                BornUtc = p.DueUtc!.Value < now ? p.DueUtc.Value : now,
                ConceivedUtc = p.ConceivedUtc!.Value,
                FatherKey = p.FatherKey ?? "",
            });
            EndPregnancy(p, default);
            ClearCraving(p);
            p.RecoveryUntilUtc = now.AddDays(RecoveryDays);
            return PregnancyEvent.Birth;
        }
        if (p.RecoveryUntilUtc != default && now >= p.RecoveryUntilUtc)
        {
            p.CycleStartUtc = p.RecoveryUntilUtc;
            p.RecoveryUntilUtc = default;
            return PregnancyEvent.RecoveryEnded;
        }
        return PregnancyEvent.None;
    }

    /// Everything pregnancy does to arousal gain now.
    public static double ArousalFactor(PregnancyState p, DateTime now)
    {
        if (!p.Enabled) return 1;
        var factor = StageOf(p, now) switch
        {
            PregnancyStage.First => 0.9,
            PregnancyStage.Second => 1.25,
            PregnancyStage.Third => 1.1,
            PregnancyStage.Recovering => 0.8,
            _ => HasCycle(p, now) ? CycleClock.PhaseOf(CycleClock.DayAt(p.CycleStartUtc, now)).ArousalFactor : 1,
        };
        return p.Unsatisfied ? factor * UnsatisfiedFactor : factor;
    }

    public static double TickXpFactor(PregnancyState p) => IsPregnant(p) ? PregnantTickXp : 1;

    // ── Instant items ─────────────────────────────────────────────────────────

    public static string? MorningAfter(PregnancyState p, DateTime now)
    {
        if (!IsPregnant(p)) return "You aren't pregnant.";
        if ((now - p.ConceivedUtc!.Value).TotalHours > MorningAfterHours) return "It's been more than 24 hours since you conceived.";
        EndPregnancy(p, now);
        ClearCraving(p);
        return null;
    }

    public static string? HormoneTea(PregnancyState p, DateTime now)
    {
        if (!HasCycle(p, now)) return IsPregnant(p) ? "You're pregnant." : "You don't have a cycle right now.";
        p.CycleStartUtc = now.AddDays(-3);
        return null;
    }

    public static string? SwiftLabour(PregnancyState p)
    {
        if (!IsPregnant(p)) return "You aren't pregnant.";
        p.DueUtc = p.DueUtc!.Value.AddDays(-SwiftLabourDays);
        return null;
    }
}
