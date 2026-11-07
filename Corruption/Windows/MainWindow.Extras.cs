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

            var wallet = save.Wallet;
            Theme.Icon(FontAwesomeIcon.Gem, Theme.PinkSoft);
            ImGui.SameLine();
            ImGui.TextUnformatted($"{wallet.Sins} Sins");
            ImGui.SameLine();
            Theme.MutedText(wallet.CheckInStreak > 1
                ? $"Checked in {wallet.CheckInStreak} days in a row. Scenes pay 1 Sin a minute."
                : "Scenes pay 1 Sin a minute.");
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
        if (save.BondWith is not { } with) return;

        Theme.CardBox("bond", () =>
        {
            Theme.Icon(FontAwesomeIcon.Link, Theme.Pink);
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            var level = engine.Bonds.Level;
            if (save.BondFormed) ImGui.TextColored(Theme.PinkSoft, $"Bonded with {NameOf(with)}");
            else ImGui.TextUnformatted($"Waiting for {NameOf(with)} to bond back");
            if (save.BondFormed)
            {
                ImGui.SameLine();
                Theme.Pill($"Bond level {level}", Theme.Pink);
            }
            var label = save.BondFormed ? "Unbond" : "Withdraw";
            Theme.AlignRight(Theme.IconButtonWidth(FontAwesomeIcon.Unlink, label));
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Unlink, label)) engine.Unbond();
            if (!save.BondFormed) return;

            for (var unlocked = 1; unlocked <= level; unlocked++)
            {
                Theme.Icon(FontAwesomeIcon.Heart, Theme.Pink);
                ImGui.SameLine();
                ImGui.TextUnformatted(BondLevels.QuirkOf(unlocked));
            }
            if (BondLevels.UntilNext(save.BondSeconds) is { } next)
            {
                Theme.Icon(FontAwesomeIcon.Lock, Theme.Faint);
                ImGui.SameLine();
                Theme.MutedText($"Level {level + 1} in {ProfileTimers.Describe(next)} of scenes together: {BondLevels.QuirkOf(level + 1)}");
            }
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
        DrawCummySettings(engine);
        DrawPregnancySettings(engine);
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
            else if (visuals.Applied is not null)
                Theme.MutedText(visuals.Superseded
                    ? $"On now: {visuals.AppliedText}. Something else changed your look since, so Corruption won't undo it."
                    : $"On now: {visuals.AppliedText}");

            var slots = VisualSlots(save);
            foreach (var slot in slots)
            {
                slot.Store.TryGetValue(slot.Key, out var choice);
                ImGui.TextColored(slot.Color, slot.Label);
                ImGui.SameLine(150 * Scale);
                Theme.MutedText(choice is null ? "Nothing" : $"{(choice.Kind == VisualKind.Glamourer ? "Design" : "Mod")}: {choice.Name}{OptionsText(choice)}");
            }
            if (save.Pregnancy.Enabled && save.Pregnancy.CanCarry)
                Theme.MutedWrapped("A trimester's visual goes on over your tier's while you're in that trimester, and comes off at the birth.");

            ImGuiHelpers.ScaledDummy(2f);
            var names = slots.Select(slot => slot.Label).ToArray();
            ImGui.SetNextItemWidth(160 * Scale);
            if (ImGui.Combo("Visual to change", ref visualTier, names, names.Length)) visualFilter = "";
            var editing = slots[Math.Clamp(visualTier, 0, slots.Count - 1)];
            if (Theme.Choice("visualKind", VisualKindNames, ref visualKind)) visualFilter = "";

            switch (visualKind)
            {
                case 0:
                    if (editing.Store.ContainsKey(editing.Key) && ImGui.Button($"Clear {editing.Label}'s visual"))
                        SetVisual(editing, null);
                    break;
                case 1:
                    DrawDesignPicker(editing);
                    break;
                default:
                    DrawModPicker(editing);
                    break;
            }
        });
    }

    private static string OptionsText(TierVisual visual)
        => visual.Options.Count == 0 ? "" : $" ({string.Join(", ", visual.Options.Values)})";

    /// Somewhere a visual can be chosen: a tier, or a trimester.
    private sealed record VisualSlot(Dictionary<string, TierVisual> Store, string Key, string Label, Vector4 Color);

    private static List<VisualSlot> VisualSlots(CharacterSave save)
    {
        var slots = VisualTiers.Select(t => new VisualSlot(save.TierVisuals, t.ToString(), t.ToString(), Theme.TierColor(t))).ToList();
        if (save.Pregnancy.Enabled && save.Pregnancy.CanCarry)
            foreach (var trimester in PregnancyVisualPlan.Keys)
                slots.Add(new VisualSlot(save.Pregnancy.Visuals, trimester, $"{trimester} trimester", Theme.Pink));
        return slots;
    }

    private void SetVisual(VisualSlot slot, TierVisual? visual)
    {
        if (visual is null) slot.Store.Remove(slot.Key);
        else slot.Store[slot.Key] = visual;
        scene.SaveProgress();
    }

    private void DrawDesignPicker(VisualSlot slot)
    {
        if (!bridges.Glamourer.IsAvailable)
        {
            ImGui.TextColored(Theme.Warning, "Glamourer isn't available. Saved choices are kept for when it's back.");
            return;
        }
        ImGui.SetNextItemWidth(220 * Scale);
        ImGui.InputTextWithHint("##designFilter", "Search designs", ref visualFilter, 64);
        slot.Store.TryGetValue(slot.Key, out var current);
        if (!ImGui.BeginListBox("##designs", new Vector2(-1, 140 * Scale))) return;
        foreach (var design in bridges.Glamourer.Designs())
        {
            if (visualFilter.Length > 0 && !design.Path.Contains(visualFilter, StringComparison.OrdinalIgnoreCase)) continue;
            var selected = current is { Kind: VisualKind.Glamourer } && current.DesignId == design.Id;
            if (ImGui.Selectable($"{design.Path}##{design.Id}", selected))
                SetVisual(slot, new TierVisual { Kind = VisualKind.Glamourer, DesignId = design.Id, Name = design.Name });
        }
        ImGui.EndListBox();
    }

    private void DrawModPicker(VisualSlot slot)
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

        slot.Store.TryGetValue(slot.Key, out var current);
        ImGui.SetNextItemWidth(220 * Scale);
        ImGui.InputTextWithHint("##modFilter", "Search mods", ref visualFilter, 64);
        if (ImGui.BeginListBox("##mods", new Vector2(-1, 140 * Scale)))
        {
            foreach (var (directory, name) in modList.OrderBy(m => m.Value, StringComparer.OrdinalIgnoreCase))
            {
                if (visualFilter.Length > 0 && !name.Contains(visualFilter, StringComparison.OrdinalIgnoreCase)) continue;
                var selected = current is { Kind: VisualKind.Penumbra } && current.ModDirectory == directory;
                if (ImGui.Selectable($"{name}##{directory}", selected))
                    SetVisual(slot, new TierVisual { Kind = VisualKind.Penumbra, ModDirectory = directory, Name = name });
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
                SetVisual(slot, copy);
            }
            foreach (var option in options)
            {
                if (!ImGui.Selectable(option, option == picked)) continue;
                var copy = Copy(chosen);
                copy.Options[group] = option;
                SetVisual(slot, copy);
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

    private static readonly string[] AnatomyNames = ["Not set", "Penis", "Vagina", "Both"];

    private void DrawCummySettings(SceneEngine engine)
    {
        Theme.CardBox("cummy", () =>
        {
            Theme.Heading(FontAwesomeIcon.Tint, "Cummy");
            var layer = settings.LayerOnPartnerClimax;
            if (ImGui.Checkbox("Get layered when a partner climaxes", ref layer))
            {
                settings.LayerOnPartnerClimax = layer;
                settings.Save();
            }
            Theme.MutedWrapped("Your own Cummy layers you on a random part when your scene partner's climax counts. No pairing is needed, and the parts you block in Cummy are still skipped.");
            if (!cummy.CanLayerSelf) ImGui.TextColored(Theme.Warning, cummy.Status);

            ImGuiHelpers.ScaledDummy(2f);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Your anatomy");
            ImGui.SameLine();
            var anatomy = (int)engine.Save.Anatomy;
            if (Theme.Choice("anatomy", AnatomyNames, ref anatomy)) engine.SetAnatomy((Anatomy)anatomy);
            Theme.MutedWrapped("When you climax, partners' clients aim from here: your penis tip or your vagina. Both shoots from the penis. Not set guesses from your skeleton's bones, which may carry both even when your body shows one.");
        });
    }

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
            Theme.MutedWrapped("Aroused, Marked, Watched, Bonded, Refractory and Fading, and with pregnancy on: Fertile, Pregnant, Craving, Unsatisfied and Recovering, while they apply. Synced players with Moodles see them. Your other Moodles are never touched.");
            if (!settings.MoodlesEnabled) return;
            if (!moodles.IsAvailable) ImGui.TextColored(Theme.Warning, "Moodles isn't available.");
            else if (moodles.Problem is { } problem) ImGui.TextColored(Theme.Warning, problem);
            else Theme.MutedText(moodles.AppliedCount == 1 ? "1 status on now" : $"{moodles.AppliedCount} statuses on now");
        });
    }

#if DEBUG
    // ── Debug: the simulated player's extra tag ──────────────────────────────

    private int debugStars;

    private int debugPersona;

    private void DrawSimulatedExtra(SceneEngine engine)
    {
        // The simulated player's personality, for chemistry and the Nearby card. "None" is a client without personas.
        var names = PersonaCatalog.Personalities.Select(p => p.Name).Prepend("None").ToArray();
        ImGui.SetNextItemWidth(160 * Scale);
        if (ImGui.Combo("Their personality", ref debugPersona, names, names.Length))
            scene.DebugPersona = debugPersona == 0 ? null : new PersonaTag(PersonaCatalog.Personalities[debugPersona - 1].Id, [PersonaIds.Sensitive]);
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

        var breeds = scene.DebugBody is { CanBreed: true };
        if (ImGui.Checkbox("Simulated player can breed", ref breeds))
            scene.DebugBody = new BodyTag(breeds ? BodyFlags.CanBreed : BodyFlags.None, PregnancyStage.None, null);

        if (ImGui.Button("+100 Sins")) engine.Save.Wallet.Sins += 100;
    }
#endif
}
