using System;
using System.Numerics;
using Corruption.Core;
using Corruption.Scene;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace Corruption.Windows;

/// The optional at-a-glance view. Hidden in cutscenes and GPose along with the rest of the UI, by Dalamud.
public sealed class HudWindow : Window
{
    private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize
        | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoSavedSettings;

    private const float BaseWidth = 230f;

    private static readonly float[] Scales = [0.85f, 1f, 1.25f];

    private readonly Settings settings;
    private readonly SceneService scene;
    private readonly Balance balance;
    private readonly Action openMainWindow;

    public HudWindow(Settings settings, SceneService scene, Balance balance, Action openMainWindow) : base("Corruption HUD###CorruptionHud", BaseFlags)
    {
        this.settings = settings;
        this.scene = scene;
        this.balance = balance;
        this.openMainWindow = openMainWindow;
        RespectCloseHotkey = false;
        IsOpen = true;
        Position = new Vector2(settings.HudX, settings.HudY);
        PositionCondition = ImGuiCond.Appearing;
    }

    public override bool DrawConditions() => settings.ShowHud && scene.Engine is not null;

    public override void PreDraw()
        => Flags = settings.HudLocked ? BaseFlags | ImGuiWindowFlags.NoMove : BaseFlags;

    public override void Draw()
    {
        if (scene.Engine is not { } engine) return;
        var size = Scales[Math.Clamp(settings.HudSize, 0, Scales.Length - 1)];
        ImGui.SetWindowFontScale(size);
        var width = BaseWidth * size * ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(width, 0));

        var save = engine.Save;
        var status = engine.Status;
        var tier = Progression.TierOf(save.Level);
        var stateColor = Theme.StateColor(status.State);

        Theme.StatusPill(Theme.StateName(status.State), stateColor);
        var rightEdge = ImGui.GetItemRectMin().X + width;
        var label = $"{tier} ♥ Lv. {save.Level}";
        if (save.Prestige > 0) label += $" {PrestigeRules.Label(save.Prestige, balance.StarsWithBonus)}";
        ImGui.SameLine();
        var labelWidth = Theme.PillWidth(label);
        var buttonWidth = Theme.IconOnlyButtonWidth(FontAwesomeIcon.Bars);
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        ImGui.SetCursorScreenPos(new Vector2(rightEdge - labelWidth - spacing - buttonWidth, ImGui.GetCursorScreenPos().Y));
        Theme.Pill(label, Theme.TierColor(tier));
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Bars)) openMainWindow();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open Corruption");

        Theme.XpBar(save.Xp, Progression.XpToNext(save.Level, balance), width: width);
        Theme.ArousalBar(save.Arousal, width: width);

        if (status.State is SceneState.Waiting or SceneState.Paused)
            ImGui.TextColored(stateColor, SceneCheck.Describe(status.Failure));

        if (status.Partner is { } partner) Line(FontAwesomeIcon.UserFriends, partner.Name, Theme.PinkSoft);
        if (status.Act is { } act) Line(FontAwesomeIcon.Fire, act.DisplayName, Theme.VioletSoft);
        if (status.Watchers > 0) Line(FontAwesomeIcon.Eye, status.Watchers == 1 ? "1 watching" : $"{status.Watchers} watching", Theme.Pink);
        if (status.SceneStartedUtc is { } started)
        {
            var elapsed = DateTime.UtcNow - started;
            var line = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
            if (status.Fatigue < 1) line += $"  fatigue {status.Fatigue:P0}";
            Line(FontAwesomeIcon.Clock, line, Theme.Muted);
        }

        SavePosition();
    }

    private static void Line(FontAwesomeIcon icon, string text, Vector4 color)
    {
        Theme.Icon(icon, color);
        ImGui.SameLine();
        ImGui.TextColored(color, text);
    }

    private void SavePosition()
    {
        if (settings.HudLocked) return;
        var pos = ImGui.GetWindowPos();
        if ((pos.X != settings.HudX || pos.Y != settings.HudY) && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            settings.HudX = pos.X;
            settings.HudY = pos.Y;
            settings.Save();
        }
    }
}
