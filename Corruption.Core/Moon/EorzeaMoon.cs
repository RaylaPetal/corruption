namespace Corruption.Core;

/// Eorzea's moon, eight phases of four Eorzean days each. Saved by name nowhere, so the order can't drift.
public enum MoonPhase
{
    NewMoon, WaxingCrescent, FirstQuarter, WaxingGibbous, FullMoon, WaningGibbous, LastQuarter, WaningCrescent,
}

/// The moon at one moment: its phase, which cycle it's in (to count each Full Moon once), when the phase ends, and the
/// next event after this phase.
public readonly record struct MoonInfo(MoonPhase Phase, long Cycle, DateTime PhaseEndsUtc, MoonPhase NextEvent, DateTime NextEventUtc)
{
    public bool IsEvent => EorzeaMoon.IsEvent(Phase);
}

/// The moon worked out from real time through Eorzea time, so every client agrees with nothing shared. An Eorzean day is
/// 70 real minutes, so a phase lasts 4 hours 40 minutes and a cycle 37 hours 20 minutes.
public static class EorzeaMoon
{
    /// Eorzea time runs this many times faster than real time.
    public const double EorzeaPerReal = 3600.0 / 175.0;

    public const int DaysPerPhase = 4;
    public const int PhasesPerCycle = 8;
    public const int DaysPerCycle = DaysPerPhase * PhasesPerCycle;

    public static bool IsEvent(MoonPhase phase) => phase is MoonPhase.FullMoon or MoonPhase.NewMoon;

    public static string Name(MoonPhase phase) => phase switch
    {
        MoonPhase.NewMoon => "New Moon",
        MoonPhase.WaxingCrescent => "Waxing Crescent",
        MoonPhase.FirstQuarter => "First Quarter",
        MoonPhase.WaxingGibbous => "Waxing Gibbous",
        MoonPhase.FullMoon => "Full Moon",
        MoonPhase.WaningGibbous => "Waning Gibbous",
        MoonPhase.LastQuarter => "Last Quarter",
        _ => "Waning Crescent",
    };

    /// What a moon event does, or empty for a phase without one.
    public static string Effect(MoonPhase phase, Balance b) => phase switch
    {
        MoonPhase.FullMoon => $"Arousal builds ×{b.FullMoonArousalFactor:0.##} as fast",
        MoonPhase.NewMoon => $"Scene time pays ×{b.NewMoonSceneSinsFactor:0.##} Sins",
        _ => "",
    };

    /// An Eorzean day in real time: 86,400 Eorzea seconds at 3600/175 times real speed is exactly 70 minutes.
    private static readonly long TicksPerDay = TimeSpan.FromSeconds(4200).Ticks;

    /// The Eorzean day number at a real moment, counted from the Unix epoch, plus the calibration offset. Whole ticks, so
    /// a phase's end always lands exactly on the next phase.
    private static long DayAt(DateTime utc, Balance b)
        => FloorDiv((utc - DateTime.UnixEpoch).Ticks, TicksPerDay) + b.MoonOffsetDays;

    /// The real moment an Eorzean day (as counted by <see cref="DayAt"/>) begins.
    private static DateTime StartOf(long day, Balance b)
        => new DateTime(DateTime.UnixEpoch.Ticks + (day - b.MoonOffsetDays) * TicksPerDay, DateTimeKind.Utc);

    private static long FloorDiv(long a, long b) => a >= 0 ? a / b : -((-a + b - 1) / b);

    public static MoonInfo At(DateTime utc, Balance b)
    {
        var day = DayAt(utc, b);
        var cycle = FloorDiv(day, DaysPerCycle);
        var inCycle = day - cycle * DaysPerCycle;
        var phase = (MoonPhase)(inCycle / DaysPerPhase);
        var phaseStart = cycle * DaysPerCycle + (int)phase * DaysPerPhase;
        var phaseEnds = StartOf(phaseStart + DaysPerPhase, b);

        // The next event after this phase: walk forward a phase at a time.
        var next = phaseStart + DaysPerPhase;
        while (!IsEvent((MoonPhase)((next - FloorDiv(next, DaysPerCycle) * DaysPerCycle) / DaysPerPhase))) next += DaysPerPhase;
        var nextPhase = (MoonPhase)((next - FloorDiv(next, DaysPerCycle) * DaysPerCycle) / DaysPerPhase);
        return new MoonInfo(phase, cycle, phaseEnds, nextPhase, StartOf(next, b));
    }
}
