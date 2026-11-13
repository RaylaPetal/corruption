using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Corruption.Core;
using Corruption.Game;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Lumina.Excel.Sheets;

namespace Corruption.Windows;

/// The Debug page, in dev builds only.
public sealed partial class MainWindow
{
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
        ImGuiHelpers.ScaledDummy(4f);
        DrawStatusIconBrowser();
        ImGuiHelpers.ScaledDummy(4f);
        DrawApiSelfTest();
    }

    // ── Plugin API self-test ────────────────────────────────────────────────

    /// Corruption's own client file, used as another plugin would, so the gates and the client are checked together.
    private CorruptionIpcClient.CorruptionIpc? apiClient;
    private readonly List<string> apiEvents = [];
    private string apiLastWrite = "";
    private float apiBreedFactor = 1;

    /// Drops the self-test's client, so its event subscriptions end with the plugin.
    public void DisposeDebug() => apiClient?.Dispose();

    private void LogApiEvent(string line)
    {
        apiEvents.Add($"{DateTime.Now:HH:mm:ss} {line}");
        if (apiEvents.Count > 14) apiEvents.RemoveAt(0);
    }

    private CorruptionIpcClient.CorruptionIpc ApiClient()
    {
        if (apiClient is not null) return apiClient;
        var c = new CorruptionIpcClient.CorruptionIpc(Plugin.PluginInterface);
        c.Ready += () => LogApiEvent("Ready");
        c.Climax += () => LogApiEvent("Climax");
        c.PartnerClimax += (n, w) => LogApiEvent($"PartnerClimax {n}@{w}");
        c.SceneStarted += (n, w, solo) => LogApiEvent($"SceneStarted {n}@{w} solo={solo}");
        c.SceneEnded += (sec, xp) => LogApiEvent($"SceneEnded {sec:0}s {xp:0} XP");
        c.LevelUp += l => LogApiEvent($"LevelUp {l}");
        c.CyclePhaseChanged += p => LogApiEvent($"CyclePhaseChanged {p}");
        c.Conceived += (n, w, src) => LogApiEvent($"Conceived {n}@{w} via {src}");
        c.TrimesterChanged += t => LogApiEvent($"TrimesterChanged {t}");
        c.Birth += (n, w) => LogApiEvent($"Birth {n}@{w}");
        c.RecoveryEnded += () => LogApiEvent("RecoveryEnded");
        c.ArousalChanged += a => LogApiEvent($"ArousalChanged {a}");
        c.StateChanged += () => LogApiEvent("StateChanged");
        return apiClient = c;
    }

    private void DrawApiSelfTest()
    {
        Theme.Heading(FontAwesomeIcon.Plug, "Plugin API self-test");
        var c = ApiClient();
        Theme.MutedText($"API version {c.ApiVersion()}, available {c.IsAvailable}");
        Theme.MutedWrapped(c.GetProfile().ToString());
        Theme.MutedWrapped(c.GetArousal().ToString());
        Theme.MutedWrapped(c.GetScene().ToString());
        Theme.MutedWrapped(c.GetBond().ToString());
        Theme.MutedWrapped(c.GetCycle().ToString());
        Theme.MutedWrapped(c.GetPregnancy().ToString());
        Theme.MutedText($"Births: {c.GetBirths().Count}");
        foreach (var n in c.GetNearby()) Theme.MutedWrapped(n.ToString());

        var target = Plugin.TargetManager.Target as IPlayerCharacter;
        var (name, world) = target is null ? ("", "") : (target.Name.TextValue, target.HomeWorld.Value.Name.ExtractText());
        ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
        ImGui.SliderFloat("Chance factor##apiFactor", ref apiBreedFactor, 0.25f, 3f, "×%.2f");
        if (target is not null) Theme.MutedText($"Breed chance from {name}: {c.BreedChance(name, world, apiBreedFactor):P1}");
        if (ImGui.Button("Breed me (target)") && target is not null) apiLastWrite = $"Breed: {c.Breed(name, world, "Self-test", apiBreedFactor)}";
        ImGui.SameLine();
        if (ImGui.Button("Arousal +10")) apiLastWrite = $"AddArousal: {c.AddArousal(10, "Self-test")}";
        ImGui.SameLine();
        if (ImGui.Button("Arousal -10")) apiLastWrite = $"AddArousal: {c.AddArousal(-10, "Self-test")}";
        ImGui.SameLine();
        if (ImGui.Button("Climax")) apiLastWrite = $"Climax: {c.RequestClimax("Self-test")}";
        var forced = c.GetForceCreampie();
        if (ImGui.Checkbox("My climaxes always creampie##apiForce", ref forced))
            apiLastWrite = $"SetForceCreampie: {c.SetForceCreampie("Self-test", forced)}";
        if (apiLastWrite.Length > 0) Theme.MutedText(apiLastWrite);
        Theme.MutedText("Events:");
        foreach (var line in apiEvents) Theme.MutedText(line);
    }

    // ── Status icon browser ─────────────────────────────────────────────────

    /// Every game status with an icon, one entry per icon: (icon, name). Read once.
    private List<(uint Icon, string Name)>? statusIcons;
    private string iconSearch = "";
    private uint pickedIcon;

    /// Finds game status icons by name, for pinning the Moodles icons of the cum statuses. Click one to copy its id.
    private void DrawStatusIconBrowser()
    {
        Theme.Heading(FontAwesomeIcon.Icons, "Status icons");
        statusIcons ??= Plugin.DataManager.GetExcelSheet<Status>(ClientLanguage.English)
            .Where(r => r.Icon != 0 && !r.Name.IsEmpty)
            .Select(r => (r.Icon, Name: r.Name.ExtractText()))
            .DistinctBy(r => r.Icon)
            .ToList();
        ImGui.SetNextItemWidth(220 * Scale);
        ImGui.InputTextWithHint("##iconSearch", "Search by status name", ref iconSearch, 64);
        ImGui.SameLine();
        Theme.MutedText(pickedIcon == 0 ? "Click an icon to pick it." : $"Picked {pickedIcon} (copied).");
        if (pickedIcon != 0)
        {
            if (ImGui.Button("Preview as layers"))
                bridges.Moodles.Preview(MoodleKind.Marked, 3, (int)pickedIcon, "Corruption");
            ImGui.SameLine();
            if (ImGui.Button("Preview as creampie"))
                bridges.Moodles.Preview(MoodleKind.Dripping, MoodlesPlan.DrippingPussy, (int)pickedIcon, "Corruption");
        }
        if (iconSearch.Trim().Length < 2)
        {
            Theme.MutedText("Type at least two letters.");
            return;
        }
        var matches = statusIcons.Where(s => s.Name.Contains(iconSearch.Trim(), StringComparison.OrdinalIgnoreCase)).Take(200).ToList();
        Theme.MutedText(matches.Count == 200 ? "The first 200 matches." : $"{matches.Count} matches.");
        var size = new Vector2(32 * Scale, 42 * Scale);
        var perRow = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (size.X + ImGui.GetStyle().ItemSpacing.X)));
        for (var i = 0; i < matches.Count; i++)
        {
            var (icon, name) = matches[i];
            if (i % perRow != 0) ImGui.SameLine();
            var wrap = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(icon)).GetWrapOrEmpty();
            ImGui.PushID((int)icon);
            if (ImGui.ImageButton(wrap.Handle, size with { Y = size.X * wrap.Height / Math.Max(1, wrap.Width) }))
            {
                pickedIcon = icon;
                ImGui.SetClipboardText(icon.ToString());
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{name}\nIcon {icon}");
            ImGui.PopID();
        }
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
