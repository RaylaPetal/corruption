using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Corruption.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Corruption.Windows;

/// Everything this character has done, as tiles on the Profile page.
public sealed partial class MainWindow
{
    private bool storyOpen = true;

    private void DrawStory(SceneEngine engine)
    {
        var save = engine.Save;
        var now = DateTime.UtcNow;
        long Stat(GoalKind kind) => save.Goals.Stats.GetValueOrDefault(kind.ToString());

        Theme.CardBox("story", () =>
        {
            ImGui.SetNextItemOpen(storyOpen, ImGuiCond.Once);
            storyOpen = ImGui.CollapsingHeader("Your story");
            if (!storyOpen) return;

            var current = engine.Status.SceneStartedUtc is { } started ? (now - started).TotalSeconds : 0;
            Section(FontAwesomeIcon.Fire, "Scenes", Theme.Pink,
                (Duration(Stat(GoalKind.SceneSeconds)), "in scenes"),
                (Duration(Math.Max(save.LongestSceneSeconds, current)), "longest scene"),
                (Stat(GoalKind.ActiveDays).ToString(), "days with a scene"),
                (save.SeenPartners.Count.ToString(), "partners met"),
                (save.SeenActs.Count.ToString(), "animations tried"));

            Section(FontAwesomeIcon.Heart, "Climaxes", Theme.PinkSoft,
                (Stat(GoalKind.Climaxes).ToString(), "climaxes"),
                (Stat(GoalKind.PartnerClimaxes).ToString(), "partners' climaxes"),
                (Stat(GoalKind.ClimaxesTogether).ToString(), "together"),
                (Duration(Stat(GoalKind.EdgedSeconds)), "edged at 100"));

            Section(FontAwesomeIcon.Tint, "Loads", Theme.VioletSoft,
                (Stat(GoalKind.LayersReceived).ToString(), "loads taken"),
                (Stat(GoalKind.Climaxes).ToString(), "loads given"),
                (engine.LayersWorn.ToString(), "layers worn now"));
            DrawLayeredNow();

            Section(FontAwesomeIcon.Eye, "Watched", Theme.Violet,
                (Duration(Stat(GoalKind.WatchedSeconds)), "watched"),
                (Duration(Stat(GoalKind.CrowdSeconds)), "by a crowd of 3+"));

            var p = save.Pregnancy;
            if (p.Enabled || p.Births > 0 || p.Fathered > 0)
            {
                Section(FontAwesomeIcon.Baby, "Family", Theme.Pink,
                    (p.Conceptions.ToString(), "conceptions"),
                    (p.Births.ToString(), p.Births == 1 ? "baby born" : "babies born"),
                    (p.Fathered.ToString(), "children fathered"),
                    (Stat(GoalKind.CravingsSatisfied).ToString(), "cravings met"));
                DrawBirthLog(p);
            }

            Section(FontAwesomeIcon.Gem, "Sins", Theme.PinkSoft,
                (save.Wallet.Sins.ToString(), "now"),
                (save.Wallet.EarnedTotal.ToString(), "earned"),
                (save.Wallet.SpentTotal.ToString(), "spent"),
                (Stat(GoalKind.ItemsUsed).ToString(), "items used"),
                (save.Wallet.CheckInStreak.ToString(), "check-in streak"));

            var earned = GoalCatalog.Achievements.Count(a => save.Goals.Achievements.Contains(a.Id));
            Section(FontAwesomeIcon.Trophy, "Progress", Theme.Warning,
                (save.Level.ToString(), Progression.TierOf(save.Level).ToString()),
                (save.Prestige == 0 ? "0" : PrestigeRules.Label(save.Prestige, balance.StarsWithBonus), "stars"),
                ($"{earned}/{GoalCatalog.Achievements.Count}", "achievements"),
                (save.StreakDays.ToString(), "day streak"),
                (engine.Bonds.Level == 0 ? "-" : engine.Bonds.Level.ToString(), "bond level"));
        });
    }

    /// Every birth, newest first, with the father and how long it took.
    private static void DrawBirthLog(PregnancyState p)
    {
        if (p.BirthLog.Count == 0) return;
        foreach (var birth in Enumerable.Reverse(p.BirthLog))
        {
            Theme.Icon(FontAwesomeIcon.Baby, Theme.Pink);
            ImGui.SameLine();
            var father = birth.FatherKey.Length > 0 ? birth.FatherKey.Split('@')[0] : "unknown";
            ImGui.TextUnformatted($"{birth.BornUtc.ToLocalTime():d MMM yyyy}");
            ImGui.SameLine();
            Theme.MutedWrapped($"father {father}, after {(birth.BornUtc - birth.ConceivedUtc).TotalDays:0} days");
        }
        ImGuiHelpers.ScaledDummy(4f);
    }

    /// A heading and a row of tiles that wraps to the window.
    private static void Section(FontAwesomeIcon icon, string title, Vector4 color, params (string Value, string Label)[] tiles)
    {
        ImGuiHelpers.ScaledDummy(2f);
        Theme.Icon(icon, color);
        ImGui.SameLine();
        ImGui.TextColored(color, title);

        // Every tile in a section is as wide as its widest label or value needs, so a long label like "partners'
        // climaxes" fits and the row still lines up. Never wider than the window.
        var available = ImGui.GetContentRegionAvail().X;
        var width = MathF.Min(available, MathF.Max(104f * Scale, tiles.Max(t => TileContentWidth(t.Value, t.Label)) + 16f * Scale));
        var height = 46f * Scale;
        var gap = 6f * Scale;
        var startX = ImGui.GetCursorPosX();
        for (var i = 0; i < tiles.Length; i++)
        {
            if (i > 0)
            {
                var nextX = ImGui.GetItemRectMax().X - ImGui.GetWindowPos().X + gap;
                if (nextX + width <= startX + available) ImGui.SameLine(nextX);
            }
            Tile(tiles[i].Value, tiles[i].Label, color, new Vector2(width, height));
        }
    }

    /// The width a tile's text needs: its label, or its value at the value's larger size.
    private static float TileContentWidth(string value, string label)
    {
        ImGui.SetWindowFontScale(1.25f);
        var valueWidth = ImGui.CalcTextSize(value).X;
        ImGui.SetWindowFontScale(1f);
        return MathF.Max(valueWidth, ImGui.CalcTextSize(label).X);
    }

    private static void Tile(string value, string label, Vector4 color, Vector2 size)
    {
        var dl = ImGui.GetWindowDrawList();
        var at = ImGui.GetCursorScreenPos();
        dl.AddRectFilled(at, at + size, ImGui.GetColorU32(Theme.Alpha(color, 0.12f)), 8f * Scale);
        dl.AddRect(at, at + size, ImGui.GetColorU32(Theme.Alpha(color, 0.35f)), 8f * Scale);

        ImGui.SetWindowFontScale(1.25f);
        var valueSize = ImGui.CalcTextSize(value);
        dl.AddText(ImGui.GetFont(), ImGui.GetFontSize(), at + new Vector2((size.X - valueSize.X) / 2f, 5f * Scale), ImGui.GetColorU32(Theme.Text), value);
        ImGui.SetWindowFontScale(1f);
        var labelSize = ImGui.CalcTextSize(label);
        dl.AddText(at + new Vector2((size.X - labelSize.X) / 2f, size.Y - labelSize.Y - 5f * Scale), ImGui.GetColorU32(Theme.Muted), label);
        ImGui.Dummy(size);
    }

    /// Like 3h 12m, or 0m.
    private static string Duration(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m" : $"{span.Minutes}m";
    }
}
