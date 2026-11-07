using System;
using System.Linq;
using System.Numerics;
using Corruption.Core;
using Corruption.Game;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

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
