using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Corruption.Core;
using Corruption.Scene;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;

namespace Corruption.Windows;

/// Status and timers, prestige, bonds and the bridges.
public sealed partial class MainWindow
{
    private static readonly string[] VisualKindNames = ["Nothing", "Glamourer design", "Penumbra mod"];
    private static readonly Tier[] VisualTiers = Enum.GetValues<Tier>().Where(t => t != Tier.Pure).ToArray();

    private bool confirmingPrestige;
    private int visualTier;
    private int visualKind;
    private string visualFilter = "";
    private Dictionary<string, string>? modList;
    private DateTime modListRead = DateTime.MinValue;

    // ── Profile: status and timers ───────────────────────────────────────────

    private void DrawStatusCard(SceneEngine engine)
    {
        var save = engine.Save;
        var now = DateTime.UtcNow;
        var status = ProfileTimers.StatusOf(save, engine.Status.State, now);
        var times = ProfileTimers.Compute(save, now);
        var (name, color) = status switch
        {
            ProfileStatus.Earning => ("Earning", Theme.Good),
            ProfileStatus.Waiting => ("Waiting", Theme.Warning),
            ProfileStatus.Paused => ("Paused", Theme.Danger),
            ProfileStatus.Hidden => ("Hidden", Theme.Muted),
            ProfileStatus.Fading => ("Fading", Theme.Warning),
            ProfileStatus.NeedsSetup => ("Setup needed", Theme.Muted),
            _ => ("Idle", Theme.VioletSoft),
        };

        Theme.CardBox("status", () =>
        {
            Theme.Heading(FontAwesomeIcon.Clock, "Status");
            ImGui.SameLine();
            Theme.StatusPill(name, color);

            Theme.Icon(FontAwesomeIcon.Heart, Theme.PinkSoft);
            ImGui.SameLine();
            if (times.SinceLastScene is { } since) ImGui.TextUnformatted($"Last scene {ProfileTimers.Describe(since)} ago");
            else Theme.MutedText("No scenes yet since setup");

            Theme.Icon(FontAwesomeIcon.HourglassHalf, times.Fading ? Theme.Warning : Theme.Violet);
            ImGui.SameLine();
            if (times.DecayOff) Theme.MutedText("Decay is off: your corruption never fades");
            else if (times.Fading && times.UntilNextFade is { } next)
                ImGui.TextColored(Theme.Warning, $"Fading. Next fade in {ProfileTimers.Describe(next)}. A scene stops it.");
            else if (times.UntilDecay is { } until) ImGui.TextUnformatted($"Decay starts in {ProfileTimers.Describe(until)}");
            else Theme.MutedText("Decay starts after your first scene");
        }, stripe: color);
    }

    // ── Profile: prestige ────────────────────────────────────────────────────

    private void DrawPrestigeCard(SceneEngine engine)
    {
        var save = engine.Save;
        var atCap = save.Level >= balance.LevelCap;
        if (save.Prestige == 0 && !atCap) return;

        Theme.CardBox("prestige", () =>
        {
            Theme.Heading(FontAwesomeIcon.Star, "Prestige");
            if (save.Prestige > 0)
            {
                Theme.Pill(PrestigeRules.Label(save.Prestige, balance.StarsWithBonus), Theme.Warning);
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                var bonus = PrestigeRules.Factor(save.Prestige, balance) - 1;
                Theme.MutedText(save.Prestige > balance.StarsWithBonus
                    ? $"+{bonus:P0} XP, the most stars can give. The rest are for show."
                    : $"+{bonus:P0} XP on every tick, for good.");
            }
            if (!atCap) return;

            Theme.MutedWrapped("Start again at level 1 with no XP or arousal, for a star and +5% XP for good. Your stats, achievements, streak, setup and bond stay.");
            if (!confirmingPrestige)
            {
                if (Theme.PrimaryButton(FontAwesomeIcon.Star, "Prestige")) confirmingPrestige = true;
                return;
            }
            ImGui.TextColored(Theme.Warning, "Back to level 1 now?");
            if (Theme.DangerButton(FontAwesomeIcon.Star, "Yes, prestige"))
            {
                if (engine.Prestige(DateTime.UtcNow) is { } refusal) Notifier.Info(refusal);
                confirmingPrestige = false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel##prestige")) confirmingPrestige = false;
        }, stripe: Theme.Warning);
    }

    // ── Nearby: bonds ────────────────────────────────────────────────────────

    private static string NameOf(string key) => key.Split('@')[0];

    /// The bond or request, even when they aren't nearby.
    private void DrawBondCard(SceneEngine engine)
    {
        var save = engine.Save;
        if (save.BondWith is not { } with)
        {
            if (Progression.TierOf(save.Level) < BondBook.MinimumTier)
                Theme.MutedText($"Bonding with a regular partner opens at the {BondBook.MinimumTier} tier.");
            return;
        }

        Theme.CardBox("bond", () =>
        {
            Theme.Icon(FontAwesomeIcon.Link, Theme.Pink);
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            if (save.BondFormed) ImGui.TextColored(Theme.PinkSoft, $"Bonded with {NameOf(with)}: scenes together pay x{balance.BondedFactor:0.##}");
            else ImGui.TextUnformatted($"Waiting for {NameOf(with)} to bond back");
            var label = save.BondFormed ? "Unbond" : "Withdraw";
            Theme.AlignRight(Theme.IconButtonWidth(FontAwesomeIcon.Unlink, label));
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Unlink, label)) engine.Unbond();
        }, stripe: Theme.Pink);
    }

    private void DrawBondRow(SceneEngine engine, VisiblePlayer nearby)
    {
        var key = nearby.Player.Key;
        var bonds = engine.Bonds;
        if (bonds.IsBondedWith(key))
        {
            Theme.Pill(nearby.Extra is null ? "Bonded (their Corruption can't show bonds)" : "Bonded", Theme.Pink);
            return;
        }
        if (bonds.IsRequesting(key))
        {
            Theme.Pill("Bond asked, waiting", Theme.PinkSoft);
            return;
        }
        if (nearby.Extra is null) return;

        var wants = bonds.WantsToBond.Contains(key);
        if (bonds.WhyNot(engine.Save.Level, nearby.Tag.Level) is { } reason)
        {
            if (wants || Progression.TierOf(engine.Save.Level) >= BondBook.MinimumTier) Theme.MutedText(wants ? $"Wants to bond. {reason}" : reason);
            return;
        }

        if (wants)
        {
            ImGui.TextColored(Theme.PinkSoft, "Wants to bond with you");
            ImGui.SameLine();
        }
        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Link, wants ? $"Bond back##{key}" : $"Bond##{key}")
            && engine.RequestBond(nearby.Player) is { } refusal)
            Notifier.Info(refusal);
    }

    // ── Settings: bridges ────────────────────────────────────────────────────

    private void DrawBridges(SceneEngine engine)
    {
        DrawTierVisuals(engine);
        DrawToys();
        DrawMoodles();
    }

    private void DrawTierVisuals(SceneEngine engine)
    {
        var save = engine.Save;
        Theme.CardBox("visuals", () =>
        {
            Theme.Heading(FontAwesomeIcon.Magic, "Tier visuals");
            var enabled = settings.TierVisualsEnabled;
            if (ImGui.Checkbox("Change how I look at each tier", ref enabled)) bridges.SetTierVisualsEnabled(enabled);
            ImGui.SameLine();
            var paused = settings.RemoveVisualWhilePaused;
            if (ImGui.Checkbox("Remove while paused", ref paused))
            {
                settings.RemoveVisualWhilePaused = paused;
                settings.Save();
            }
            Theme.MutedWrapped("Synced players see these like any other change to your look, whether or not they have Corruption.");

            var visuals = bridges.Visuals;
            if (visuals.Problem is { } problem) ImGui.TextColored(Theme.Warning, problem);
            else if (visuals.Applied is { } applied)
                Theme.MutedText(visuals.Superseded
                    ? $"On now: {applied.Name}. Something else changed your look since, so Corruption won't undo it."
                    : $"On now: {applied.Name}");

            foreach (var tier in VisualTiers)
            {
                save.TierVisuals.TryGetValue(tier.ToString(), out var choice);
                ImGui.TextColored(Theme.TierColor(tier), tier.ToString());
                ImGui.SameLine(110 * Scale);
                Theme.MutedText(choice is null ? "Nothing" : $"{(choice.Kind == VisualKind.Glamourer ? "Design" : "Mod")}: {choice.Name}{OptionsText(choice)}");
            }

            ImGuiHelpers.ScaledDummy(2f);
            var names = VisualTiers.Select(t => t.ToString()).ToArray();
            ImGui.SetNextItemWidth(140 * Scale);
            if (ImGui.Combo("Tier to change", ref visualTier, names, names.Length)) visualFilter = "";
            var editing = VisualTiers[Math.Clamp(visualTier, 0, VisualTiers.Length - 1)];
            if (Theme.Choice("visualKind", VisualKindNames, ref visualKind)) visualFilter = "";

            switch (visualKind)
            {
                case 0:
                    if (save.TierVisuals.ContainsKey(editing.ToString()) && ImGui.Button($"Clear {editing}'s visual"))
                        SetVisual(save, editing, null);
                    break;
                case 1:
                    DrawDesignPicker(save, editing);
                    break;
                default:
                    DrawModPicker(save, editing);
                    break;
            }
        });
    }

    private static string OptionsText(TierVisual visual)
        => visual.Options.Count == 0 ? "" : $" ({string.Join(", ", visual.Options.Values)})";

    private void SetVisual(CharacterSave save, Tier tier, TierVisual? visual)
    {
        if (visual is null) save.TierVisuals.Remove(tier.ToString());
        else save.TierVisuals[tier.ToString()] = visual;
        scene.SaveProgress();
    }

    private void DrawDesignPicker(CharacterSave save, Tier tier)
    {
        if (!bridges.Glamourer.IsAvailable)
        {
            ImGui.TextColored(Theme.Warning, "Glamourer isn't available. Saved choices are kept for when it's back.");
            return;
        }
        ImGui.SetNextItemWidth(220 * Scale);
        ImGui.InputTextWithHint("##designFilter", "Search designs", ref visualFilter, 64);
        save.TierVisuals.TryGetValue(tier.ToString(), out var current);
        if (!ImGui.BeginListBox("##designs", new Vector2(-1, 140 * Scale))) return;
        foreach (var design in bridges.Glamourer.Designs())
        {
            if (visualFilter.Length > 0 && !design.Path.Contains(visualFilter, StringComparison.OrdinalIgnoreCase)) continue;
            var selected = current is { Kind: VisualKind.Glamourer } && current.DesignId == design.Id;
            if (ImGui.Selectable($"{design.Path}##{design.Id}", selected))
                SetVisual(save, tier, new TierVisual { Kind = VisualKind.Glamourer, DesignId = design.Id, Name = design.Name });
        }
        ImGui.EndListBox();
    }

    private void DrawModPicker(CharacterSave save, Tier tier)
    {
        if (!penumbra.IsAvailable)
        {
            ImGui.TextColored(Theme.Warning, "Penumbra isn't available. Saved choices are kept for when it's back.");
            return;
        }
        if (modList is null || DateTime.UtcNow - modListRead > TimeSpan.FromSeconds(30))
        {
            modList = penumbra.TryGetModList() ?? [];
            modListRead = DateTime.UtcNow;
        }

        save.TierVisuals.TryGetValue(tier.ToString(), out var current);
        ImGui.SetNextItemWidth(220 * Scale);
        ImGui.InputTextWithHint("##modFilter", "Search mods", ref visualFilter, 64);
        if (ImGui.BeginListBox("##mods", new Vector2(-1, 140 * Scale)))
        {
            foreach (var (directory, name) in modList.OrderBy(m => m.Value, StringComparer.OrdinalIgnoreCase))
            {
                if (visualFilter.Length > 0 && !name.Contains(visualFilter, StringComparison.OrdinalIgnoreCase)) continue;
                var selected = current is { Kind: VisualKind.Penumbra } && current.ModDirectory == directory;
                if (ImGui.Selectable($"{name}##{directory}", selected))
                    SetVisual(save, tier, new TierVisual { Kind = VisualKind.Penumbra, ModDirectory = directory, Name = name });
            }
            ImGui.EndListBox();
        }

        if (current is not { Kind: VisualKind.Penumbra } chosen || penumbra.TryGetGroups(chosen.ModDirectory) is not { } groups) return;
        if (groups.Count == 0) Theme.MutedText("This mod has no options: it's simply turned on.");
        foreach (var (group, options) in groups)
        {
            if (options.Length == 0) continue;
            chosen.Options.TryGetValue(group, out var picked);
            ImGui.SetNextItemWidth(220 * Scale);
            if (!ImGui.BeginCombo(group, picked ?? "(as it is now)")) continue;
            if (ImGui.Selectable("(as it is now)", picked is null))
            {
                var copy = Copy(chosen);
                copy.Options.Remove(group);
                SetVisual(save, tier, copy);
            }
            foreach (var option in options)
            {
                if (!ImGui.Selectable(option, option == picked)) continue;
                var copy = Copy(chosen);
                copy.Options[group] = option;
                SetVisual(save, tier, copy);
            }
            ImGui.EndCombo();
        }
    }

    /// A new object, so the service sees the choice change and applies it again.
    private static TierVisual Copy(TierVisual visual) => new()
    {
        Kind = visual.Kind, DesignId = visual.DesignId, ModDirectory = visual.ModDirectory, Name = visual.Name,
        Options = new Dictionary<string, string>(visual.Options),
    };

    private void DrawToys()
    {
        var toys = bridges.Toys;
        Theme.CardBox("toys", () =>
        {
            Theme.Heading(FontAwesomeIcon.Heartbeat, "Toys");
            var enabled = settings.ToysEnabled;
            if (ImGui.Checkbox("Vibrate toys with arousal", ref enabled)) bridges.SetToysEnabled(enabled);
            Theme.MutedWrapped("Through Intiface Central. During a scene, toys follow your arousal, and a climax gives a 3 second burst. The safeword stops them at once.");

            var address = settings.IntifaceAddress;
            ImGui.SetNextItemWidth(220 * Scale);
            if (ImGui.InputText("Intiface address", ref address, 128))
            {
                settings.IntifaceAddress = address.Trim();
                settings.Save();
            }

            var max = settings.ToyMaxIntensity * 100f;
            ImGui.SetNextItemWidth(220 * Scale);
            if (ImGui.SliderFloat("Maximum intensity", ref max, 5f, 100f, "%.0f%%"))
            {
                settings.ToyMaxIntensity = max / 100f;
                settings.Save();
            }

            if (!settings.ToysEnabled) return;
            if (toys.IsConnected)
            {
                Theme.StatusPill("Connected", Theme.Good);
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                Theme.MutedText($"{(toys.DeviceCount == 1 ? "1 device" : $"{toys.DeviceCount} devices")}, now at {bridges.ToyIntensity:P0}");
            }
            else if (toys.IsConnecting)
            {
                Theme.StatusPill("Connecting", Theme.Warning);
            }
            else
            {
                Theme.StatusPill("Can't connect", Theme.Danger);
                ImGui.SameLine();
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Redo, "Retry")) toys.Connect(settings.IntifaceAddress);
                Theme.MutedWrapped(toys.LastError is { } error
                    ? $"Is Intiface Central running, with its server started? ({error})"
                    : "Is Intiface Central running, with its server started?");
            }
        });
    }

    private void DrawMoodles()
    {
        var moodles = bridges.Moodles;
        Theme.CardBox("moodles", () =>
        {
            Theme.Heading(FontAwesomeIcon.Smile, "Moodles");
            var enabled = settings.MoodlesEnabled;
            if (ImGui.Checkbox("Show statuses on my character", ref enabled)) bridges.SetMoodlesEnabled(enabled);
            Theme.MutedWrapped("Aroused, Marked, Watched, Bonded, Refractory and Fading, while they apply. Synced players with Moodles see them. Your other Moodles are never touched.");
            if (!settings.MoodlesEnabled) return;
            if (!moodles.IsAvailable) ImGui.TextColored(Theme.Warning, "Moodles isn't available.");
            else if (moodles.Problem is { } problem) ImGui.TextColored(Theme.Warning, problem);
            else Theme.MutedText(moodles.AppliedCount == 1 ? "1 status on now" : $"{moodles.AppliedCount} statuses on now");
        });
    }

#if DEBUG
    // ── Debug: the simulated player's extra tag ──────────────────────────────

    private int debugStars;

    private void DrawSimulatedExtra(SceneEngine engine)
    {
        var extra = scene.DebugExtra;
        var has = extra is not null;
        if (ImGui.Checkbox("Has the extra tag", ref has)) extra = has ? new ExtraTag(0, null) : null;
        if (extra is { } tag)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100 * Scale);
            debugStars = tag.Stars;
            if (ImGui.SliderInt("Stars", ref debugStars, 0, 15)) tag = tag with { Stars = debugStars };
            ImGui.SameLine();
            var namesMe = tag.BondHash is not null;
            if (ImGui.Checkbox("Names me", ref namesMe) && Plugin.ObjectTable.LocalPlayer is { } me)
                tag = tag with { BondHash = namesMe ? BondHashes.Of($"{me.Name.TextValue}@{me.HomeWorld.Value.Name.ExtractText()}") : null };
            extra = tag;
        }
        scene.DebugExtra = extra;
        Theme.MutedText($"Bond: {engine.Save.BondWith ?? "none"}{(engine.Save.BondFormed ? " (formed)" : "")}; layers worn {engine.LayersWorn}");
    }
#endif
}
