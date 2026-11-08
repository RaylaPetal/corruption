using System;
using System.Linq;
using System.Numerics;
using Corruption.Mods;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;

namespace Corruption.Windows;

/// The Acts page: folders and the mods that count.
public sealed partial class MainWindow
{
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
            Theme.MutedWrapped("Reading mods...");
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

    /// The next item on a mod's row, or on the next line lined up under the mod's name when it wouldn't fit.
    private static void WrapAfterName(float width)
        => Theme.SameLineOrWrap(width, ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X);

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
            WrapAfterName(Theme.PillWidth(CountsOf(mod)));
            Theme.Pill(CountsOf(mod), Theme.Violet);
            if (acts.IsNew(mod))
            {
                WrapAfterName(Theme.PillWidth("new"));
                Theme.Pill("new", Theme.Warning);
            }
            return;
        }

        var on = true;
        if (ImGui.Checkbox($"{mod.Name}##mod", ref on)) acts.Unpick(pick);
        WrapAfterName(Theme.PillWidth(CountsOf(mod)));
        Theme.Pill(CountsOf(mod), Theme.Pink);
        if (mod.NotLooping > 0)
        {
            WrapAfterName(Theme.PillWidth($"{mod.NotLooping} can't count"));
            Theme.Pill($"{mod.NotLooping} can't count", Theme.Faint);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("These emotes don't loop, so they can't count as acts.");
        }
        WrapAfterName(Theme.CheckboxWidth("Counts alone"));
        var solo = pick.Solo;
        if (ImGui.Checkbox("Counts alone##solo", ref solo)) acts.SetSolo(mod.Directory, solo);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Its animations also earn with nobody nearby, as a solo scene: half the XP, up to 45 minutes a day. With a partner they count as usual.");
        DrawExclusionToggle(mod);
    }
}
