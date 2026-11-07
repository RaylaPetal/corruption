using System;
using System.Linq;
using Corruption.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Corruption.Windows;

/// The shop: Sins, what's for sale, what's owned and what's working.
public sealed partial class MainWindow
{
    private string? shopMessage;

    private void DrawShop(SceneEngine engine)
    {
        var save = engine.Save;
        var now = DateTime.UtcNow;

        Theme.CardBox("wallet", () =>
        {
            Theme.Icon(FontAwesomeIcon.Gem, Theme.Pink);
            ImGui.SameLine();
            ImGui.SetWindowFontScale(1.25f);
            ImGui.TextColored(Theme.PinkSoft, $"{save.Wallet.Sins} Sins");
            ImGui.SetWindowFontScale(1f);
            Theme.MutedText("Earned from your daily check-in, scenes, levels, goals and achievements.");
            if (shopMessage is { } message) ImGui.TextColored(Theme.Warning, message);
        }, stripe: Theme.Pink);

        DrawActiveEffects(save, now);
        DrawInventory(engine, now);

        Theme.Heading(FontAwesomeIcon.Store, "For sale");
        foreach (var item in ItemBook.Offered(save))
        {
            Theme.CardBox($"item{item.Id}", () =>
            {
                Theme.Icon(ItemIcons.Of(item.Id), item.Role == ItemRole.Anyone ? Theme.Violet : Theme.Pink);
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(item.Name);
                ImGui.SameLine();
                Theme.Pill(ItemIcons.Lasts(item), Theme.Muted);
                var label = $"{item.Price} Sins";
                Theme.AlignRight(Theme.IconButtonWidth(FontAwesomeIcon.ShoppingCart, label));
                var afford = save.Wallet.Sins >= item.Price;
                if (!afford) ImGui.BeginDisabled();
                if (Theme.PrimaryButton(FontAwesomeIcon.ShoppingCart, $"{label}##buy{item.Id}"))
                    shopMessage = engine.BuyItem(item.Id) ?? $"Bought {item.Name}. It's in your inventory.";
                if (!afford) ImGui.EndDisabled();
                if (!afford && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip($"You need {item.Price - save.Wallet.Sins} more Sins.");
                Theme.MutedText(item.Effect);
            });
        }
        if (!save.Pregnancy.Enabled) Theme.MutedWrapped("Turn pregnancy on in Settings for fertility and pregnancy items.");
    }

    private static void DrawActiveEffects(CharacterSave save, DateTime now)
    {
        var active = save.Effects.Where(e => ItemBook.IsActive(save, e.Key, now)).ToList();
        if (active.Count == 0) return;
        Theme.CardBox("effects", () =>
        {
            Theme.Heading(FontAwesomeIcon.Magic, "Working now");
            foreach (var (id, effect) in active)
            {
                if (ItemCatalog.Find(id) is not { } item) continue;
                Theme.Icon(ItemIcons.Of(id), Theme.Pink);
                ImGui.SameLine();
                ImGui.TextUnformatted(item.Name);
                ImGui.SameLine();
                Theme.MutedText($"{ItemIcons.Remaining(effect, now)}: {item.Effect}");
            }
        }, stripe: Theme.Violet);
    }

    private void DrawInventory(SceneEngine engine, DateTime now)
    {
        var owned = engine.Save.Inventory.Where(i => i.Value > 0).ToList();
        if (owned.Count == 0) return;
        Theme.CardBox("inventory", () =>
        {
            Theme.Heading(FontAwesomeIcon.Box, "Your items");
            foreach (var (id, count) in owned)
            {
                if (ItemCatalog.Find(id) is not { } item) continue;
                Theme.Icon(ItemIcons.Of(id), Theme.Violet);
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(count > 1 ? $"{item.Name} ×{count}" : item.Name);
                Theme.AlignRight(Theme.IconButtonWidth(FontAwesomeIcon.HandSparkles, "Use"));
                if (Theme.PrimaryButton(FontAwesomeIcon.HandSparkles, $"Use##use{id}"))
                    shopMessage = engine.UseItem(id, now);
            }
        });
        ImGuiHelpers.ScaledDummy(2f);
    }
}
