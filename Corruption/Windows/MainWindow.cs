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

    private static readonly string[] DifficultyNames = Enum.GetNames<Difficulty>();
    private static readonly string[] DecayNames = Enum.GetNames<DecayMode>();
    private static readonly string[] ClimaxNames = ["Automatic at 100", "By hand only"];
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
    private int draftDifficulty = (int)Difficulty.Normal;
    private int draftDecay = (int)DecayMode.Normal;
    private int draftClimax;
    private float draftGain = 1f;
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
    }

    private static float Scale => ImGuiHelpers.GlobalScale;

    public override void Draw()
    {
        if (scene.Engine is not { } engine)
        {
            Theme.MutedText("Log in to a character to use this.");
            return;
        }

        DrawHeader(engine);
        if (engine.Save.Setup is null)
        {
            ImGui.BeginChild("##setup", Vector2.Zero);
            DrawSetup(engine);
            ImGui.EndChild();
            return;
        }
        DrawNav();

        ImGui.BeginChild("##page", Vector2.Zero);
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

    private void Nav(FontAwesomeIcon icon, string text, Page target)
    {
        if (Theme.NavButton(icon, text, page == target)) page = target;
        ImGui.SameLine(0, 4f * Scale);
    }

    // ── First-time setup ─────────────────────────────────────────────────────

    private void DrawSetup(SceneEngine engine)
    {
        Theme.CardBox("intro", () =>
        {
            Theme.Heading(FontAwesomeIcon.Star, "Set up your profile");
            Theme.MutedWrapped("Choose how this character plays. None of this can be changed afterward unless you reset your profile.");
        }, stripe: Theme.Pink);

        Theme.CardBox("difficulty", () =>
        {
            Theme.Heading(FontAwesomeIcon.Fire, "Difficulty");
            Theme.Choice("difficulty", DifficultyNames, ref draftDifficulty);
            Theme.MutedText(ProfileSetup.Describe((Difficulty)draftDifficulty));
        });

        Theme.CardBox("decay", () =>
        {
            Theme.Heading(FontAwesomeIcon.HourglassHalf, "Decay");
            Theme.Choice("decay", DecayNames, ref draftDecay);
            Theme.MutedText(ProfileSetup.Describe((DecayMode)draftDecay));
            Theme.MutedText("Fading never drops you below the start of your current tier.");
        });

        Theme.CardBox("climax", () =>
        {
            Theme.Heading(FontAwesomeIcon.Heart, "Climax");
            Theme.Choice("climax", ClimaxNames, ref draftClimax);
            Theme.MutedText(draftClimax == 0 ? "You climax when arousal reaches 100, or earlier with /corr climax." : "Only /corr climax does it, from 70 arousal. Arousal holds at 100 until you do.");
        });

        Theme.CardBox("gain", () =>
        {
            Theme.Heading(FontAwesomeIcon.Bolt, "Arousal gain");
            ImGui.SetNextItemWidth(220 * Scale);
            ImGui.SliderFloat("##gain", ref draftGain, (float)ProfileSetup.MinArousalGain, (float)ProfileSetup.MaxArousalGain, "%.2fx");
            Theme.MutedText($"About {ClimaxMinutes(draftGain):0.#} minutes to climax in a quiet scene.");
        });

        if (Theme.PrimaryButton(FontAwesomeIcon.Check, "Start"))
        {
            engine.CompleteSetup(new ProfileSetup
            {
                Difficulty = (Difficulty)draftDifficulty,
                Decay = (DecayMode)draftDecay,
                AutoClimax = draftClimax == 0,
                ArousalGain = Math.Round(draftGain, 2),
            }, DateTime.UtcNow);
        }
    }

    /// Minutes from 0 to 100 arousal at Pure with nobody watching.
    private double ClimaxMinutes(double gain) => 100 / (balance.ArousalPerTick * gain) * balance.TickSeconds / 60;

    // ── Profile ──────────────────────────────────────────────────────────────

    private void DrawProfile(SceneEngine engine)
    {
        DrawSceneCard(engine);
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
    }

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

    // ── Goals and achievements ───────────────────────────────────────────────

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
            foreach (var achievement in GoalCatalog.Achievements.OrderByDescending(a => data.Achievements.Contains(a.Id)))
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
            Theme.MutedText($"+{def.Xp:0} XP");
            if (!goal.Done)
                Theme.GradientBar((float)goal.Progress / def.Target, Theme.VioletDeep, Theme.Pink, ProgressText(def.Kind, goal.Progress, def.Target), 14f * Scale);
        }
    }

    private static void DrawAchievement(CharacterSave save, AchievementDef achievement, bool earned)
    {
        Theme.Icon(earned ? FontAwesomeIcon.Trophy : FontAwesomeIcon.Lock, earned ? Theme.Warning : Theme.Faint);
        ImGui.SameLine();
        if (earned) ImGui.TextColored(Theme.PinkSoft, achievement.Name);
        else ImGui.TextUnformatted(achievement.Name);
        ImGui.SameLine();
        Theme.MutedText(achievement.Text);
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

    // ── Nearby ───────────────────────────────────────────────────────────────

    private void DrawNearby()
    {
        if (scene.Engine is not { } engine) return;
        Theme.CardBox("hide", () =>
        {
            var hidden = engine.Save.Hidden;
            if (ImGui.Checkbox("Hide me", ref hidden)) scene.SetHidden(hidden);
            Theme.MutedWrapped(engine.Save.Hidden
                ? "Other players can't see you have Corruption, or your level and arousal. Scenes can't earn until you turn this off."
                : "Players with Corruption who are synced with you can see your level, arousal and whether you're in an act.");
        }, stripe: engine.Save.Hidden ? Theme.Warning : null);

        DrawBondCard(engine);

        var current = engine.Status.Partner;
        Theme.Heading(FontAwesomeIcon.UserFriends, "Nearby with Corruption");
        if (scene.Nearby.Count == 0)
        {
            Theme.MutedWrapped("Nobody yet. You'll only see someone here if they run Corruption and SimpleHeels, and you're synced with them through Lightless or a similar service.");
        }
        foreach (var nearby in scene.Nearby.OrderBy(n => n.Distance))
        {
            var isCurrent = current is not null && current.Matches(nearby.Player.Name, nearby.Player.World);
            Theme.CardBox(nearby.Player.Key, () =>
            {
                var tag = nearby.Tag;
                var tier = Progression.TierOf(tag.Level);
                Theme.Icon(FontAwesomeIcon.Heart, isCurrent ? Theme.Pink : Theme.Faint);
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(nearby.Player.Name);
                ImGui.SameLine();
                Theme.MutedText($"@{nearby.Player.World}");
                ImGui.SameLine();
                Theme.Pill($"{tier} ♥ Lv. {tag.Level}", Theme.TierColor(tier));
                if (engine.StarsOf(nearby.Player) is > 0 and var stars)
                {
                    ImGui.SameLine();
                    Theme.Pill(PrestigeRules.Label(stars, balance.StarsWithBonus), Theme.Warning);
                }
                ImGui.SameLine();
                Theme.Pill($"{tag.Arousal}%", Theme.Pink);
                ImGui.SameLine();
                Theme.Pill(tag.Paused ? "Paused" : tag.Pose is not null ? "In an act" : "Idle", tag.Paused ? Theme.Danger : tag.Pose is not null ? Theme.Good : Theme.Muted);
                var distance = $"{nearby.Distance:0.#} y";
                Theme.AlignRight(ImGui.CalcTextSize(distance).X);
                ImGui.AlignTextToFramePadding();
                Theme.MutedText(distance);
                DrawBondRow(engine, nearby);
            }, stripe: isCurrent ? Theme.Pink : null);
        }

        ImGuiHelpers.ScaledDummy(4f);
        Theme.CardBox("integrations", () =>
        {
            Theme.Heading(FontAwesomeIcon.Plug, "Integrations");
            Integration("Penumbra", penumbra.IsAvailable, penumbra.IsAvailable ? "Connected" : "Unavailable (required)");
            Integration("SimpleHeels", heels.IsAvailable, heels.IsAvailable ? "Connected" : "Unavailable (required to find other players)");
            Integration("Nameplate text", plateRenderer.IsAvailable, plateRenderer.IsAvailable ? "Running" : "Couldn't start, so it's off this session");
            Integration("Cummy", cummy.IsAvailable, cummy.Status);
        });
    }

    private static void Integration(string name, bool ok, string detail)
    {
        Theme.StatusPill(name, ok ? Theme.Good : Theme.Danger);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        Theme.MutedText(detail);
    }

    // ── Acts ─────────────────────────────────────────────────────────────────

    private void DrawActs()
    {
        if (!penumbra.IsAvailable)
        {
            ImGui.TextColored(Theme.Warning, "Penumbra is unavailable.");
            return;
        }

        if (ImGui.CollapsingHeader("Step 1: Folders that hold your acts", acts.File.Folders.Count == 0 ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None))
        {
            ImGui.BeginChild("##folders", new Vector2(0, 170 * Scale), true);
            foreach (var folder in acts.AllFolders)
            {
                var ticked = acts.File.Folders.Contains(folder, StringComparer.OrdinalIgnoreCase);
                if (ImGui.Checkbox(folder.Length == 0 ? "(mods in no folder)" : folder, ref ticked)) acts.SetFolderTicked(folder, ticked);
            }
            if (acts.AllFolders.Count == 0) Theme.MutedText("No Penumbra folders found.");
            ImGui.EndChild();
        }

        Theme.Heading(FontAwesomeIcon.Fire, "Step 2: Mods that count");
        Theme.MutedWrapped("Tick a mod and every animation in it counts, as long as Penumbra is serving it to you.");

        ImGui.SetNextItemWidth(220 * Scale);
        ImGui.InputTextWithHint("##filter", "Filter", ref filter, 64);
        ImGui.SameLine();
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.SyncAlt, "Refresh")) acts.RequestRescan();

        if (rowsVersion != acts.Version || rowsFilter != filter)
        {
            rowsVersion = acts.Version;
            rowsFilter = filter;
            rows = acts.Mods
                .Where(m => filter.Length == 0 || m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => acts.File.Find(m.Directory) is not null).ThenByDescending(acts.IsNew).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            newCount = acts.Mods.Count(acts.IsNew);
        }

        if (acts.IsScanning)
        {
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            Theme.MutedText("Reading mods...");
        }
        if (newCount > 0)
        {
            ImGui.SameLine();
            Theme.Pill($"{newCount} new, not yet picked", Theme.Warning);
            ImGui.SameLine();
            if (ImGui.Button("Mark all seen")) acts.MarkAllSeen();
        }

        ImGui.BeginChild("##mods", Vector2.Zero);
        for (var index = 0; index < rows.Count; index++)
        {
            ImGui.PushID(rows[index].Directory);
            DrawMod(rows[index]);
            ImGui.PopID();
        }
        if (rows.Count == 0) Theme.MutedText(acts.File.Folders.Count == 0 ? "Tick a folder above to see its mods." : "No animation mods here.");
        ImGui.EndChild();
    }

    private static string CountsOf(ModRow mod)
        => mod.Countable == 1 ? "1 animation" : $"{mod.Countable} animations";

    private void DrawMod(ModRow mod)
    {
        if (mod.Countable == 0)
        {
            ImGui.BeginDisabled();
            var off = false;
            ImGui.Checkbox($"{mod.Name}##mod", ref off);
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("None of this mod's emotes loop, so it can't count.");
            return;
        }

        var pick = acts.File.Find(mod.Directory);
        if (pick is null)
        {
            var picked = false;
            if (ImGui.Checkbox($"{mod.Name}##mod", ref picked)) acts.Pick(mod);
            ImGui.SameLine();
            Theme.Pill(CountsOf(mod), Theme.Violet);
            if (acts.IsNew(mod))
            {
                ImGui.SameLine();
                Theme.Pill("new", Theme.Warning);
            }
            return;
        }

        var on = true;
        if (ImGui.Checkbox($"{mod.Name}##mod", ref on)) acts.Unpick(pick);
        ImGui.SameLine();
        Theme.Pill(CountsOf(mod), Theme.Pink);
        if (mod.NotLooping > 0)
        {
            ImGui.SameLine();
            Theme.Pill($"{mod.NotLooping} can't count", Theme.Faint);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("These emotes don't loop, so they can't count as acts.");
        }
        DrawExclusionToggle(mod);
    }

    // ── Nameplate ────────────────────────────────────────────────────────────

    private void DrawNameplate(SceneEngine engine)
    {
        var values = engine.OwnPlate;
        Theme.CardBox("preview", () =>
        {
            Theme.MutedText("Normally, with no arousal");
            DrawPlatePreview(PlateComposer.Compose(settings.PlateTemplate, values));
            Theme.MutedText($"During an act or with any arousal, cycling with the text above every {settings.PlateCycleSeconds:0.#}s");
            DrawPlatePreview(PlateComposer.Compose(settings.PlateActTemplate, values));
            Theme.MutedText("Just after a climax, cycling with arousal");
            DrawPlatePreview(PlateComposer.Compose(settings.PlateClimaxTemplate, values, forceGlow: true));
        }, stripe: Theme.Pink);

        if (!plateRenderer.IsAvailable)
            ImGui.TextColored(Theme.Warning, "The nameplate overlay couldn't start, so no text is shown.");

        Theme.CardBox("show", () =>
        {
            Theme.Heading(FontAwesomeIcon.Eye, "Showing it");
            var own = settings.PlateShowOwn;
            if (ImGui.Checkbox("Above my nameplate", ref own))
            {
                settings.PlateShowOwn = own;
                settings.Save();
            }
            ImGui.SameLine();
            var partners = settings.PlateShowPartners;
            if (ImGui.Checkbox("Above other players' nameplates", ref partners))
            {
                settings.PlateShowPartners = partners;
                settings.Save();
            }
            Theme.MutedWrapped("Only you and players with Corruption who are synced with you can see this. Everyone else sees nothing.");

            var size = settings.PlateScale;
            ImGui.SetNextItemWidth(180 * Scale);
            if (ImGui.SliderFloat("Size", ref size, 0.5f, 2f, "%.2f"))
            {
                settings.PlateScale = size;
                settings.Save();
            }
            var offset = settings.PlateOffsetY;
            ImGui.SetNextItemWidth(180 * Scale);
            if (ImGui.SliderFloat("Height", ref offset, -40f, 80f, "%.0f"))
            {
                settings.PlateOffsetY = offset;
                settings.Save();
            }
        });

        Theme.CardBox("template", () =>
        {
            Theme.Heading(FontAwesomeIcon.Pen, "Text");
            TextRow("Normally", "##steady", settings.PlateTemplate, t => settings.PlateTemplate = t);
            TextRow("With arousal", "##act", settings.PlateActTemplate, t => settings.PlateActTemplate = t);
            TextRow("On climax", "##climax", settings.PlateClimaxTemplate, t => settings.PlateClimaxTemplate = t);
            Theme.MutedText("Tokens: {tier} {level} {arousal} {arousalBar} {prestige}");

            var unknown = PlateComposer.UnknownTokens(settings.PlateTemplate + settings.PlateActTemplate + settings.PlateClimaxTemplate);
            if (unknown.Count > 0) ImGui.TextColored(Theme.Warning, $"Unknown: {string.Join(", ", unknown)}");

            var cycle = settings.PlateCycleSeconds;
            ImGui.SetNextItemWidth(180 * Scale);
            if (ImGui.SliderFloat("Seconds per text", ref cycle, 1f, 15f, "%.0f"))
            {
                settings.PlateCycleSeconds = cycle;
                settings.Save();
            }

            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Undo, "Reset texts"))
            {
                settings.PlateTemplate = PlateComposer.DefaultTemplate;
                settings.PlateActTemplate = PlateComposer.DefaultActTemplate;
                settings.PlateClimaxTemplate = PlateComposer.DefaultClimaxTemplate;
                settings.Save();
            }
            Theme.MutedText("Other players' text uses these too, with their values.");
        });
    }

    private void TextRow(string label, string id, string value, Action<string> set)
    {
        ImGui.AlignTextToFramePadding();
        Theme.MutedText(label);
        ImGui.SameLine(110 * Scale);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        var text = value;
        if (ImGui.InputText(id, ref text, 64))
        {
            set(text);
            settings.Save();
        }
    }

    /// The text roughly as it looks over a character, with its glow.
    private static void DrawPlatePreview(RenderedPlate preview)
    {
        var text = preview.Text;
        ImGui.SetWindowFontScale(1.35f);
        var size = ImGui.CalcTextSize(text);
        var left = ImGui.GetCursorScreenPos().X + MathF.Max(0, (ImGui.GetContentRegionAvail().X - size.X) / 2f);
        var at = new Vector2(left, ImGui.GetCursorScreenPos().Y);
        var dl = ImGui.GetWindowDrawList();
        if (preview.Glow is { } glow)
        {
            var color = ImGui.GetColorU32(new Vector4(glow, 0.45f));
            var reach = 1.5f * Scale;
            foreach (var (x, y) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, 1), (-1, 1), (1, -1) })
                dl.AddText(at + new Vector2(x, y) * reach, color, text);
        }
        dl.AddText(at, ImGui.GetColorU32(new Vector4(preview.Color, 1f)), text);
        ImGui.Dummy(size);
        ImGui.SetWindowFontScale(1f);
    }

    // ── Settings ─────────────────────────────────────────────────────────────

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
            Theme.Heading(FontAwesomeIcon.Trophy, "Goal and achievement alerts");
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
            Theme.MutedText("The sound follows the game's system sound volume.");
        });

        DrawBridges(engine);

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

#if DEBUG
    // ── Debug (dev builds only) ──────────────────────────────────────────────

    private string debugDump = "";
    private int debugMode = 50;
    private int debugVariant;

    private void DrawDebug(SceneEngine engine)
    {
        DrawDetectionSpike();
        ImGuiHelpers.ScaledDummy(4f);
        DrawSimulator(engine);
        ImGuiHelpers.ScaledDummy(4f);
        DrawPregnancyDebug(engine);
    }

    /// What the in-game spike needs: raw pose state, and what Penumbra serves for the current pose.
    private void DrawDetectionSpike()
    {
        Theme.Heading(FontAwesomeIcon.Search, "Detection");
        var me = Plugin.ObjectTable.LocalPlayer;
        var pose = PoseReader.Read(me);
        var race = PoseReader.RaceCode(me);
        Theme.MutedText($"You: {PoseReader.Describe(me)}; pose {pose?.ToString() ?? "none"}; race {race ?? "?"}");
        var target = Plugin.TargetManager.Target as IPlayerCharacter;
        Theme.MutedText($"Target: {(target is null ? "none" : $"{PoseReader.Describe(target)}; pose {PoseReader.Read(target)?.ToString() ?? "none"}")}");
        var nonLooping = acts.Candidates.Count(c => c.Pose is null);
        Theme.MutedWrapped($"Nameplate: {plateRenderer.Diagnostics}");
        Theme.MutedText($"Candidates: {acts.Candidates.Count}, of which {nonLooping} don't loop. Picked mods: {acts.File.Mods.Count}.");

        if (ImGui.Button("Explain current pose") && pose is { } current)
        {
            debugDump = string.Join("\n", detector.Explain(current, race).DefaultIfEmpty("No candidate for this pose."));
            Plugin.Log.Information($"Corruption detection dump:\n{debugDump}");
        }
        ImGui.SameLine();
        if (ImGui.Button("Log non-looping candidates"))
        {
            foreach (var candidate in acts.Candidates.Where(c => c.Pose is null))
                Plugin.Log.Information($"Non-looping: {candidate.ModName} / {candidate.Group} / {candidate.Option}: {candidate.Trigger}");
        }
        if (debugDump.Length > 0)
        {
            ImGui.BeginChild("##dump", new Vector2(0, 120 * Scale), true, ImGuiWindowFlags.HorizontalScrollbar);
            ImGui.TextUnformatted(debugDump);
            ImGui.EndChild();
        }
    }

    private void DrawSimulator(SceneEngine engine)
    {
        Theme.Heading(FontAwesomeIcon.Robot, "Partner simulator");
        var active = scene.DebugPartnerActive;
        if (ImGui.Checkbox("Simulated player is here", ref active)) scene.DebugPartnerActive = active;
        if (!active) return;

        var distance = scene.DebugDistance;
        ImGui.SetNextItemWidth(160 * Scale);
        if (ImGui.SliderFloat("Distance", ref distance, 0, 15, "%.1f y")) scene.DebugDistance = distance;

        var tag = scene.DebugTag;
        var playing = tag.Pose is not null;
        if (ImGui.Checkbox("In an act", ref playing)) tag = tag with { Pose = playing ? new PoseKey(50, 0) : null };
        ImGui.SameLine();
        var paused = tag.Paused;
        if (ImGui.Checkbox("Paused", ref paused)) tag = tag with { Paused = paused };
        ImGui.SetNextItemWidth(160 * Scale);
        var level = tag.Level;
        if (ImGui.SliderInt("Level", ref level, 1, 100)) tag = tag with { Level = level };
        if (ImGui.Button("Climax")) tag = tag with { ClimaxCount = tag.ClimaxCount + 1, Arousal = 20 };
        ImGui.SameLine();
        if (ImGui.Button("Layer me through Cummy")) scene.DebugLayerMe();
        ImGui.SameLine();
        if (ImGui.Button("Aim from my target")) scene.DebugAimFromTarget();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Shows the nearest parts on you to your target's genitals, as a climax from them would land. Nothing is layered.");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("A real layer on a random part, through Cummy plugin API 3. It pays no XP: the simulated player's Climax button does that, as a real partner would.");
        scene.DebugTag = tag;

        if (ImGui.Button("Arousal +10")) engine.Save.Arousal = Math.Min(99, engine.Save.Arousal + 10);
        ImGui.SameLine();
        if (ImGui.Button("Level 100")) engine.Save.Level = 100;
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Test prestige. This changes your real save.");
        DrawSimulatedExtra(engine);

        Theme.MutedText($"Status: {SceneCheck.Describe(engine.Status.Failure)}; last tick {engine.Status.LastTickXp:0.##} XP");
        foreach (var line in scene.DebugMessages) Theme.MutedText(line);
    }
#endif
}
