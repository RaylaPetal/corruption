using System;
using Corruption.Core;
using Dalamud.Interface;

namespace Corruption.Windows;

/// How each item looks in the Shop and on the HUD.
public static class ItemIcons
{
    public static FontAwesomeIcon Of(string id) => id switch
    {
        ItemIds.Aphrodisiac => FontAwesomeIcon.Fire,
        ItemIds.NumbingBalm => FontAwesomeIcon.Snowflake,
        ItemIds.LustIncense => FontAwesomeIcon.Wind,
        ItemIds.Perfume => FontAwesomeIcon.Eye,
        ItemIds.FatigueSalve => FontAwesomeIcon.Leaf,
        ItemIds.AfterglowWine => FontAwesomeIcon.GlassCheers,
        ItemIds.HairTrigger => FontAwesomeIcon.Bolt,
        ItemIds.CumMagnet => FontAwesomeIcon.Magnet,
        ItemIds.StreakCharm => FontAwesomeIcon.CalendarCheck,
        ItemIds.FertilityCharm => FontAwesomeIcon.Seedling,
        ItemIds.VirilityTonic => FontAwesomeIcon.Flask,
        ItemIds.Contraceptive => FontAwesomeIcon.Ban,
        ItemIds.MorningAfter => FontAwesomeIcon.Sun,
        ItemIds.HormoneTea => FontAwesomeIcon.MugHot,
        ItemIds.SwiftLabour => FontAwesomeIcon.Forward,
        ItemIds.CravingTreat => FontAwesomeIcon.Cookie,
        _ => FontAwesomeIcon.Gem,
    };

    /// "12m left", "2 uses left".
    public static string Remaining(ActiveEffect effect, DateTime now)
        => effect.UntilUtc is { } until
            ? $"{ProfileTimers.Describe(until - now)} left"
            : effect.UsesLeft == 1 ? "1 use left" : $"{effect.UsesLeft} uses left";

    public static string Lasts(ItemDef item) => item.Kind switch
    {
        ItemKind.Timed => item.Duration is { TotalHours: >= 1 } d ? $"{d.TotalHours:0} h" : $"{item.Duration?.TotalMinutes:0} min",
        ItemKind.Counted => item.Uses == 1 ? "1 use" : $"{item.Uses} uses",
        _ => "Instant",
    };
}
