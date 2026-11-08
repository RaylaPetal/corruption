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

        foreach (var (category, name) in ItemCatalog.Categories)
        {
            var items = ItemBook.Offered(save).Where(i => ItemCatalog.CategoryOf(i) == category).ToList();
            if (items.Count == 0) continue;
            if (!ImGui.CollapsingHeader($"{name}###shop{category}", ImGuiTreeNodeFlags.DefaultOpen)) continue;
            foreach (var item in items) DrawShopItem(engine, item);
        }
        if (!save.Pregnancy.Enabled) Theme.MutedWrapped("Turn pregnancy on in Settings for fertility and pregnancy items.");

        DrawShopTitles(engine);
    }

    private void DrawShopItem(SceneEngine engine, ItemDef item)
    {
        var save = engine.Save;
        Theme.CardBox($"item{item.Id}", () =>
        {
            Theme.Icon(ItemIcons.Of(item.Id), item.Role == ItemRole.Anyone ? Theme.Violet : Theme.Pink);
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(item.Name);
            ImGui.SameLine();
            Theme.Pill(ItemIcons.Lasts(item), Theme.Muted);
            var price = ItemBook.PriceFor(save, item);
            var label = $"{price} Sins";
            Theme.AlignRight(Theme.IconButtonWidth(FontAwesomeIcon.ShoppingCart, label));
            var afford = save.Wallet.Sins >= price;
            if (!afford) ImGui.BeginDisabled();
            if (Theme.PrimaryButton(FontAwesomeIcon.ShoppingCart, $"{label}##buy{item.Id}"))
                shopMessage = engine.BuyItem(item.Id) ?? $"Bought {item.Name}. It's in your inventory.";
            if (!afford) ImGui.EndDisabled();
            if (!afford && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip($"You need {price - save.Wallet.Sins} more Sins.");
            Theme.MutedText(item.Effect);
        });
    }

    /// Titles for sale: bought once and kept, worn from Profile.
    private void DrawShopTitles(SceneEngine engine)
    {
        var save = engine.Save;
        if (!ImGui.CollapsingHeader("Titles###shopTitles", ImGuiTreeNodeFlags.DefaultOpen)) return;
        Theme.MutedWrapped("Yours for good once bought. Wear one from the title picker on Profile: only the one you wear gives its bonus.");
        foreach (var title in TitleCatalog.All.Where(t => t.Source == TitleSource.Shop))
        {
            var color = new System.Numerics.Vector4(PlateComposer.TitleColor(title), 1f);
            Theme.CardBox($"title{title.Id}", () =>
            {
                Theme.Icon(FontAwesomeIcon.Crown, color);
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(color, title.Name);
                if (TitleBook.IsUnlocked(save, title.Id))
                {
                    var state = TitleBook.Equipped(save)?.Id == title.Id ? "Wearing" : "Owned";
                    Theme.AlignRight(Theme.PillWidth(state));
                    Theme.Pill(state, Theme.Good);
                }
                else
                {
                    var label = $"{title.Price} Sins";
                    Theme.AlignRight(Theme.IconButtonWidth(FontAwesomeIcon.ShoppingCart, label));
                    var refusal = TitleBook.BuyRefusal(save, title);
                    if (refusal is not null) ImGui.BeginDisabled();
                    if (Theme.PrimaryButton(FontAwesomeIcon.ShoppingCart, $"{label}##buy{title.Id}"))
                        shopMessage = engine.BuyTitle(title.Id) ?? $"Bought the {title.Name} title. Wear it from Profile.";
                    if (refusal is not null)
                    {
                        ImGui.EndDisabled();
                        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(refusal);
                    }
                }
                Theme.MutedText(title.IsCosmetic ? "Just for show, in a colour of its own" : title.Effect);
            }, stripe: color);
        }
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
                Theme.MutedWrapped($"{ItemIcons.Remaining(effect, now)}: {item.Effect}");
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
