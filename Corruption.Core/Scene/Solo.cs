namespace Corruption.Core;

/// The fixed numbers of solo scenes.
public static class SoloRules
{
    /// Solo ticks and climaxes pay this share of a duo scene's.
    public const double TickXp = 0.5;
    public const double ClimaxXp = 0.5;

    /// Verified solo minutes a day, before titles and Do Not Disturb Signs.
    public const double DailyMinutes = 45;

    /// The ticker's partner key for a solo scene, so a partner arriving starts a fresh tick.
    public const string TickerKey = "<solo>";
}

/// One character's solo time today.
public static class SoloBook
{
    /// What a Do Not Disturb Sign adds to today.
    public const double SignSeconds = 20 * 60;

    /// Starts the count again on a new UTC day.
    public static void Rollover(CharacterSave save, DateTime now)
    {
        if (save.SoloDayUtc == now.Date) return;
        save.SoloDayUtc = now.Date;
        save.SoloSecondsToday = 0;
        save.SoloBonusSecondsToday = 0;
    }

    /// Today's whole budget: the daily minutes, titles' extra minutes, and today's signs.
    public static double BudgetSeconds(CharacterSave save, Modifiers mods)
        => (SoloRules.DailyMinutes + mods.SoloBudgetMinutes) * 60 + save.SoloBonusSecondsToday;

    public static double SecondsLeft(CharacterSave save, DateTime now, Modifiers mods)
    {
        Rollover(save, now);
        return Math.Max(0, BudgetSeconds(save, mods) - save.SoloSecondsToday);
    }

    /// Another verified solo second.
    public static void Spend(CharacterSave save, DateTime now, double seconds = 1)
    {
        Rollover(save, now);
        save.SoloSecondsToday += seconds;
    }
}
