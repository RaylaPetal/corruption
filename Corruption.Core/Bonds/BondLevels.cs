namespace Corruption.Core;

/// A bond grows with verified scene time together. Each level adds a quirk, which only applies with the bonded partner.
public static class BondLevels
{
    public const int Max = 5;

    /// Seconds together each level starts at: when formed, then 1, 3, 6 and 12 hours.
    public static readonly IReadOnlyList<double> Thresholds = [0, 3600, 3 * 3600, 6 * 3600, 12 * 3600];

    public const int InTuneLevel = 2;
    public const int TogetherLevel = 3;
    public const int FertileLevel = 4;
    public const int DevotedLevel = 5;

    public static int LevelOf(double seconds)
    {
        var level = 1;
        while (level < Max && seconds >= Thresholds[level]) level++;
        return level;
    }

    /// Time until the next level, or null at the top.
    public static TimeSpan? UntilNext(double seconds)
    {
        var level = LevelOf(seconds);
        return level >= Max ? null : TimeSpan.FromSeconds(Thresholds[level] - seconds);
    }

    public static string QuirkOf(int level) => level switch
    {
        1 => "Bonded: ×1.25 XP together",
        2 => "In tune: arousal builds 10% faster together",
        3 => "Together: climaxing within 30 seconds of each other pays bonus XP and Sins",
        4 => "Fertile bond: conception is 1.5× as likely together",
        5 => "Devoted: ×1.35 XP and a halved refractory period together",
        _ => "",
    };
}
