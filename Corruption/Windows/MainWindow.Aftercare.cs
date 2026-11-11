using System;
using System.Collections.Generic;
using System.Linq;
using Corruption.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Corruption.Windows;

/// Lick clean after a climax, and what Cummy says is layered now.
public sealed partial class MainWindow
{
    /// How long the last lick's answer stays on screen after the offer closes.
    private static readonly TimeSpan LickMessageFor = TimeSpan.FromSeconds(20);

    /// The Lick clean button and its state. With <paramref name="onlyFor"/>, only when the offer is for that player,
    /// such as on their Nearby card.
    private void DrawLickClean(SceneEngine engine, PeerRecord? onlyFor)
    {
        var now = DateTime.UtcNow;
        var open = engine.LickOfferAt(now);
        var last = engine.LastLick;
        var shown = open ?? (last is { Message: not null } && now - (last.Until - AftercareRules.OfferFor) < AftercareRules.OfferFor + LickMessageFor ? last : null);
        if (shown is null) return;
        if (onlyFor is not null && !shown.Partner.Matches(onlyFor.Name, onlyFor.World)) return;

        if (open is { State: LickState.Offered })
        {
            if (ImGui.Button($"Lick {open.Partner.Name} clean##lick{onlyFor?.Key}"))
                engine.TryLickClean(now);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(open.Hole is { } hole
                    ? $"You came inside their {hole}: lick it out. Cummy's own rules decide if it goes through."
                    : "Lick off the part your climax most likely landed on. Cummy's own rules decide if it goes through.");
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            Theme.MutedText($"{(int)open.Left(now).TotalSeconds}s left");
        }
        else if (open is { State: LickState.Waiting })
        {
            Theme.Icon(FontAwesomeIcon.Tint, Theme.PinkSoft);
            ImGui.SameLine();
            Theme.MutedText($"Licking {open.Partner.Name}...");
        }
        if (shown.Message is { } message)
            ImGui.TextColored(shown.State == LickState.Paid ? Theme.Good : Theme.Warning, message);
    }

    /// Cummy version 5: each layered part with its stage out of its stage count, and the blocked parts. Version 6 adds
    /// each dripping hole with the time it has left.
    private void DrawLayeredNow()
    {
        if (!cummy.HasV5) return;
        var layered = cummy.Stages.Where(s => s.Value > 0).ToList();
        var dripping = DrippingLeft();
        if (layered.Count == 0 && cummy.Blocked.Count == 0 && dripping.Count == 0) return;
        ImGui.Spacing();
        Theme.MutedText("Layered now");
        var first = true;
        foreach (var (part, stage) in layered.OrderBy(s => CummyParts.All.ToList().IndexOf(s.Key)))
        {
            var label = $"{cummy.PartNames.GetValueOrDefault(part, part)} {stage}/{cummy.PartSizes.GetValueOrDefault(part, stage)}";
            if (first) Theme.Pill(label, Theme.VioletSoft);
            else Theme.NextPill(label, Theme.VioletSoft);
            first = false;
        }
        if (layered.Count == 0) Theme.MutedText("Nothing layered right now.");
        foreach (var (hole, left) in dripping)
        {
            Theme.Icon(FontAwesomeIcon.Tint, Theme.PinkSoft);
            ImGui.SameLine();
            Theme.MutedText($"{(hole == CreampieAim.Pussy ? "Pussy" : "Ass")} dripping, {(int)left.TotalMinutes}:{left.Seconds:00}");
        }
        if (cummy.Blocked.Count > 0)
            Theme.MutedWrapped($"Blocked: {string.Join(", ", cummy.Blocked.Select(p => cummy.PartNames.GetValueOrDefault(p, p)))}");
    }

    /// Each dripping hole and the time it has left, counted down from Cummy's last answer. Empty below version 6.
    private List<(string Hole, TimeSpan Left)> DrippingLeft()
    {
        if (!cummy.HasV6) return [];
        var since = DateTime.UtcNow - cummy.CreampiesReadUtc;
        return [.. new[] { CreampieAim.Pussy, CreampieAim.Ass }
            .Where(cummy.Creampies.ContainsKey)
            .Select(h => (h, TimeSpan.FromSeconds(cummy.Creampies[h]) - since))
            .Where(d => d.Item2 > TimeSpan.Zero)];
    }
}
