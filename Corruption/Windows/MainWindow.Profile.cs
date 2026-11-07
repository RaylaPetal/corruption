using System;
using System.Collections.Generic;
using System.Numerics;
using Corruption.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Corruption.Windows;

/// The Profile page: the scene card, your profile and safeword name.
public sealed partial class MainWindow
{
    private void DrawProfile(SceneEngine engine)
    {
        DrawSceneCard(engine);
        DrawLastSceneCard(engine);
        DrawTitleCard(engine);
        DrawStatusCard(engine);
        DrawPregnancyCard(engine);
        DrawProfileCard(engine);
        DrawPrestigeCard(engine);
        DrawStory(engine);
    }

    /// One glance: what's happening, and only what's worth saying about it.
    private void DrawSceneCard(SceneEngine engine)
    {
        var save = engine.Save;
        var status = engine.Status;
        var color = Theme.StateColor(status.State);

        Theme.CardBox("scene", () =>
        {
            var button = engine.IsPaused ? "Resume" : "Stop";
            var icon = engine.IsPaused ? FontAwesomeIcon.Play : FontAwesomeIcon.HandPaper;

            ImGui.AlignTextToFramePadding();
            Theme.Icon(status.State == SceneState.Earning ? FontAwesomeIcon.Fire : status.State == SceneState.Paused ? FontAwesomeIcon.HandPaper : FontAwesomeIcon.Heart, color);
            ImGui.SameLine();
            ImGui.TextColored(color, status.State switch
            {
                SceneState.Earning => status.Partner is { } with ? $"In a scene with {with.Name}" : "In a scene",
                SceneState.Paused => "Paused",
                SceneState.Waiting => SceneCheck.Describe(status.Failure),
                _ => "No scene right now",
            });

            Theme.AlignRight(Theme.IconButtonWidth(icon, button));
            if (engine.IsPaused)
            {
                if (Theme.PrimaryButton(icon, button)) engine.Resume();
            }
            else if (Theme.DangerButton(icon, button))
            {
                scene.Pause();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(engine.IsPaused ? "Carry on." : $"Safeword: stops XP and arousal at once. {SafewordName()}.");

            if (status.State == SceneState.Idle)
                Theme.MutedText("Play an animation from a picked mod, close to someone else doing the same.");
            if (!penumbra.IsAvailable)
                ImGui.TextColored(Theme.Warning, "Penumbra is unavailable, so no acts can be detected.");

            if (status.Act is { } act)
            {
                Theme.Pill(act.DisplayName, Theme.Violet);
                if (status.SceneStartedUtc is { } started)
                {
                    var elapsed = DateTime.UtcNow - started;
                    ImGui.SameLine();
                    Theme.Pill($"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}", Theme.Muted);
                }
                if (status.Fatigue < 1)
                {
                    ImGui.SameLine();
                    Theme.Pill($"Fatigue {status.Fatigue:P0}", Theme.Warning);
                }
                if (Arousal.InRefractory(save, DateTime.UtcNow))
                {
                    ImGui.SameLine();
                    Theme.Pill("Recovering", Theme.PinkSoft);
                }
            }

            DrawExcludeCurrent(engine);
            if (status.State == SceneState.Earning) DrawBonuses(engine);
            if (status.State == SceneState.Waiting && ImGui.TreeNode("Scene checks"))
            {
                DrawConditions(status);
                ImGui.TreePop();
            }
        }, stripe: color);
    }

    /// Only the bonuses in effect, so a plain scene stays plain.
    private void DrawBonuses(SceneEngine engine)
    {
        var save = engine.Save;
        var status = engine.Status;
        // The last tick's factors, which include the bond, layers and prestige.
        var factors = status.Factors;

        var perTick = balance.TickXp * factors.Total * (save.Setup?.XpMultiplier ?? 1);
        Theme.Pill($"{perTick:0.#} XP per tick", Theme.Pink);
        Bonus("Your tier", factors.Level, Theme.Violet);
        Bonus("Partner's tier", factors.Partner, Theme.PinkSoft);
        Bonus(status.Watchers == 1 ? "1 watcher" : $"{status.Watchers} watchers", factors.Watchers, Theme.Pink);
        Bonus($"{save.StreakDays} day streak", factors.Streak, Theme.Good);
        Bonus("Bonded", factors.Bonded, Theme.Pink);
        Bonus(engine.LayersWorn == 1 ? "1 layer" : $"{engine.LayersWorn} layers", factors.Marked, Theme.PinkSoft);
        Bonus(PrestigeRules.Label(save.Prestige, balance.StarsWithBonus), factors.Prestige, Theme.Warning);
        Bonus(TitleBook.Equipped(save)?.Name ?? "Title", factors.Title, Theme.Warning);
        // A title whose bonus isn't scene XP still shows, so the player can see it's working.
        if (TitleBook.Equipped(save) is { IsCosmetic: false } title && factors.Title <= 1)
        {
            ImGui.SameLine();
            Theme.Pill(title.Name, Theme.Warning);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(title.Effect);
        }
        if (status.Partner is { } partner && engine.HasChemistryWith(partner))
        {
            ImGui.SameLine();
            Theme.Pill($"Chemistry x{PersonaCatalog.ChemistryXp:0.##}", Theme.Pink);
        }
        if (engine.IsPentUp)
        {
            ImGui.SameLine();
            Theme.Pill($"Pent Up, {Math.Ceiling(save.PentUpSecondsLeft / 60):0}m left", Theme.Warning);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"A day without a scene: ×{PersonaCatalog.PentUpXp:0.##} XP until you've had more time in scenes.");
        }
        var moon = engine.MoonAt(DateTime.UtcNow);
        if (moon.IsEvent)
        {
            ImGui.SameLine();
            Theme.Pill(EorzeaMoon.Name(moon.Phase), Theme.VioletSoft);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(EorzeaMoon.Effect(moon.Phase, balance));
        }
    }

    /// The last scene's totals, kept until the next one ends.
    private void DrawLastSceneCard(SceneEngine engine)
    {
        if (engine.Save.LastScene is not { } last || engine.Status.SceneStartedUtc is not null) return;
        Theme.CardBox("lastScene", () =>
        {
            Theme.Heading(FontAwesomeIcon.History, "Last scene");
            foreach (var line in SceneSummaries.Lines(last)) ImGui.TextUnformatted(line);
            Theme.MutedText($"Ended {Ago(DateTime.UtcNow - last.EndedUtc)}");
        });
    }

    private static string Ago(TimeSpan span)
        => span.TotalMinutes < 1 ? "just now"
            : span.TotalHours < 1 ? $"{(int)span.TotalMinutes}m ago"
            : span.TotalDays < 1 ? $"{(int)span.TotalHours}h ago"
            : $"{(int)span.TotalDays}d ago";

    private static void Bonus(string label, double factor, Vector4 color)
    {
        if (factor <= 1) return;
        ImGui.SameLine();
        Theme.Pill($"{label} x{factor:0.##}", color);
    }

    private void DrawProfileCard(SceneEngine engine)
    {
        var save = engine.Save;
        if (save.Setup is not { } setup) return;

        Theme.CardBox("profile", () =>
        {
            Theme.Heading(FontAwesomeIcon.Star, "Your profile");

            Theme.Pill(setup.Difficulty.ToString(), Theme.Pink);
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            Theme.MutedText(ProfileSetup.Describe(setup.Difficulty));

            Theme.Pill($"Decay: {setup.Decay}", Theme.Violet);
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            Theme.MutedText(ProfileSetup.Describe(setup.Decay));

            Theme.Pill(setup.AutoClimax ? "Climax: automatic" : "Climax: by hand", Theme.PinkSoft);

            Theme.Pill($"Arousal gain: {setup.ArousalGain:0.##}x", Theme.Pink);
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            Theme.MutedText($"About {ClimaxMinutes(setup.ArousalGain):0.#} minutes to climax in a quiet scene.");

            DrawPersona(save);

            ImGuiHelpers.ScaledDummy(2f);
            Theme.Icon(FontAwesomeIcon.Lock, Theme.Faint);
            ImGui.SameLine();
            Theme.MutedText("Locked until you reset your profile in Settings.");

            ImGuiHelpers.ScaledDummy(2f);
            Theme.Pill($"{save.SeenPartners.Count} partners met", Theme.PinkSoft);
            ImGui.SameLine();
            Theme.Pill($"{save.SeenActs.Count} animations tried", Theme.Violet);
            ImGui.SameLine();
            Theme.Pill(save.StreakDays == 1 ? "1 day streak" : $"{save.StreakDays} day streak", Theme.Good);
        });
    }

    /// The personality and traits, each with what it does. A trait missing its pregnancy role says so.
    private static void DrawPersona(CharacterSave save)
    {
        ImGuiHelpers.ScaledDummy(2f);
        var personality = PersonaBook.Personality(save);
        Theme.Pill(save.Setup?.Personality is null ? "Personality: not chosen yet" : $"Personality: {personality.Name}", Theme.Violet);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        Theme.MutedText(personality.Upside.Length == 0
            ? save.Setup?.Personality is null ? "Plays as Easygoing until you choose, in the welcome window." : "No bonus and no drawback"
            : $"{personality.Upside}. {personality.Downside}.");
        foreach (var trait in PersonaBook.Traits(save))
        {
            var active = PersonaBook.IsActive(save, trait);
            Theme.Pill(trait.Name, active ? Theme.PinkSoft : Theme.Faint);
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            Theme.MutedText(active ? $"{trait.Upside}. {trait.Downside}."
                : trait.Role == ItemRole.Carry ? "Needs the carry role: turn pregnancy on as a carrier in Settings."
                : "Needs the breed role: turn pregnancy on as a breeder in Settings.");
        }
    }

    /// Each condition passes until the check's first failure, which is the one to fix. Later ones aren't known yet.
    private static void DrawConditions(SceneStatus status)
    {
        if (status.Failure == SceneFailure.Paused)
        {
            Theme.Icon(FontAwesomeIcon.HandPaper, Theme.Danger);
            ImGui.SameLine();
            ImGui.TextColored(Theme.Danger, "Paused");
            return;
        }

        var failedAt = Array.FindIndex(Conditions, c => c.Failure == status.Failure);
        for (var i = 0; i < Conditions.Length; i++)
        {
            var passed = failedAt < 0 || i < failedAt;
            var failed = i == failedAt;
            Theme.Icon(passed ? FontAwesomeIcon.CheckCircle : failed ? FontAwesomeIcon.TimesCircle : FontAwesomeIcon.Circle,
                passed ? Theme.Good : failed ? Theme.Warning : Theme.Faint);
            ImGui.SameLine();
            ImGui.TextColored(passed ? Theme.Text : failed ? Theme.Warning : Theme.Faint, Conditions[i].Text);
        }
    }

    private string SafewordName()
    {
        var parts = new List<string>();
        if (settings.SafewordCtrl) parts.Add("Ctrl");
        if (settings.SafewordShift) parts.Add("Shift");
        if (settings.SafewordAlt) parts.Add("Alt");
        parts.Add(settings.SafewordKey.ToString());
        return string.Join("+", parts);
    }
}
