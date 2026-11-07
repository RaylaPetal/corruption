using System;
using System.Collections.Generic;
using System.Numerics;
using Corruption.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Corruption.Windows;

/// Celebrates a goal, achievement, new tier, title or finished scene: one popup at a time near the top of the screen,
/// with a game sound.
public sealed class RewardPopup : Window
{
    private const float ShowSeconds = 5f;
    private const float FadeSeconds = 0.5f;
    private const int MaxQueued = 8;

    private const ImGuiWindowFlags PopupFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoInputs
        | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar;

    private readonly Settings settings;
    private readonly Queue<GoalReward> queue = new();
    private GoalReward? showing;
    private DateTime shownAt;

    public RewardPopup(Settings settings) : base("Corruption reward###CorruptionReward", PopupFlags)
    {
        this.settings = settings;
        RespectCloseHotkey = false;
        IsOpen = true;
    }

    public void Show(GoalReward reward)
    {
        // The scene summary has its own switch, and no sound of its own when it's off.
        if (reward.Kind == RewardKind.Scene && !settings.ShowScenePopup) return;
        if (!settings.ShowRewardPopup)
        {
            if (settings.PlayRewardSound) PlaySound(settings);
            return;
        }
        if (queue.Count < MaxQueued) queue.Enqueue(reward);
    }

    public static void PlaySound(Settings settings) => UIGlobals.PlayChatSoundEffect((uint)Math.Clamp(settings.RewardSound, 1, 16));

    public override bool DrawConditions()
    {
        var now = DateTime.UtcNow;
        if (showing is not null && (now - shownAt).TotalSeconds >= ShowSeconds) showing = null;
        if (showing is null && queue.Count > 0)
        {
            showing = queue.Dequeue();
            shownAt = now;
            if (settings.PlayRewardSound) PlaySound(settings);
        }
        return showing is not null;
    }

    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.Pos + new Vector2(viewport.Size.X / 2f, viewport.Size.Y * 0.14f), ImGuiCond.Always, new Vector2(0.5f, 0f));
        ImGui.SetNextWindowBgAlpha(Fade() * 0.97f);
    }

    private float Fade()
    {
        var age = (float)(DateTime.UtcNow - shownAt).TotalSeconds;
        return Math.Clamp(MathF.Min(age / FadeSeconds, (ShowSeconds - age) / FadeSeconds), 0f, 1f);
    }

    public override void Draw()
    {
        using var content = Theme.Content();
        if (showing is not { } reward) return;
        using var alpha = ImRaii.PushStyle(ImGuiStyleVar.Alpha, Fade());

        var scale = ImGuiHelpers.GlobalScale;
        var (icon, color, heading) = reward.Kind switch
        {
            RewardKind.Achievement => (FontAwesomeIcon.Trophy, Theme.Warning, "Achievement unlocked"),
            RewardKind.Weekly => (FontAwesomeIcon.CalendarAlt, Theme.Violet, "Weekly goal complete"),
            RewardKind.Tier => (FontAwesomeIcon.Fire, Theme.TierColor(Enum.TryParse<Tier>(reward.Title, out var tier) ? tier : Tier.Pure), "New tier"),
            RewardKind.Title => (FontAwesomeIcon.Crown, Theme.Warning, "Title unlocked"),
            RewardKind.Scene => (FontAwesomeIcon.Heart, Theme.Pink, "Scene over"),
            _ => (FontAwesomeIcon.Sun, Theme.Pink, "Daily goal complete"),
        };

        ImGui.Dummy(new Vector2(280 * scale, 0));
        ImGui.SetWindowFontScale(1.5f);
        Theme.Icon(icon, color);
        ImGui.SetWindowFontScale(1f);
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextColored(color, heading);
        if (reward.Kind != RewardKind.Scene)
        {
            ImGui.SetWindowFontScale(1.2f);
            ImGui.TextUnformatted(reward.Title);
            ImGui.SetWindowFontScale(1f);
        }
        if (reward.Xp > 0) Theme.MutedText($"+{reward.Xp:0} XP");
        if (reward.Detail is { } detail)
            foreach (var line in detail.Split('\n'))
                Theme.MutedText(line);
        ImGui.EndGroup();
    }
}
