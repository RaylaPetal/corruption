using System;
using System.Collections.Generic;
using System.Linq;
using Corruption.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Corruption.Windows;

/// The Goals page: dailies, weeklies and achievements.
public sealed partial class MainWindow
{
    private void DrawGoals(SceneEngine engine)
    {
        var data = engine.Save.Goals;
        var now = DateTime.UtcNow;
        Theme.CardBox("daily", () =>
        {
            Theme.Heading(FontAwesomeIcon.Sun, "Daily");
            DrawGoalList(data.Daily);
            Theme.MutedText($"New goals in {Remaining(now.Date.AddDays(1) - now)}");
        }, stripe: Theme.Pink);

        Theme.CardBox("weekly", () =>
        {
            Theme.Heading(FontAwesomeIcon.CalendarAlt, "Weekly");
            DrawGoalList(data.Weekly);
            Theme.MutedText($"New goals in {Remaining(GoalBook.StartOfWeek(now).AddDays(7) - now)}");
        }, stripe: Theme.Violet);

        var earned = GoalCatalog.Achievements.Count(a => data.Achievements.Contains(a.Id));
        Theme.CardBox("achievements", () =>
        {
            Theme.Heading(FontAwesomeIcon.Trophy, $"Achievements: {earned} of {GoalCatalog.Achievements.Count}");
            foreach (var achievement in GoalCatalog.Shown(engine.Save).OrderByDescending(a => data.Achievements.Contains(a.Id)))
                DrawAchievement(engine.Save, achievement, data.Achievements.Contains(achievement.Id));
        });
    }

    private static void DrawGoalList(List<GoalState> goals)
    {
        if (goals.Count == 0) Theme.MutedText("Your goals appear once you're logged in.");
        foreach (var goal in goals)
        {
            if (GoalCatalog.Find(goal.Id) is not { } def) continue;
            Theme.Icon(goal.Done ? FontAwesomeIcon.CheckCircle : FontAwesomeIcon.Circle, goal.Done ? Theme.Good : Theme.Faint);
            ImGui.SameLine();
            if (goal.Done) ImGui.TextColored(Theme.Good, def.Text);
            else ImGui.TextUnformatted(def.Text);
            ImGui.SameLine();
            Theme.MutedWrapped($"+{def.Xp:0} XP");
            if (!goal.Done)
                Theme.GradientBar((float)goal.Progress / def.Target, Theme.VioletDeep, Theme.Pink, ProgressText(def.Kind, goal.Progress, def.Target), 14f * Scale);
        }
    }

    private static void DrawAchievement(CharacterSave save, AchievementDef achievement, bool earned)
    {
        Theme.Icon(earned ? FontAwesomeIcon.Trophy : FontAwesomeIcon.Lock, earned ? Theme.Warning : Theme.Faint);
        ImGui.SameLine();
        if (achievement.Hidden && !earned)
        {
            ImGui.TextColored(Theme.Faint, "???");
            ImGui.SameLine();
            Theme.MutedWrapped("A hidden achievement");
            return;
        }
        if (earned) ImGui.TextColored(Theme.PinkSoft, achievement.Name);
        else ImGui.TextUnformatted(achievement.Name);
        ImGui.SameLine();
        Theme.MutedWrapped(achievement.Text);
        if (TitleCatalog.Find(achievement.Id) is { } title)
        {
            ImGui.SameLine();
            Theme.Icon(FontAwesomeIcon.Crown, earned ? new System.Numerics.Vector4(PlateComposer.TitleColor(title), 1f) : Theme.Faint);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Unlocks the {title.Name} title: {(title.IsCosmetic ? "just for show" : title.Effect)}.");
        }
        if (!earned && achievement.Target > 1)
        {
            var progress = Math.Min(achievement.Progress(save), achievement.Target);
            Theme.GradientBar((float)progress / achievement.Target, Theme.VioletDeep, Theme.Violet, $"{progress} / {achievement.Target}", 12f * Scale);
        }
    }

    private static string ProgressText(GoalKind kind, long progress, long target)
        => kind is GoalKind.SceneSeconds or GoalKind.WatchedSeconds
            ? $"{progress / 60} / {target / 60} min"
            : $"{progress} / {target}";

    private static string Remaining(TimeSpan span)
        => span.TotalDays >= 1 ? $"{(int)span.TotalDays}d {span.Hours}h" : $"{span.Hours}h {span.Minutes}m";
}
