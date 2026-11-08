using System;
using System.Linq;
using System.Numerics;
using Corruption.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Corruption.Windows;

/// The title picker on the Profile page: the equipped title and its bonus, and every title by source.
public sealed partial class MainWindow
{
    private string? titleMessage;

    private static readonly (TitleSource Source, string Heading)[] TitleGroups =
    [
        (TitleSource.Level, "Levels"),
        (TitleSource.Prestige, "Prestige"),
        (TitleSource.Achievement, "Achievements"),
        (TitleSource.Hidden, "Hidden"),
        (TitleSource.Moon, "The moon"),
        (TitleSource.Shop, "Shop"),
    ];

    private void DrawTitleCard(SceneEngine engine)
    {
        var save = engine.Save;
        var now = DateTime.UtcNow;
        var equipped = TitleBook.Equipped(save);
        var locked = engine.TitleLock(now);

        Theme.CardBox("title", () =>
        {
            Theme.Heading(FontAwesomeIcon.Crown, "Your title");
            if (equipped is null)
            {
                Theme.MutedText("None. Pick one below: only the one you wear gives its bonus.");
            }
            else
            {
                ImGui.TextColored(new Vector4(PlateComposer.TitleColor(equipped), 1f), equipped.Name);
                ImGui.SameLine();
                Theme.MutedWrapped(equipped.IsCosmetic ? "Just for show" : equipped.Effect);
            }
            if (locked is not null)
            {
                Theme.Icon(FontAwesomeIcon.Lock, Theme.Warning);
                ImGui.SameLine();
                ImGui.TextColored(Theme.Warning, locked);
            }
            if (titleMessage is not null) Theme.MutedWrapped(titleMessage);

            var unlocked = save.Titles.Unlocked.Count(id => TitleCatalog.Find(id) is not null);
            if (!ImGui.TreeNode($"Choose a title ({unlocked} of {TitleCatalog.All.Count} unlocked)###titles")) return;

            if (equipped is not null && TitleButton("Take it off", "none", locked)) Equip(engine, null);
            foreach (var (source, heading) in TitleGroups)
            {
                ImGui.Spacing();
                Theme.MutedText(heading);
                foreach (var title in TitleCatalog.All.Where(t => t.Source == source))
                    DrawTitleRow(engine, title, equipped, locked);
            }
            ImGui.TreePop();
        }, stripe: equipped is null ? null : new Vector4(PlateComposer.TitleColor(equipped), 1f));
    }

    private void DrawTitleRow(SceneEngine engine, TitleDef title, TitleDef? equipped, string? locked)
    {
        var save = engine.Save;
        var owned = TitleBook.IsUnlocked(save, title.Id);
        ImGui.PushID(title.Id);

        // A hidden title stays a secret until its achievement is earned.
        if (title.Source == TitleSource.Hidden && !owned)
        {
            Theme.Icon(FontAwesomeIcon.Lock, Theme.Faint);
            ImGui.SameLine();
            ImGui.TextColored(Theme.Faint, "???");
            ImGui.PopID();
            return;
        }

        Theme.Icon(owned ? FontAwesomeIcon.Crown : FontAwesomeIcon.Lock, owned ? new Vector4(PlateComposer.TitleColor(title), 1f) : Theme.Faint);
        ImGui.SameLine();
        if (owned) ImGui.TextColored(new Vector4(PlateComposer.TitleColor(title), 1f), title.Name);
        else ImGui.TextColored(Theme.Muted, title.Name);
        ImGui.SameLine();
        Theme.MutedWrapped(title.IsCosmetic ? "Just for show" : title.Effect);

        if (equipped?.Id == title.Id)
        {
            Theme.AlignRight(Theme.PillWidth("Wearing"));
            Theme.Pill("Wearing", Theme.Good);
        }
        else if (owned)
        {
            if (TitleButton("Wear", title.Id, locked)) Equip(engine, title.Id);
        }
        else
        {
            var how = TitleCatalog.HowTo(title);
            Theme.AlignRight(ImGui.CalcTextSize(how).X);
            ImGui.TextColored(Theme.Faint, how);
        }
        ImGui.PopID();
    }

    /// A right-aligned button, greyed out with the reason while titles are locked.
    private static bool TitleButton(string label, string id, string? locked)
    {
        Theme.AlignRight(ImGui.CalcTextSize(label).X + ImGui.GetStyle().FramePadding.X * 2);
        if (locked is not null) ImGui.BeginDisabled();
        var clicked = ImGui.SmallButton($"{label}##{id}");
        if (locked is not null)
        {
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(locked);
        }
        return clicked;
    }

    /// The title tag follows on the engine's next sample.
    private void Equip(SceneEngine engine, string? id) => titleMessage = engine.TryEquipTitle(id, DateTime.UtcNow);
}
