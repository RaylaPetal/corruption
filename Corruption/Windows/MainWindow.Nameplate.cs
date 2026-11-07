using System;
using System.Numerics;
using Corruption.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;

namespace Corruption.Windows;

/// The Nameplate page: preview, showing it and the texts.
public sealed partial class MainWindow
{
    private void DrawNameplate(SceneEngine engine)
    {
        var values = engine.OwnPlate;
        Theme.CardBox("preview", () =>
        {
            Theme.MutedText("Normally, with no arousal");
            DrawPlatePreview(PlateComposer.Compose(settings.PlateTemplate, values));
            Theme.MutedText($"During an act or with any arousal, cycling with the text above every {settings.PlateCycleSeconds:0.#}s");
            DrawPlatePreview(PlateComposer.Compose(settings.PlateActTemplate, values));
            Theme.MutedText("Just after a climax, cycling with arousal");
            DrawPlatePreview(PlateComposer.Compose(settings.PlateClimaxTemplate, values, forceGlow: true));
        }, stripe: Theme.Pink);

        if (!plateRenderer.IsAvailable)
            ImGui.TextColored(Theme.Warning, "The nameplate overlay couldn't start, so no text is shown.");

        Theme.CardBox("show", () =>
        {
            Theme.Heading(FontAwesomeIcon.Eye, "Showing it");
            var own = settings.PlateShowOwn;
            if (ImGui.Checkbox("Above my nameplate", ref own))
            {
                settings.PlateShowOwn = own;
                settings.Save();
            }
            ImGui.SameLine();
            var partners = settings.PlateShowPartners;
            if (ImGui.Checkbox("Above other players' nameplates", ref partners))
            {
                settings.PlateShowPartners = partners;
                settings.Save();
            }
            Theme.MutedWrapped("Only you and players with Corruption who are synced with you can see this. Everyone else sees nothing.");

            ImGui.AlignTextToFramePadding();
            Theme.MutedText("Title");
            ImGui.SameLine();
            foreach (var position in new[] { TitlePosition.Above, TitlePosition.Below, TitlePosition.Off })
            {
                if (ImGui.RadioButton(position == TitlePosition.Above ? "Above the text" : position == TitlePosition.Below ? "Below the name" : "Off",
                        settings.TitlePosition == position))
                {
                    settings.TitlePosition = position;
                    settings.Save();
                }
                ImGui.SameLine();
            }
            ImGui.NewLine();
            Theme.MutedWrapped("Your title comes from the title picker on Profile, not from your tier. It still shows above your name when the text is off.");

            var size = settings.PlateScale;
            ImGui.SetNextItemWidth(180 * Scale);
            if (ImGui.SliderFloat("Size", ref size, 0.5f, 2f, "%.2f"))
            {
                settings.PlateScale = size;
                settings.Save();
            }
            var offset = settings.PlateOffsetY;
            ImGui.SetNextItemWidth(180 * Scale);
            if (ImGui.SliderFloat("Height", ref offset, -40f, 80f, "%.0f"))
            {
                settings.PlateOffsetY = offset;
                settings.Save();
            }
        });

        Theme.CardBox("template", () =>
        {
            Theme.Heading(FontAwesomeIcon.Pen, "Text");
            TextRow("Normally", "##steady", settings.PlateTemplate, t => settings.PlateTemplate = t);
            TextRow("With arousal", "##act", settings.PlateActTemplate, t => settings.PlateActTemplate = t);
            TextRow("On climax", "##climax", settings.PlateClimaxTemplate, t => settings.PlateClimaxTemplate = t);
            Theme.MutedText("Tokens: {tier} {level} {arousal} {arousalBar} {prestige} {title} {personality}");

            var unknown = PlateComposer.UnknownTokens(settings.PlateTemplate + settings.PlateActTemplate + settings.PlateClimaxTemplate);
            if (unknown.Count > 0) ImGui.TextColored(Theme.Warning, $"Unknown: {string.Join(", ", unknown)}");

            var cycle = settings.PlateCycleSeconds;
            ImGui.SetNextItemWidth(180 * Scale);
            if (ImGui.SliderFloat("Seconds per text", ref cycle, 1f, 15f, "%.0f"))
            {
                settings.PlateCycleSeconds = cycle;
                settings.Save();
            }

            if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Undo, "Reset texts"))
            {
                settings.PlateTemplate = PlateComposer.DefaultTemplate;
                settings.PlateActTemplate = PlateComposer.DefaultActTemplate;
                settings.PlateClimaxTemplate = PlateComposer.DefaultClimaxTemplate;
                settings.Save();
            }
            Theme.MutedText("Other players' text uses these too, with their values.");
        });
    }

    private void TextRow(string label, string id, string value, Action<string> set)
    {
        ImGui.AlignTextToFramePadding();
        Theme.MutedText(label);
        ImGui.SameLine(110 * Scale);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        var text = value;
        if (ImGui.InputText(id, ref text, 64))
        {
            set(text);
            settings.Save();
        }
    }

    /// The text roughly as it looks over a character, with its glow, and the title where it's set to go.
    private void DrawPlatePreview(RenderedPlate preview)
    {
        var title = settings.TitlePosition == TitlePosition.Off ? null : preview.Title;
        if (title is not null && settings.TitlePosition == TitlePosition.Above) DrawPreviewTitle(title);
        DrawPreviewText(preview);
        if (title is not null && settings.TitlePosition == TitlePosition.Below)
        {
            DrawPreviewName();
            DrawPreviewTitle(title);
        }
    }

    private static void DrawPreviewTitle(PlateTitle title)
    {
        ImGui.SetWindowFontScale(1.15f);
        var size = ImGui.CalcTextSize(title.Text);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0, (ImGui.GetContentRegionAvail().X - size.X) / 2f));
        ImGui.TextColored(new Vector4(title.Color, 1f), title.Text);
        ImGui.SetWindowFontScale(1f);
    }

    private static void DrawPreviewName()
    {
        const string name = "Your Name";
        var size = ImGui.CalcTextSize(name);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0, (ImGui.GetContentRegionAvail().X - size.X) / 2f));
        Theme.MutedText(name);
    }

    private static void DrawPreviewText(RenderedPlate preview)
    {
        var text = preview.Text;
        ImGui.SetWindowFontScale(1.35f);
        var size = ImGui.CalcTextSize(text);
        var left = ImGui.GetCursorScreenPos().X + MathF.Max(0, (ImGui.GetContentRegionAvail().X - size.X) / 2f);
        var at = new Vector2(left, ImGui.GetCursorScreenPos().Y);
        var dl = ImGui.GetWindowDrawList();
        if (preview.Glow is { } glow)
        {
            var color = ImGui.GetColorU32(new Vector4(glow, 0.45f));
            var reach = 1.5f * Scale;
            foreach (var (x, y) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, 1), (-1, 1), (1, -1) })
                dl.AddText(at + new Vector2(x, y) * reach, color, text);
        }
        dl.AddText(at, ImGui.GetColorU32(new Vector4(preview.Color, 1f)), text);
        ImGui.Dummy(size);
        ImGui.SetWindowFontScale(1f);
    }
}
