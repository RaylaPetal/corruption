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

    /// Shown when turned on, and during a scene with a partner if that's chosen. A scene counts from its first verified
    /// tick until it has ended, so a brief interruption doesn't make the HUD blink.
    public override bool DrawConditions()
        => scene.Engine is { } engine
           && (settings.ShowHud || settings.HudDuringScenes && engine.Status.SceneStartedUtc is not null);

    public override void PreDraw()
        => Flags = settings.HudLocked ? BaseFlags | ImGuiWindowFlags.NoMove : BaseFlags;

    public override void Draw()
    {
        if (scene.Engine is not { } engine) return;
        var size = Scales[Math.Clamp(settings.HudSize, 0, Scales.Length - 1)];
        ImGui.SetWindowFontScale(size);
        var width = BaseWidth * size * ImGuiHelpers.GlobalScale;

        var save = engine.Save;
        var status = engine.Status;
        var tier = Progression.TierOf(save.Level);
        var stateColor = Theme.StateColor(status.State);
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        // Row 1: who you are on the left, the menu on the right. The HUD widens rather than letting them overlap.
        var label = $"{tier} ♥ Lv. {save.Level}";
        if (save.Prestige > 0) label += $" {PrestigeRules.Label(save.Prestige, balance.StarsWithBonus)}";
        var buttonWidth = Theme.IconOnlyButtonWidth(FontAwesomeIcon.Bars);
        width = MathF.Max(width, Theme.PillWidth(label) + spacing * 2 + buttonWidth);
        var left = ImGui.GetCursorScreenPos().X;
        ImGui.Dummy(new Vector2(width, 0));

        Theme.Pill(label, Theme.TierColor(tier));
        ImGui.SameLine();
        ImGui.SetCursorScreenPos(new Vector2(left + width - buttonWidth, ImGui.GetItemRectMin().Y));
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Bars)) openMainWindow();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open Corruption");

        // The title, and a moon event while one is on.
        var title = TitleBook.Equipped(save);
        var moon = engine.MoonAt(DateTime.UtcNow);
        if (title is not null)
            ImGui.TextColored(new Vector4(PlateComposer.TitleColor(title), 1f), title.Name);
        if (moon.IsEvent)
        {
            if (title is not null) ImGui.SameLine();
            Theme.Icon(FontAwesomeIcon.Moon, Theme.VioletSoft);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{EorzeaMoon.Name(moon.Phase)}: {EorzeaMoon.Effect(moon.Phase, balance)}. {MainWindow.Left(moon.PhaseEndsUtc - DateTime.UtcNow)} left.");
        }

        // Row 2: the bars, exactly the HUD's width.
        Theme.XpBar(save.Xp, Progression.XpToNext(save.Level, balance), width: width);
        Theme.ArousalBar(save.Arousal, width: width);

        // Row 3: the scene state, and why it isn't earning, wrapped to the HUD.
        // Everything below wraps at the HUD's edge, so a long name or message can't stretch it.
        ImGui.PushTextWrapPos(left + width - ImGui.GetWindowPos().X);
        Theme.StatusPill(Theme.StateName(status.State), stateColor);
        if (status.State is SceneState.Waiting or SceneState.Paused) ImGui.TextColored(stateColor, SceneCheck.Describe(status.Failure));

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

        ImGui.PopTextWrapPos();

        DrawBaby(save, width);
        DrawEffects(save);
        SavePosition();
    }

    /// A small growing baby while pregnant, with the days to go beside it.
    private static void DrawBaby(CharacterSave save, float width)
    {
        var p = save.Pregnancy;
        if (!PregnancyBook.IsPregnant(p)) return;
        var now = DateTime.UtcNow;
        var days = PregnancyBook.DaysPregnant(p, now);
        BabyIllustration.Draw(MathF.Min(64 * ImGuiHelpers.GlobalScale, width * 0.3f), days / PregnancyBook.DaysLong);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip($"About the size of {BabyIllustration.SizeOn(days)}");
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextColored(Theme.PinkSoft, "Pregnant");
        var left = Math.Ceiling((p.DueUtc!.Value - now).TotalDays);
        Theme.MutedText(left <= 1 ? "Due today" : $"{left:0} days to go");
        if (p.Craving is { } craving && !p.CravingMet) Theme.MutedText($"Craving: {CravingBook.Describe(craving).ToLowerInvariant()}");
        ImGui.EndGroup();
    }

    /// One icon per item working now, with what's left on hover.
    private static void DrawEffects(CharacterSave save)
    {
        var now = DateTime.UtcNow;
        var first = true;
        foreach (var (id, effect) in save.Effects)
        {
            if (!ItemBook.IsActive(save, id, now) || ItemCatalog.Find(id) is not { } item) continue;
            if (!first) ImGui.SameLine();
            first = false;
            Theme.Icon(ItemIcons.Of(id), Theme.PinkSoft);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{item.Name}: {ItemIcons.Remaining(effect, now)}\n{item.Effect}");
        }
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
