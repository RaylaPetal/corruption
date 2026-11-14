using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Corruption.Bridges;
using Corruption.Core;
using Corruption.Game;
using Corruption.Ipc;
using Corruption.Mods;
using Corruption.Plate;
using Corruption.Scene;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace Corruption.Windows;

public sealed partial class MainWindow : Window
{
    private enum Page { Profile, Goals, Shop, Nearby, Acts, Nameplate, Settings, Debug }

    private static readonly string[] HudSizes = ["Small", "Medium", "Large"];

    /// What the scene needs, in the order the check tests it.
    private static readonly (SceneFailure Failure, string Text)[] Conditions =
    [
        (SceneFailure.NoKnownAct, "You're playing a known act"),
        (SceneFailure.NoPartner, "Someone nearby has Corruption"),
        (SceneFailure.PartnerTooFar, "You're within 3 yalms"),
        (SceneFailure.NoPartnerDeclaration, "They're playing a known act"),
        (SceneFailure.PartnerEmoteMismatch, "You can see them playing it"),
    ];


    private readonly Settings settings;
    private readonly Balance balance;
    private readonly SceneService scene;
    private readonly ActsService acts;
    private readonly PenumbraIpc penumbra;
    private readonly SimpleHeelsIpc heels;
    private readonly CummyBridge cummy;
    private readonly BridgeHub bridges;
    private readonly PlateRenderer plateRenderer;
    private readonly ActDetector detector;
    private readonly RewardPopup popup;

    private Page page = Page.Profile;
    private string filter = "";
    private bool confirmingReset;
    private int rowsVersion = -1;
    private string rowsFilter = "";
    private List<ModRow> rows = [];
    private int newCount;

    public MainWindow(Settings settings, Balance balance, SceneService scene, ActsService acts, PenumbraIpc penumbra,
        SimpleHeelsIpc heels, CummyBridge cummy, BridgeHub bridges, PlateRenderer plateRenderer, ActDetector detector, RewardPopup popup)
        : base("Corruption###CorruptionMain")
    {
        this.bridges = bridges;
        this.settings = settings;
        this.balance = balance;
        this.scene = scene;
        this.acts = acts;
        this.penumbra = penumbra;
        this.heels = heels;
        this.cummy = cummy;
        this.plateRenderer = plateRenderer;
        this.detector = detector;
        this.popup = popup;
        Size = new Vector2(660, 640);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(520, 420), MaximumSize = new Vector2(1400, 1600) };
        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.QuestionCircle,
            Click = _ => HelpLinks.RequestOpen(),
            ShowTooltip = () => ImGui.SetTooltip("Help & bug reports"),
        });
        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Scroll,
            Click = _ => OpenChangelog?.Invoke(),
            ShowTooltip = () => ImGui.SetTooltip("What's new"),
        });
    }

    private static float Scale => ImGuiHelpers.GlobalScale;

    public override void Draw()
    {
        using var content = Theme.Content();
        HelpLinks.Draw();
        if (scene.Engine is not { } engine)
        {
            Theme.MutedText("Log in to a character to use this.");
            return;
        }

        DrawHeader(engine);
        if (engine.Save.Setup is null)
        {
            ImGui.BeginChild("##setup", Vector2.Zero);
            DrawSetup();
            ImGui.EndChild();
            return;
        }
        DrawNav();

        // The page leaves room for the integrations footer below it.
        var integrations = Integrations();
        var footer = FooterLines(integrations) * ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;
        ImGui.BeginChild("##page", new Vector2(0, -footer));
        switch (page)
        {
            case Page.Profile: DrawProfile(engine); break;
            case Page.Goals: DrawGoals(engine); break;
            case Page.Shop: DrawShop(engine); break;
            case Page.Nearby: DrawNearby(); break;
            case Page.Acts: DrawActs(); break;
            case Page.Nameplate: DrawNameplate(engine); break;
            case Page.Settings: DrawSettings(engine); break;
#if DEBUG
            case Page.Debug: DrawDebug(engine); break;
#endif
        }
        ImGui.EndChild();
        DrawFooter(integrations);
    }

    /// Every integration: its name, whether it's there, whether Corruption needs it, and what that means.
    private (string Name, bool Ok, bool Required, string Detail)[] Integrations() =>
    [
        ("Penumbra", penumbra.IsAvailable, true, penumbra.IsAvailable ? "Connected" : "Unavailable. It's required to detect acts."),
        ("SimpleHeels", heels.IsAvailable, true, heels.IsAvailable ? "Connected" : "Unavailable. It's required to find other players."),
        ("Cummy", cummy.IsAvailable, false, cummy.Status),
        ("Glamourer", bridges.Glamourer.IsAvailable, false, bridges.Glamourer.IsAvailable ? "Connected, for tier visuals" : "Not installed. Only tier visuals need it."),
        ("Moodles", bridges.Moodles.IsAvailable, false, bridges.Moodles.IsAvailable ? "Connected, for statuses" : "Not installed. Only statuses need it."),
        ("Nameplate", plateRenderer.IsAvailable, false, plateRenderer.IsAvailable ? "The nameplate overlay is running" : "The overlay couldn't start, so it's off this session"),
    ];

    private static float PillWidth(string name) => ImGui.CalcTextSize(name).X + 30f * Scale;

    private static float FooterGap => 6f * Scale;

    /// The support buttons sit at the right of the footer's first line, so pills there wrap short of them.
    private static float FooterSupportReserve => SupportLinks.Width + ImGui.GetStyle().ItemSpacing.X;

    /// Lines the footer takes at the current width, so the page above leaves room for all of them.
    private int FooterLines((string Name, bool Ok, bool Required, string Detail)[] items)
    {
        var avail = ImGui.GetContentRegionAvail().X;
        var lines = 1;
        var used = 0f;
        foreach (var item in items)
        {
            var width = PillWidth(item.Name);
            var limit = lines == 1 ? avail - FooterSupportReserve : avail;
            if (used > 0 && used + FooterGap + width > limit)
            {
                lines++;
                used = 0;
            }
            used += (used > 0 ? FooterGap : 0) + width;
        }
        return lines;
    }

    /// Every integration at a glance, on every page. Required ones are red when missing, optional ones just grey.
    private void DrawFooter((string Name, bool Ok, bool Required, string Detail)[] items)
    {
        ImGui.Separator();
        var start = ImGui.GetCursorPos();
        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - SupportLinks.Width);
        SupportLinks.Draw();
        ImGui.SetCursorPos(start);

        var first = true;
        var firstLine = true;
        foreach (var (name, ok, required, detail) in items)
        {
            if (!first)
            {
                ImGui.SameLine(0, FooterGap);
                var room = ImGui.GetContentRegionAvail().X - (firstLine ? FooterSupportReserve : 0);
                if (room < PillWidth(name))
                {
                    ImGui.NewLine();
                    firstLine = false;
                }
            }
            first = false;
            Theme.StatusPill(name, ok ? Theme.Good : required ? Theme.Danger : Theme.Faint);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(detail);
        }
    }

    // ── Header and navigation ────────────────────────────────────────────────

    private void DrawHeader(SceneEngine engine)
    {
        var save = engine.Save;
        var tier = Progression.TierOf(save.Level);
        var status = engine.Status;

        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var pad = new Vector2(14f, 11f) * Scale;
        var dl = ImGui.GetWindowDrawList();

        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);
        ImGui.SetCursorScreenPos(start + pad);
        ImGui.BeginGroup();

        var inner = width - pad.X * 2f;
        ImGui.SetWindowFontScale(1.35f);
        ImGui.TextColored(Theme.TierColor(tier), tier.ToString());
        ImGui.SetWindowFontScale(1f);
        ImGui.SameLine();
        Theme.Pill($"Level {save.Level}", Theme.Violet);
        if (save.Prestige > 0)
        {
            ImGui.SameLine();
            Theme.Pill(PrestigeRules.Label(save.Prestige, balance.StarsWithBonus), Theme.Warning);
        }
        if (save.Setup is not null)
        {
            ImGui.SameLine();
            Theme.Pill($"♦ {save.Wallet.Sins} Sins", Theme.PinkSoft);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Spend them in the Shop. Earned from your daily check-in, scenes, levels, goals and achievements.");
        }

        var moon = engine.MoonAt(DateTime.UtcNow);
        ImGui.SameLine();
        Theme.Pill(EorzeaMoon.Name(moon.Phase), moon.IsEvent ? Theme.VioletSoft : Theme.Muted);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(moon.IsEvent
                ? $"{EorzeaMoon.Effect(moon.Phase, balance)}. {Left(moon.PhaseEndsUtc - DateTime.UtcNow)} left."
                : $"{EorzeaMoon.Name(moon.NextEvent)} in {Left(moon.NextEventUtc - DateTime.UtcNow)}: {EorzeaMoon.Effect(moon.NextEvent, balance)}.");

        var stateName = Theme.StateName(status.State);
        ImGui.SameLine(MathF.Max(inner - Theme.StatusPillWidth(stateName), ImGui.GetCursorPosX() - start.X) + 0f);
        Theme.StatusPill(stateName, Theme.StateColor(status.State));

        Theme.XpBar(save.Xp, Progression.XpToNext(save.Level, balance), width: inner);
        Theme.GradientBar((float)(save.Arousal / 100), Theme.PinkDeep, Theme.Pink,
            $"Arousal {PlateComposer.ArousalNumber(save.Arousal)}", width: inner - Theme.HeartsWidth() - 10f * Scale);
        ImGui.SameLine(0, 10f * Scale);
        ImGui.AlignTextToFramePadding();
        Theme.Hearts(save.Arousal);
        DrawHeaderCycle(save, inner);

        ImGui.EndGroup();
        var end = new Vector2(start.X + width, ImGui.GetItemRectMax().Y + pad.Y);

        dl.ChannelsSetCurrent(0);
        Theme.GradientRect(start, end, Theme.Alpha(Theme.VioletDeep, 0.55f), Theme.Alpha(Theme.PinkDeep, 0.40f), 10f * Scale);
        dl.ChannelsMerge();

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(end - start);
    }

    /// A time left, like "2h 10m" or "45m".
    public static string Left(TimeSpan span)
        => span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m" : $"{Math.Max(0, span.Minutes)}m";

    private void DrawNav()
    {
        Nav(FontAwesomeIcon.Heart, "Profile", Page.Profile);
        Nav(FontAwesomeIcon.Trophy, "Goals", Page.Goals);
        Nav(FontAwesomeIcon.Store, "Shop", Page.Shop);
        Nav(FontAwesomeIcon.UserFriends, "Nearby", Page.Nearby);
        Nav(FontAwesomeIcon.Fire, "Acts", Page.Acts);
        Nav(FontAwesomeIcon.Tag, "Nameplate", Page.Nameplate);
        Nav(FontAwesomeIcon.Cog, "Settings", Page.Settings);
#if DEBUG
        Nav(FontAwesomeIcon.Bug, "Debug", Page.Debug);
#endif
        ImGui.NewLine();
        ImGuiHelpers.ScaledDummy(1f);
    }

    /// One nav button, wrapping to the next line when it wouldn't fit on this one.
    private void Nav(FontAwesomeIcon icon, string text, Page target)
    {
        if (ImGui.GetContentRegionAvail().X < Theme.NavButtonWidth(icon, text)) ImGui.NewLine();
        if (Theme.NavButton(icon, text, page == target)) page = target;
        ImGui.SameLine(0, 4f * Scale);
    }

    // ── First-time setup ─────────────────────────────────────────────────────

    /// Opens the welcome window. Set by the plugin once both windows exist.
    public Action? OpenWelcome { get; set; }
    public Action? OpenChangelog { get; set; }

    /// Before setup, the main window only points to the welcome window, which holds the setup.
    private void DrawSetup()
    {
        Theme.CardBox("intro", () =>
        {
            Theme.Heading(FontAwesomeIcon.Star, "Set up your profile");
            Theme.MutedWrapped("A few steps choose your difficulty, decay, climax and arousal gain, your anatomy, pregnancy, and how Corruption shows. Scenes can't earn until it's done.");
            if (Theme.PrimaryButton(FontAwesomeIcon.ArrowRight, "Finish setup")) OpenWelcome?.Invoke();
        }, stripe: Theme.Pink);
    }

    /// Minutes from 0 to 100 arousal at Pure with nobody watching.
    private double ClimaxMinutes(double gain) => 100 / (balance.ArousalPerTick * gain) * balance.TickSeconds / 60;
}
