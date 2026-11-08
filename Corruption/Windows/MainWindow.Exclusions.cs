using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Corruption.Core;
using Corruption.Mods;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;

namespace Corruption.Windows;

/// Excluding single animations of a picked mod, such as the cuddles in a big collection.
public sealed partial class MainWindow
{
    private string? exclusionMod;
    private string exclusionSearch = "";
    private int exclusionVersion = -1;
    private string exclusionBuiltFor = "";
    private List<(string Key, string Label)> exclusionRows = [];

    private void DrawExclusionToggle(ModRow mod)
    {
        var excluded = acts.File.ExcludedCount(mod.Directory);
        var open = exclusionMod == mod.Directory;
        var label = excluded == 0 ? "Exclude some" : $"{excluded} excluded";
        WrapAfterName(Theme.IconButtonWidth(open ? FontAwesomeIcon.ChevronUp : FontAwesomeIcon.Ban, label));
        if (ImGuiComponents.IconButtonWithText(open ? FontAwesomeIcon.ChevronUp : FontAwesomeIcon.Ban, $"{label}##excl"))
        {
            exclusionMod = open ? null : mod.Directory;
            exclusionSearch = "";
            exclusionVersion = -1;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Choose animations in this mod that don't count, such as cuddles.");
        if (exclusionMod == mod.Directory) DrawExclusions(mod);
    }

    private void DrawExclusions(ModRow mod)
    {
        var key = $"{mod.Directory}\n{exclusionSearch}";
        if (exclusionVersion != acts.Version || exclusionBuiltFor != key)
        {
            exclusionVersion = acts.Version;
            exclusionBuiltFor = key;
            exclusionRows = acts.Candidates
                .Where(c => c.Pose is not null && string.Equals(c.ModDirectory, mod.Directory, StringComparison.OrdinalIgnoreCase))
                .Select(c => (Key: ActsFile.ExclusionKeyOf(c.Group, c.Option, c.Pose!.Value), Label: $"{c.Group} / {c.Option}  ·  {c.Trigger}"))
                .DistinctBy(r => r.Key)
                .Where(r => exclusionSearch.Length == 0 || r.Label.Contains(exclusionSearch, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        ImGui.Indent(24 * Scale);
        ImGui.SetNextItemWidth(220 * Scale);
        ImGui.InputTextWithHint("##exclSearch", "Search, e.g. cuddle", ref exclusionSearch, 64);
        ImGui.SameLine();
        if (ImGui.Button("Exclude all shown")) acts.SetExcluded(mod.Directory, exclusionRows.Select(r => r.Key), true);
        ImGui.SameLine();
        if (ImGui.Button("Include all shown")) acts.SetExcluded(mod.Directory, exclusionRows.Select(r => r.Key), false);
        Theme.MutedText($"{exclusionRows.Count} shown. Ticked animations count; unticked ones earn nothing.");

        var rowHeight = ImGui.GetFrameHeightWithSpacing();
        var height = Math.Min(260 * Scale, Math.Max(1, exclusionRows.Count) * rowHeight + 8 * Scale);
        if (ImGui.BeginChild("##exclusions", new Vector2(-1, height), true))
        {
            // Only the rows in view are drawn, since a collection can hold thousands.
            var first = Math.Clamp((int)(ImGui.GetScrollY() / rowHeight), 0, exclusionRows.Count);
            var visible = (int)(height / rowHeight) + 2;
            var last = Math.Min(exclusionRows.Count, first + visible);
            if (first > 0) ImGui.Dummy(new Vector2(1, first * rowHeight - ImGui.GetStyle().ItemSpacing.Y));
            for (var i = first; i < last; i++)
            {
                var (rowKey, label) = exclusionRows[i];
                var counts = !acts.File.IsExcluded(mod.Directory, rowKey);
                if (ImGui.Checkbox($"{label}##{i}", ref counts)) acts.SetExcluded(mod.Directory, [rowKey], !counts);
            }
            var below = exclusionRows.Count - last;
            if (below > 0) ImGui.Dummy(new Vector2(1, below * rowHeight - ImGui.GetStyle().ItemSpacing.Y));
        }
        ImGui.EndChild();
        ImGui.Unindent(24 * Scale);
        ImGuiHelpers.ScaledDummy(4f);
    }

    /// On the scene card: the playing animation, to exclude or include in one click.
    private void DrawExcludeCurrent(SceneEngine engine)
    {
        if (engine.Status.Act is { } act)
        {
            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Ban, "Exclude this animation"))
                acts.SetExcluded(act.ModDirectory, [act.ExclusionKey], true);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{act.ModName}: {act.Group} / {act.Option} stops counting. Undo it from the Acts page.");
        }
        else if (engine.Status.Excluded is { } excluded)
        {
            ImGui.TextColored(Theme.Warning, $"{excluded.DisplayName} is excluded.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Count it again")) acts.SetExcluded(excluded.ModDirectory, [excluded.ExclusionKey], false);
        }
    }
}
