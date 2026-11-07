using System;
using Corruption.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Components;

namespace Corruption.Windows;

/// The Settings page: safeword, windows, alerts, bridges, welcome and reset.
public sealed partial class MainWindow
{
    private void DrawSettings(SceneEngine engine)
    {
        Theme.CardBox("safeword", () =>
        {
            Theme.Heading(FontAwesomeIcon.HandPaper, "Safeword");
            DrawSafewordKey();
            Theme.MutedWrapped("Stops scene XP and arousal at once, and tells nearby players you've paused. /corr resume to continue.");
        });

        Theme.CardBox("hud", () =>
        {
            Theme.Heading(FontAwesomeIcon.Desktop, "Windows");
            var show = settings.ShowHud;
            if (ImGui.Checkbox("Show HUD", ref show))
            {
                settings.ShowHud = show;
                settings.Save();
            }
            ImGui.SameLine();
            var locked = settings.HudLocked;
            if (ImGui.Checkbox("Lock", ref locked))
            {
                settings.HudLocked = locked;
                settings.Save();
            }
            ImGui.SameLine();
            var size = settings.HudSize;
            ImGui.SetNextItemWidth(100 * Scale);
            if (ImGui.Combo("Size", ref size, HudSizes, HudSizes.Length))
            {
                settings.HudSize = size;
                settings.Save();
            }
            var duringScenes = settings.HudDuringScenes;
            if (ImGui.Checkbox("Show the HUD during scenes, even when it's off", ref duringScenes))
            {
                settings.HudDuringScenes = duringScenes;
                settings.Save();
            }
            var always = settings.ShowInCutscenesAndGpose;
            if (ImGui.Checkbox("Show windows in cutscenes and GPose", ref always))
            {
                settings.ShowInCutscenesAndGpose = always;
                settings.Save();
                Plugin.ApplyUiHide(settings);
            }
        });

        Theme.CardBox("rewards", () =>
        {
            Theme.Heading(FontAwesomeIcon.Trophy, "Alerts");
            var show = settings.ShowRewardPopup;
            if (ImGui.Checkbox("Show a popup", ref show))
            {
                settings.ShowRewardPopup = show;
                settings.Save();
            }
            ImGui.SameLine();
            var sound = settings.PlayRewardSound;
            if (ImGui.Checkbox("Play a sound", ref sound))
            {
                settings.PlayRewardSound = sound;
                settings.Save();
            }
            var scenes = settings.ShowScenePopup;
            if (ImGui.Checkbox("Sum up each scene of a minute or more", ref scenes))
            {
                settings.ShowScenePopup = scenes;
                settings.Save();
            }
            Theme.MutedText("Popups for goals, achievements, new tiers and titles, and the scene summary.");
            ImGui.SetNextItemWidth(180 * Scale);
            var effect = settings.RewardSound;
            if (ImGui.SliderInt("Sound", ref effect, 1, 16, "<se.%d>"))
            {
                settings.RewardSound = effect;
                settings.Save();
            }
            if (ImGui.IsItemDeactivatedAfterEdit()) RewardPopup.PlaySound(settings);
            ImGui.SameLine();
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Play, "Test"))
            {
                if (settings.ShowRewardPopup) popup.Show(new GoalReward(RewardKind.Achievement, "Worked Up", "", 500));
                else RewardPopup.PlaySound(settings);
            }
            if (settings.ShowRewardPopup)
            {
                ImGui.SameLine();
                if (ImGui.Button("Tier")) popup.Show(new GoalReward(RewardKind.Tier, nameof(Tier.Tainted), "", 0, "Level 40"));
                ImGui.SameLine();
                if (ImGui.Button("Title")) popup.Show(new GoalReward(RewardKind.Title, "Insatiable", "", 0, "Refractory period ×0.75"));
                ImGui.SameLine();
                if (ImGui.Button("Scene"))
                    popup.Show(SceneSummaries.Reward(new SceneSummary
                    {
                        VerifiedSeconds = 860, Xp = 412, Sins = 18, OwnClimaxes = 1, PartnerClimaxes = 1, PeakWatchers = 3, Partners = ["Someone@World"],
                    }));
            }
            Theme.MutedText("The sound follows the game's system sound volume.");
        });

        DrawBridges(engine);

        Theme.CardBox("welcomeAgain", () =>
        {
            Theme.Heading(FontAwesomeIcon.Star, "Welcome");
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Redo, "Go through the welcome again")) OpenWelcome?.Invoke();
            Theme.MutedText("Anatomy, pregnancy, layering and display, a step at a time. Your locked profile stays.");
        });

        Theme.CardBox("progress", () =>
        {
            Theme.Heading(FontAwesomeIcon.ExclamationTriangle, "Profile");
            if (!confirmingReset)
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Undo, "Reset this character's profile")) confirmingReset = true;
            }
            else
            {
                ImGui.TextColored(Theme.Warning, "Back to level 1 with no XP, streak, stars or bond, and you choose your setup again.");
                Theme.MutedText("Other characters aren't affected.");
                if (Theme.DangerButton(FontAwesomeIcon.Undo, "Yes, reset"))
                {
                    engine.ResetProgress(DateTime.UtcNow);
                    confirmingReset = false;
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel")) confirmingReset = false;
            }
        });
    }

    private void DrawSafewordKey()
    {
        var ctrl = settings.SafewordCtrl;
        if (ImGui.Checkbox("Ctrl", ref ctrl))
        {
            settings.SafewordCtrl = ctrl;
            settings.Save();
        }
        ImGui.SameLine();
        var shift = settings.SafewordShift;
        if (ImGui.Checkbox("Shift", ref shift))
        {
            settings.SafewordShift = shift;
            settings.Save();
        }
        ImGui.SameLine();
        var alt = settings.SafewordAlt;
        if (ImGui.Checkbox("Alt", ref alt))
        {
            settings.SafewordAlt = alt;
            settings.Save();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(130 * Scale);
        if (ImGui.BeginCombo("Key", settings.SafewordKey.ToString()))
        {
            foreach (var key in Plugin.KeyState.GetValidVirtualKeys())
            {
                if (key is VirtualKey.NO_KEY or VirtualKey.CONTROL or VirtualKey.SHIFT or VirtualKey.MENU) continue;
                if (ImGui.Selectable(key.ToString(), key == settings.SafewordKey))
                {
                    settings.SafewordKey = key;
                    settings.Save();
                }
            }
            ImGui.EndCombo();
        }
    }
}
