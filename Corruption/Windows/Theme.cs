using System;
using System.Numerics;
using Corruption.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Corruption.Windows;

/// The plum, violet and pink look. Every colour and shared widget lives here.
internal static class Theme
{
    private static float Scale => ImGuiHelpers.GlobalScale;

    // Surfaces, darkest to lightest.
    public static readonly Vector4 WindowBg = Hex(0x150D1F, 0.97f);
    public static readonly Vector4 Card = Hex(0x21142F);
    public static readonly Vector4 CardHover = Hex(0x2C1B40);
    public static readonly Vector4 Field = Hex(0x2C1B40);
    public static readonly Vector4 FieldHover = Hex(0x3A2452);
    public static readonly Vector4 FieldActive = Hex(0x482D66);
    public static readonly Vector4 Border = Hex(0x54347A, 0.65f);

    public static readonly Vector4 Pink = Hex(0xFF5FA8);
    public static readonly Vector4 PinkSoft = Hex(0xFF9CCB);
    public static readonly Vector4 PinkDeep = Hex(0xC92C78);
    public static readonly Vector4 Violet = Hex(0xA66BFF);
    public static readonly Vector4 VioletSoft = Hex(0xCDAEFF);
    public static readonly Vector4 VioletDeep = Hex(0x5E35B8);

    public static readonly Vector4 Text = Hex(0xF5EAF8);
    public static readonly Vector4 Muted = Hex(0xA893BC);
    public static readonly Vector4 Faint = Hex(0x6A5580);
    public static readonly Vector4 Good = Hex(0x74E8BC);
    public static readonly Vector4 Warning = Hex(0xFFC56E);
    public static readonly Vector4 Danger = Hex(0xFF6680);

    private static Vector4 Hex(uint rgb, float alpha = 1f)
        => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

    public static Vector4 Alpha(Vector4 color, float alpha) => color with { W = alpha };

    private static uint U32(Vector4 color) => ImGui.GetColorU32(color);

    // ── Whole-UI style ───────────────────────────────────────────────────────

    /// Pushed once around all of Corruption's windows, so a push can never go unmatched and nothing reaches other
    /// plugins' windows.
    public static IDisposable Push()
    {
        var colors = ImRaii.PushColor(ImGuiCol.WindowBg, WindowBg)
            .Push(ImGuiCol.ChildBg, Vector4.Zero)
            .Push(ImGuiCol.PopupBg, Hex(0x1B1128, 0.98f))
            .Push(ImGuiCol.Border, Border)
            .Push(ImGuiCol.Text, Text)
            .Push(ImGuiCol.TextDisabled, Muted)
            .Push(ImGuiCol.TextSelectedBg, Alpha(Pink, 0.35f))
            .Push(ImGuiCol.TitleBg, Hex(0x1B1128))
            .Push(ImGuiCol.TitleBgActive, Hex(0x3A1E5C))
            .Push(ImGuiCol.TitleBgCollapsed, Hex(0x1B1128))
            .Push(ImGuiCol.FrameBg, Field)
            .Push(ImGuiCol.FrameBgHovered, FieldHover)
            .Push(ImGuiCol.FrameBgActive, FieldActive)
            .Push(ImGuiCol.Button, Hex(0x432A66))
            .Push(ImGuiCol.ButtonHovered, Hex(0x5E3B8F))
            .Push(ImGuiCol.ButtonActive, PinkDeep)
            .Push(ImGuiCol.Header, Hex(0x3A2452))
            .Push(ImGuiCol.HeaderHovered, Hex(0x54347A))
            .Push(ImGuiCol.HeaderActive, PinkDeep)
            .Push(ImGuiCol.CheckMark, Pink)
            .Push(ImGuiCol.SliderGrab, Pink)
            .Push(ImGuiCol.SliderGrabActive, PinkSoft)
            .Push(ImGuiCol.Separator, Border)
            .Push(ImGuiCol.SeparatorHovered, Violet)
            .Push(ImGuiCol.SeparatorActive, Pink)
            .Push(ImGuiCol.ResizeGrip, Alpha(Violet, 0.25f))
            .Push(ImGuiCol.ResizeGripHovered, Violet)
            .Push(ImGuiCol.ResizeGripActive, Pink)
            .Push(ImGuiCol.ScrollbarBg, Vector4.Zero)
            .Push(ImGuiCol.ScrollbarGrab, Hex(0x54347A))
            .Push(ImGuiCol.ScrollbarGrabHovered, Violet)
            .Push(ImGuiCol.ScrollbarGrabActive, Pink);

        var style = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, 10f * Scale)
            .Push(ImGuiStyleVar.ChildRounding, 8f * Scale)
            .Push(ImGuiStyleVar.FrameRounding, 6f * Scale)
            .Push(ImGuiStyleVar.PopupRounding, 8f * Scale)
            .Push(ImGuiStyleVar.GrabRounding, 5f * Scale)
            .Push(ImGuiStyleVar.ScrollbarRounding, 6f * Scale)
            .Push(ImGuiStyleVar.ScrollbarSize, 10f * Scale)
            .Push(ImGuiStyleVar.WindowBorderSize, 1f)
            .Push(ImGuiStyleVar.WindowPadding, new Vector2(12f, 12f) * Scale);

        return new Scope(style, colors);
    }

    /// The roomier frames and spacing, pushed inside each window's content. Not around the window itself: the title bar's
    /// height and close button follow FramePadding, while Dalamud places its own title bar button for the default, so a
    /// larger padding there draws the two on top of each other.
    public static IDisposable Content()
        => ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(8f, 5f) * Scale)
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(7f, 7f) * Scale);

    private sealed class Scope(IDisposable style, IDisposable colors) : IDisposable
    {
        public void Dispose()
        {
            style.Dispose();
            colors.Dispose();
        }
    }

    // ── Text ─────────────────────────────────────────────────────────────────

    public static void MutedText(string text) => ImGui.TextColored(Muted, text);

    public static void MutedWrapped(string text)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Text, Muted);
        ImGui.TextWrapped(text);
    }

    public static void Icon(FontAwesomeIcon icon, Vector4 color)
    {
        using var font = ImRaii.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(color, icon.ToIconString());
    }

    /// A pink icon, a title and a rule that fades from pink to violet to nothing.
    public static void Heading(FontAwesomeIcon icon, string text)
    {
        Icon(icon, Pink);
        ImGui.SameLine();
        ImGui.TextColored(PinkSoft, text);
        var y = ImGui.GetCursorScreenPos().Y - 3f * Scale;
        var x = ImGui.GetCursorScreenPos().X;
        var width = ImGui.GetContentRegionAvail().X;
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilledMultiColor(new Vector2(x, y), new Vector2(x + width * 0.6f, y + 1f * Scale),
            U32(Pink), U32(Alpha(Violet, 0.5f)), U32(Alpha(Violet, 0.5f)), U32(Pink));
        dl.AddRectFilledMultiColor(new Vector2(x + width * 0.6f, y), new Vector2(x + width, y + 1f * Scale),
            U32(Alpha(Violet, 0.5f)), U32(Alpha(Violet, 0f)), U32(Alpha(Violet, 0f)), U32(Alpha(Violet, 0.5f)));
        ImGuiHelpers.ScaledDummy(2f);
    }

    // ── Shapes ───────────────────────────────────────────────────────────────

    /// A left-to-right gradient with rounded ends. ImGui's gradient rectangle can't round, so the ends are solid caps.
    public static void GradientRect(Vector2 min, Vector2 max, Vector4 left, Vector4 right, float rounding)
    {
        var dl = ImGui.GetWindowDrawList();
        var width = max.X - min.X;
        var cap = MathF.Min(rounding, width / 2f);
        if (cap < 1f)
        {
            dl.AddRectFilledMultiColor(min, max, U32(left), U32(right), U32(right), U32(left));
            return;
        }
        dl.AddRectFilled(min, new Vector2(min.X + cap, max.Y), U32(left), cap, ImDrawFlags.RoundCornersLeft);
        dl.AddRectFilled(new Vector2(max.X - cap, min.Y), max, U32(right), cap, ImDrawFlags.RoundCornersRight);
        dl.AddRectFilledMultiColor(new Vector2(min.X + cap, min.Y), new Vector2(max.X - cap, max.Y),
            U32(left), U32(right), U32(right), U32(left));
    }

    /// A rounded progress bar with a gradient fill and an optional centred label.
    public static void GradientBar(float fraction, Vector4 from, Vector4 to, string label = "", float height = 0, float width = -1)
    {
        if (height <= 0) height = ImGui.GetTextLineHeight() + 6f * Scale;
        if (width <= 0) width = ImGui.GetContentRegionAvail().X;
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, height);
        var rounding = height / 2f;
        var dl = ImGui.GetWindowDrawList();

        dl.AddRectFilled(min, max, U32(Hex(0x120A1B)), rounding);
        var filled = Math.Clamp(fraction, 0f, 1f) * width;
        if (filled >= 2f) GradientRect(min, new Vector2(min.X + MathF.Max(filled, rounding * 2f), max.Y), from, to, rounding);
        dl.AddRect(min, max, U32(Alpha(Violet, 0.3f)), rounding);

        if (label.Length > 0)
        {
            var size = ImGui.CalcTextSize(label);
            var at = min + new Vector2((width - size.X) / 2f, (height - size.Y) / 2f);
            dl.AddText(at + new Vector2(1f, 1f) * Scale, U32(new Vector4(0, 0, 0, 0.7f)), label);
            dl.AddText(at, U32(Text), label);
        }
        ImGui.Dummy(new Vector2(width, height));
    }

    public static void XpBar(double xp, long needed, float height = 0, float width = -1)
        => GradientBar(needed == 0 ? 1f : (float)(xp / needed), VioletDeep, Violet,
            needed == 0 ? "Max level" : $"{Math.Floor(xp):0} / {needed} XP", height, width);

    public static void ArousalBar(double arousal, float height = 0, float width = -1)
        => GradientBar((float)(arousal / 100), PinkDeep, Pink, $"Arousal {PlateComposer.ArousalNumber(arousal)}", height, width);

    /// Five hearts that fill one at a time.
    public static void Hearts(double arousal)
    {
        using var font = ImRaii.PushFont(UiBuilder.IconFont);
        using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(3f, 0) * Scale);
        for (var i = 0; i < 5; i++)
        {
            if (i > 0) ImGui.SameLine();
            var fill = (float)Math.Clamp((arousal - i * 20) / 20, 0, 1);
            ImGui.TextColored(Vector4.Lerp(Hex(0x4A2F5F), Pink, fill), FontAwesomeIcon.Heart.ToIconString());
        }
    }

    public static float HeartsWidth()
    {
        using var font = ImRaii.PushFont(UiBuilder.IconFont);
        return ImGui.CalcTextSize(FontAwesomeIcon.Heart.ToIconString()).X * 5 + 12f * Scale;
    }

    public static float PillWidth(string text) => ImGui.CalcTextSize(text).X + 16f * Scale;

    /// Stays on this line when the next item, <paramref name="width"/> wide, fits; otherwise starts the next line,
    /// <paramref name="indent"/> in from where the line started.
    public static void SameLineOrWrap(float width, float indent = 0)
    {
        ImGui.SameLine();
        if (ImGui.GetContentRegionAvail().X >= width) return;
        ImGui.NewLine();
        if (indent > 0) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + indent);
    }

    /// A pill after the last item, on the next line when it wouldn't fit.
    public static void NextPill(string text, Vector4 color)
    {
        SameLineOrWrap(PillWidth(text));
        Pill(text, color);
    }

    /// How wide a checkbox with this label is.
    public static float CheckboxWidth(string label)
        => ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X + ImGui.CalcTextSize(label.Split("##")[0]).X;

    /// A small rounded badge, centred on a frame-height line.
    public static void Pill(string text, Vector4 color)
    {
        var size = ImGui.CalcTextSize(text) + new Vector2(16f, 4f) * Scale;
        var line = MathF.Max(ImGui.GetFrameHeight(), size.Y);
        var pos = ImGui.GetCursorScreenPos() + new Vector2(0, (line - size.Y) / 2f);
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(pos, pos + size, U32(Alpha(color, 0.18f)), size.Y / 2f);
        dl.AddRect(pos, pos + size, U32(Alpha(color, 0.45f)), size.Y / 2f);
        dl.AddText(pos + new Vector2(8f, 2f) * Scale, U32(Vector4.Lerp(color, Text, 0.35f)), text);
        ImGui.Dummy(new Vector2(size.X, line));
    }

    /// A status pill with a dot.
    public static void StatusPill(string text, Vector4 color)
    {
        var dot = ImGui.GetTextLineHeight() * 0.32f;
        var size = ImGui.CalcTextSize(text) + new Vector2(22f * Scale + dot, 4f * Scale);
        var line = MathF.Max(ImGui.GetFrameHeight(), size.Y);
        var pos = ImGui.GetCursorScreenPos() + new Vector2(0, (line - size.Y) / 2f);
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(pos, pos + size, U32(Alpha(color, 0.18f)), size.Y / 2f);
        dl.AddCircleFilled(pos + new Vector2(8f * Scale + dot / 2f, size.Y / 2f), dot, U32(color));
        dl.AddText(pos + new Vector2(14f * Scale + dot, 2f * Scale), U32(Vector4.Lerp(color, Text, 0.5f)), text);
        ImGui.Dummy(new Vector2(size.X, line));
    }

    public static float StatusPillWidth(string text)
        => ImGui.CalcTextSize(text).X + 22f * Scale + ImGui.GetTextLineHeight() * 0.32f;

    /// A tier's colour, brightened so the darkest tiers still read on the dark background.
    public static Vector4 TierColor(Tier tier)
    {
        var color = PlateComposer.TierColor(tier);
        return Vector4.Lerp(new Vector4(color, 1f), PinkSoft, 0.25f);
    }

    public static Vector4 StateColor(SceneState state) => state switch
    {
        SceneState.Earning => Good,
        SceneState.Waiting => Warning,
        SceneState.Paused => Danger,
        _ => Muted,
    };

    public static string StateName(SceneState state) => state switch
    {
        SceneState.Earning => "Earning",
        SceneState.Waiting => "Waiting",
        SceneState.Paused => "Paused",
        _ => "Idle",
    };

    // ── Cards ────────────────────────────────────────────────────────────────

    private static Vector2 CardPadding => new Vector2(12f, 10f) * Scale;

    private static float cardRight;
    private static float cardLeft;

    /// A rounded card the width of the window around <paramref name="body"/>, with an optional coloured stripe on its
    /// left edge. The background goes behind the content afterwards, so it fits whatever was drawn. Cards don't nest.
    public static void CardBox(string id, Action body, Vector4? stripe = null, bool border = false)
    {
        var dl = ImGui.GetWindowDrawList();
        var pad = CardPadding;
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        cardLeft = start.X + pad.X;
        cardRight = start.X + width - pad.X;

        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);
        using (ImRaii.PushId(id))
        {
            ImGui.SetCursorScreenPos(start + pad);
            ImGui.BeginGroup();
            using (ImRaii.ItemWidth(width - pad.X * 2f)) body();
            ImGui.EndGroup();
        }
        var end = new Vector2(start.X + width, ImGui.GetItemRectMax().Y + pad.Y);

        dl.ChannelsSetCurrent(0);
        var rounding = 8f * Scale;
        dl.AddRectFilled(start, end, U32(Card), rounding);
        if (border) dl.AddRect(start, end, U32(Border), rounding);
        if (stripe is { } color)
            dl.AddRectFilled(start, new Vector2(start.X + 4f * Scale, end.Y), U32(color), rounding, ImDrawFlags.RoundCornersLeft);
        dl.ChannelsMerge();

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(end - start);
    }

    /// Inside a card: continue the line so an item this wide ends at the card's right padding.
    public static void AlignRight(float width)
    {
        var wanted = cardRight - width;
        var minimum = ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X;
        ImGui.SameLine(MathF.Max(wanted, minimum) - cardLeft + ImGui.GetScrollX());
    }

    // ── Buttons ──────────────────────────────────────────────────────────────

    /// The one main action of a group.
    public static bool PrimaryButton(FontAwesomeIcon icon, string text)
    {
        using var colors = ImRaii.PushColor(ImGuiCol.Button, PinkDeep)
            .Push(ImGuiCol.ButtonHovered, Pink)
            .Push(ImGuiCol.ButtonActive, PinkSoft);
        return ImGuiComponents.IconButtonWithText(icon, text);
    }

    public static bool DangerButton(FontAwesomeIcon icon, string text)
    {
        using var colors = ImRaii.PushColor(ImGuiCol.Button, Hex(0x8F2440))
            .Push(ImGuiCol.ButtonHovered, Hex(0xC2305A))
            .Push(ImGuiCol.ButtonActive, Danger);
        return ImGuiComponents.IconButtonWithText(icon, text);
    }

    /// A square button with the icon centred on its drawn shape: a button label centres the glyph's advance, which
    /// sits off-centre for most Font Awesome icons.
    public static unsafe bool CenteredIconButton(string id, FontAwesomeIcon icon, float size, Vector4 color)
    {
        var box = new Vector2(size, size);
        var pressed = ImGui.Button($"##{id}", box);
        var min = ImGui.GetItemRectMin();
        var text = icon.ToIconString();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var glyph = ImGui.GetFont().FindGlyph(text[0]);
            var glyphSize = new Vector2(glyph->X1 - glyph->X0, glyph->Y1 - glyph->Y0);
            var pos = min + (box - glyphSize) / 2 - new Vector2(glyph->X0, glyph->Y0);
            var alpha = ImGui.GetStyle().Alpha;
            var tinted = alpha < 1f ? color with { W = color.W * alpha } : color;
            ImGui.GetWindowDrawList().AddText(new Vector2(MathF.Round(pos.X), MathF.Round(pos.Y)), ImGui.GetColorU32(tinted), text);
        }
        return pressed;
    }

    public static float IconButtonWidth(FontAwesomeIcon icon, string text)
    {
        float iconWidth;
        using (ImRaii.PushFont(UiBuilder.IconFont)) iconWidth = ImGui.CalcTextSize(icon.ToIconString()).X;
        return iconWidth + ImGui.CalcTextSize(text).X + ImGui.GetStyle().FramePadding.X * 2 + ImGui.GetStyle().ItemInnerSpacing.X;
    }

    public static float IconOnlyButtonWidth(FontAwesomeIcon icon)
    {
        using var font = ImRaii.PushFont(UiBuilder.IconFont);
        return ImGui.CalcTextSize(icon.ToIconString()).X + ImGui.GetStyle().FramePadding.X * 2;
    }

    /// How wide a nav button is, so a row can wrap before one.
    public static float NavButtonWidth(FontAwesomeIcon icon, string text)
    {
        float iconWidth;
        using (ImRaii.PushFont(UiBuilder.IconFont)) iconWidth = ImGui.CalcTextSize(icon.ToIconString()).X;
        return iconWidth + 6f * Scale + ImGui.CalcTextSize(text).X + 22f * Scale;
    }

    /// One page of the main window's navigation. The icon and the text are each centred in the pill's height, since
    /// the icon font's glyphs aren't the text's height.
    public static bool NavButton(FontAwesomeIcon icon, string text, bool selected)
    {
        Vector2 iconSize;
        using (ImRaii.PushFont(UiBuilder.IconFont)) iconSize = ImGui.CalcTextSize(icon.ToIconString());
        var iconWidth = iconSize.X;
        var textSize = ImGui.CalcTextSize(text);
        var gap = 6f * Scale;
        var size = new Vector2(NavButtonWidth(icon, text), MathF.Max(textSize.Y, iconSize.Y) + 12f * Scale);

        var min = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton($"##nav{text}", size);
        var hovered = ImGui.IsItemHovered();
        var max = min + size;
        var rounding = size.Y / 2f;
        var dl = ImGui.GetWindowDrawList();

        if (selected) GradientRect(min, max, PinkDeep, VioletDeep, rounding);
        else if (hovered) dl.AddRectFilled(min, max, U32(FieldHover), rounding);

        var color = selected ? Text : hovered ? PinkSoft : Muted;
        var left = min.X + 11f * Scale;
        using (ImRaii.PushFont(UiBuilder.IconFont))
            dl.AddText(new Vector2(left, min.Y + (size.Y - iconSize.Y) / 2f), U32(selected ? Text : hovered ? Pink : Muted), icon.ToIconString());
        dl.AddText(new Vector2(left + iconWidth + gap, min.Y + (size.Y - textSize.Y) / 2f), U32(color), text);
        return clicked;
    }

    /// A row of buttons where one is chosen.
    public static bool Choice(string id, string[] labels, ref int selected)
    {
        var changed = false;
        for (var i = 0; i < labels.Length; i++)
        {
            if (i > 0) ImGui.SameLine(0, 4f * Scale);
            var isSelected = selected == i;
            using var colors = ImRaii.PushColor(ImGuiCol.Button, isSelected ? PinkDeep : Field)
                .Push(ImGuiCol.ButtonHovered, isSelected ? Pink : FieldHover);
            if (ImGui.Button($"{labels[i]}##{id}{i}"))
            {
                selected = i;
                changed = true;
            }
        }
        return changed;
    }
}
