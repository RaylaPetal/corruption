using System;
using System.Collections.Generic;
using System.Numerics;
using Corruption.Core;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Overlay.UiOverlay;

namespace Corruption.Plate;

/// Who gets text this moment, by EntityId. SceneService rebuilds it once a second; the overlay reads it every frame.
public sealed class PlateLabels
{
    public IReadOnlyDictionary<uint, RenderedPlate> Current { get; set; } = new Dictionary<uint, RenderedPlate>();

    /// The nameplate values the last label was placed from, for the debug panel.
    public string Diagnostics { get; set; } = "No label placed yet.";
}

/// Text above nameplates, in KamiToolKit's own overlay, positioned each frame from the nameplate's name text. Adapted
/// from Oathbound's StatusIconOverlay. Nothing is attached to the NamePlate addon, so other nameplate edits stay
/// untouched, and disposing the controller removes every label.
public sealed unsafe class PlateOverlay : OverlayNode
{
    /// Never smaller than this, however far away the plate is, so the text stays readable.
    private const float MinFontSize = 14f;
    private const float MaxFontSize = 64f;
    private const float WidthPerFontUnit = 24f;
    private const float Gap = 4f;

    private static readonly Vector4 Edge = new(0.1f, 0.03f, 0.15f, 1f);

    private readonly Settings settings;
    private readonly PlateLabels labels;
    private readonly TextNode?[] nodes = new TextNode?[AddonNamePlate.NumNamePlateObjects];
    private readonly RenderedPlate?[] shownOn = new RenderedPlate?[AddonNamePlate.NumNamePlateObjects];
    private readonly TextNode?[] titleNodes = new TextNode?[AddonNamePlate.NumNamePlateObjects];
    private readonly PlateTitle?[] titleShownOn = new PlateTitle?[AddonNamePlate.NumNamePlateObjects];

    /// The title is a little smaller than the plate text, so the two read as a pair.
    private const float TitleScale = 0.85f;
    private bool failed;

    public PlateOverlay(Settings settings, PlateLabels labels)
    {
        this.settings = settings;
        this.labels = labels;
        Size = new Vector2(1f, 1f);
    }

    public override OverlayLayer OverlayLayer => OverlayLayer.BehindUserInterface;

    protected override void OnUpdate()
    {
        try
        {
            Place();
        }
        catch (Exception ex)
        {
            HideAll();
            if (!failed)
            {
                failed = true;
                Plugin.Log.Warning(ex, "Nameplate text: the nameplate layout couldn't be read, so it's off for this session.");
            }
        }
    }

    private void Place()
    {
        if (failed || labels.Current.Count == 0)
        {
            HideAll();
            return;
        }

        var addon = (AddonNamePlate*)Plugin.GameGui.GetAddonByName("NamePlate").Address;
        if (addon == null || addon->AtkUnitBase.RootNode == null || !addon->AtkUnitBase.RootNode->IsVisible())
        {
            HideAll();
            return;
        }

        var framework = Framework.Instance();
        var ui3D = framework == null || framework->GetUIModule() == null ? null : framework->GetUIModule()->GetUI3DModule();
        if (ui3D == null)
        {
            HideAll();
            return;
        }

        Span<bool> shown = stackalloc bool[AddonNamePlate.NumNamePlateObjects];
        var infos = ui3D->NamePlateObjectInfoPointers;
        var count = Math.Min(ui3D->NamePlateObjectInfoCount, infos.Length);
        for (var i = 0; i < count; i++)
        {
            var info = infos[i].Value;
            if (info == null || info->GameObject == null) continue;
            var index = info->NamePlateIndex;
            if (index < 0 || index >= AddonNamePlate.NumNamePlateObjects) continue;
            if (!labels.Current.TryGetValue(info->GameObject->EntityId, out var label)) continue;

            var nameText = addon->NamePlateObjectArray[index].NameText;
            if (nameText == null || !nameText->AtkResNode.IsVisible() || !addon->NamePlateObjectArray[index].NameContainer->IsVisible()) continue;

            PlaceLabel(index, nameText, label);
            shown[index] = true;
        }

        for (var i = 0; i < shown.Length; i++)
        {
            if (shown[i]) continue;
            if (nodes[i] is { IsVisible: true } node) node.IsVisible = false;
            if (titleNodes[i] is { IsVisible: true } title) title.IsVisible = false;
        }
    }

    /// By default the label matches the name's size and sits just above everything the name's text node draws, which
    /// includes any title sharing that node. All of it is read from the game's own node, so it follows the player's
    /// nameplate settings and distance. The Size and Height settings adjust from there.
    private void PlaceLabel(int index, AtkTextNode* nameText, RenderedPlate label)
    {
        var res = &nameText->AtkResNode;
        var plateScale = AccumulatedScale(res);
        ushort textWidth = 0, textHeight = 0;
        nameText->GetTextDrawSize(&textWidth, &textHeight, null, 0, -1, false);

        // The low bits of the node's alignment are a 3x3 grid, read left to right and top to bottom.
        var alignment = nameText->AlignmentFontType & 0x0F;
        var lines = PlateLayout.CountLines(nameText->NodeText.AsSpan());
        var drawnTop = PlateLayout.DrawnTop(res->ScreenY, res->Height * plateScale, alignment, textHeight * plateScale, lines,
            nameText->FontSize * plateScale);

        var fontSize = Math.Clamp(nameText->FontSize * plateScale * Math.Clamp(settings.PlateScale, 0.5f, 2f), MinFontSize, MaxFontSize);
        var height = fontSize * 1.3f;
        var width = fontSize * WidthPerFontUnit;
        var centerX = res->ScreenX + res->Width * plateScale / 2f;

        // The plate text's top, or where it would be: an Above title stacks on it, or takes its place when there's no text.
        var textY = drawnTop - height - (Gap + settings.PlateOffsetY) * plateScale;
        var node = Node(index);
        var hasText = label.Text.Length > 0;
        if (hasText)
        {
            if (shownOn[index] != label)
            {
                shownOn[index] = label;
                node.String = label.Text;
                node.TextColor = new Vector4(label.Color, 1f);
                node.TextOutlineColor = label.Glow is { } glow ? new Vector4(glow, 1f) : Edge;
            }
            node.FontSize = (byte)fontSize;
            node.Size = new Vector2(width, height);
            node.Position = new Vector2(centerX - width / 2f, textY);
            node.IsVisible = true;
        }
        else if (node.IsVisible)
        {
            node.IsVisible = false;
            shownOn[index] = null;
        }

        var titleNode = TitleNode(index);
        if (label.Title is not { } title || settings.TitlePosition == TitlePosition.Off)
        {
            titleNode.IsVisible = false;
            titleShownOn[index] = null;
        }
        else
        {
            if (titleShownOn[index] != title)
            {
                titleShownOn[index] = title;
                titleNode.String = title.Text;
                titleNode.TextColor = new Vector4(title.Color, 1f);
                titleNode.TextOutlineColor = Edge;
            }
            var titleSize = Math.Max(MinFontSize, fontSize * TitleScale);
            var titleHeight = titleSize * 1.3f;
            float titleY;
            if (settings.TitlePosition == TitlePosition.Below)
            {
                var drawnBottom = PlateLayout.DrawnBottom(res->ScreenY, res->Height * plateScale, alignment, textHeight * plateScale, lines,
                    nameText->FontSize * plateScale);
                titleY = drawnBottom + (Gap + settings.PlateOffsetY) * plateScale;
                titleNode.AlignmentType = AlignmentType.Top;
            }
            else
            {
                titleY = hasText ? textY - titleHeight : drawnTop - titleHeight - (Gap + settings.PlateOffsetY) * plateScale;
                titleNode.AlignmentType = AlignmentType.Bottom;
            }
            titleNode.FontSize = (byte)titleSize;
            titleNode.Size = new Vector2(width, titleHeight);
            titleNode.Position = new Vector2(centerX - width / 2f, titleY);
            titleNode.IsVisible = true;
        }

        labels.Diagnostics = $"plate scale {plateScale:0.00}, name font {nameText->FontSize}, alignment {alignment}, "
                           + $"box {res->Width}x{res->Height} at {res->ScreenX:0},{res->ScreenY:0}, drawn {textWidth}x{textHeight}, {lines} lines, top {drawnTop:0}, label font {fontSize:0}";
    }

    private static float AccumulatedScale(AtkResNode* node)
    {
        var scale = 1f;
        for (var n = node; n != null; n = n->ParentNode)
            scale *= n->ScaleX > 0 ? n->ScaleX : 1f;
        return scale;
    }

    private TextNode Node(int index) => NodeIn(nodes, index);

    private TextNode TitleNode(int index) => NodeIn(titleNodes, index);

    private TextNode NodeIn(TextNode?[] array, int index)
    {
        ref var node = ref array[index];
        if (node is null)
        {
            node = new TextNode
            {
                AlignmentType = AlignmentType.Bottom,
                TextFlags = TextFlags.Edge,
                IsVisible = false,
            };
            node.AttachNode(this);
        }
        return node;
    }

    private void HideAll()
    {
        for (var i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] is { IsVisible: true } node) node.IsVisible = false;
            if (titleNodes[i] is { IsVisible: true } title) title.IsVisible = false;
            shownOn[i] = null;
            titleShownOn[i] = null;
        }
    }
}

/// Owns the overlay. If it can't start, everything else keeps working without nameplate text.
public sealed class PlateRenderer : IDisposable
{
    private readonly OverlayController? controller;

    private readonly PlateLabels labels;

    public bool IsAvailable => controller is not null;

    public string Diagnostics => labels.Diagnostics;

    public PlateRenderer(Settings settings, PlateLabels labels)
    {
        this.labels = labels;
        try
        {
            KamiToolKitLibrary.Initialize(Plugin.PluginInterface, "Corruption");
            controller = new OverlayController();
            controller.CreateNode(() => new PlateOverlay(settings, labels));
        }
        catch (Exception ex)
        {
            controller = null;
            Plugin.Log.Warning(ex, "Nameplate text: the overlay couldn't start, so it's off for this session.");
        }
    }

    public void Dispose()
    {
        controller?.Dispose();
        KamiToolKitLibrary.Dispose();
    }
}
