using System.Globalization;

namespace Corruption.Core;

/// What a pregnant character craves today. Saved as a number, so only ever add kinds at the end.
public enum CravingKind { BondedScene, Climax, TenMinutes, Watched, NewAnimation, Layer, Creampie }

/// One craving per UTC day while pregnant. An unmet one leaves the character Unsatisfied until the next is met.
public static class CravingBook
{
    public const double TenMinutesSeconds = 600;
    public const double SatisfiedArousal = 10;

    public static string Describe(CravingKind kind) => kind switch
    {
        CravingKind.BondedScene => "A scene with your bonded partner",
        CravingKind.Climax => "A climax",
        CravingKind.TenMinutes => "Ten minutes in scenes",
        CravingKind.Watched => "Being watched in a scene",
        CravingKind.NewAnimation => "An animation you haven't played today",
        CravingKind.Creampie => "Taking a creampie",
        _ => "Being layered by a partner",
    };

    /// Picks today's craving when the day has changed. Returns it when a new one was picked.
    /// A creampie is only craved by a character who has had Cummy version 6 connected.
    public static CravingKind? Roll(PregnancyState p, DateTime now, int seed, bool bonded, bool creampies = false)
    {
        if (!PregnancyBook.IsConfirmed(p)) return null;
        var day = now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (p.CravingDay == day) return null;
        if (p.Craving is not null && !p.CravingMet) p.Unsatisfied = true;

        var kinds = Enum.GetValues<CravingKind>().Where(k => (bonded || k != CravingKind.BondedScene) && (creampies || k != CravingKind.Creampie)).ToArray();
        var hash = 17;
        foreach (var c in day) hash = unchecked(hash * 31 + c);
        p.Craving = kinds[new Random(seed ^ hash).Next(kinds.Length)];
        p.CravingDay = day;
        p.CravingMet = false;
        p.CravingSeconds = 0;
        return p.Craving;
    }

    /// Something happened that today's craving may be. True when it satisfied it.
    public static bool Met(PregnancyState p, CravingKind kind)
    {
        if (!PregnancyBook.IsConfirmed(p) || p.Craving != kind || p.CravingMet) return false;
        p.CravingMet = true;
        p.Unsatisfied = false;
        return true;
    }

    /// A Craving Treat: satisfies whatever today's craving is.
    public static bool Treat(PregnancyState p) => p.Craving is { } kind && Met(p, kind);
}
