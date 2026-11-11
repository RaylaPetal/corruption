using System;
using System.Collections.Generic;
using System.Linq;
using Corruption.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Corruption.Windows;

/// The Nearby page: hiding, the bond, who's around and integrations.
public sealed partial class MainWindow
{
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

        if (!heels.IsAvailable)
            ImGui.TextColored(Theme.Warning, "SimpleHeels is unavailable, and it's required to find other players.");

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
                Theme.MutedWrapped($"@{nearby.Player.World}");
                Theme.NextPill($"{tier} ♥ Lv. {tag.Level}", Theme.TierColor(tier));
                if (engine.StarsOf(nearby.Player) is > 0 and var stars)
                {
                    Theme.NextPill(PrestigeRules.Label(stars, balance.StarsWithBonus), Theme.Warning);
                }
                if (TitleCatalog.Find(engine.TitleOf(nearby.Player)) is { } title)
                {
                    Theme.NextPill(title.Name, new System.Numerics.Vector4(PlateComposer.TitleColor(title), 1f));
                }
                Theme.NextPill($"{tag.Arousal}%", Theme.Pink);
                Theme.NextPill(tag.Paused ? "Paused" : tag.Pose is not null ? "In an act" : "Idle", tag.Paused ? Theme.Danger : tag.Pose is not null ? Theme.Good : Theme.Muted);
                var distance = $"{nearby.Distance:0.#} y";
                Theme.AlignRight(ImGui.CalcTextSize(distance).X);
                ImGui.AlignTextToFramePadding();
                Theme.MutedText(distance);
                DrawNearbyPersona(engine, nearby.Player);
                if (PartnerBook.Of(engine.Save, nearby.Player.Key) is { Scenes: > 0 } history)
                    Theme.MutedText(History(history));
                DrawLickClean(engine, nearby.Player);
                DrawBondRow(engine, nearby);
            }, stripe: isCurrent ? Theme.Pink : null);
        }

        DrawPastPartners(engine);
    }

    /// Their personality and traits, with what each does on hover, and a mark when your personalities suit each other.
    private static void DrawNearbyPersona(SceneEngine engine, PeerRecord player)
    {
        if (engine.PersonaOf(player) is not { } persona) return;
        var drew = false;
        void Next()
        {
            if (drew) ImGui.SameLine();
            drew = true;
        }
        if (persona.KnownPersonality is { Id: not PersonaIds.Easygoing } personality)
        {
            Next();
            Theme.Pill(personality.Name, Theme.Violet);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{personality.Upside}. {personality.Downside}.");
        }
        foreach (var trait in persona.KnownTraits)
        {
            Next();
            Theme.Pill(trait.Name, Theme.PinkSoft);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{trait.Upside}. {trait.Downside}.");
        }
        if (engine.HasChemistryWith(player))
        {
            Next();
            Theme.Icon(FontAwesomeIcon.HeartCircleBolt, Theme.Pink);
            ImGui.SameLine();
            ImGui.TextColored(Theme.Pink, "Chemistry");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Your personalities suit each other: scenes together pay ×{PersonaCatalog.ChemistryXp:0.##} XP.");
        }
    }

    /// Everyone this character has had a scene with, most recent first.
    private static void DrawPastPartners(SceneEngine engine)
    {
        var partners = engine.Save.Partners.Where(p => p.Value.Scenes > 0)
            .OrderByDescending(p => p.Value.LastUtc ?? DateTime.MinValue).ToList();
        if (partners.Count == 0) return;
        ImGuiHelpers.ScaledDummy(2f);
        if (!ImGui.TreeNode($"Past partners ({partners.Count})###pastPartners")) return;
        foreach (var (key, record) in partners)
        {
            var at = key.IndexOf('@');
            ImGui.TextUnformatted(at > 0 ? key[..at] : key);
            if (at > 0)
            {
                ImGui.SameLine();
                Theme.MutedWrapped(key[at..]);
            }
            ImGui.SameLine();
            Theme.MutedWrapped(History(record));
        }
        ImGui.TreePop();
    }

    /// Like "5 scenes · 2h 10m together · last 3d ago".
    private static string History(PartnerRecord record)
    {
        var scenes = record.Scenes == 1 ? "1 scene" : $"{record.Scenes} scenes";
        var parts = new List<string> { scenes };
        if (record.Seconds >= 60) parts.Add($"{Together(record.Seconds)} together");
        parts.Add(record.LastUtc is { } last ? $"last {Ago(DateTime.UtcNow - last)}" : "last scene unknown");
        return string.Join(" · ", parts);
    }

    private static string Together(double seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m" : $"{span.Minutes}m";
    }
}
