using System.Collections.Generic;
using System.Numerics;
using Corruption.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace Corruption.Windows;

/// What's new: opens by itself once after an update with the releases not seen yet, and on request with all of them.
/// Closing it marks everything seen.
public sealed class ChangelogWindow : Window
{
    private readonly Settings settings;
    private IReadOnlyList<ChangelogEntry> showing;

    public ChangelogWindow(Settings settings) : base("What's new in Corruption###CorruptionChangelog")
    {
        this.settings = settings;
        Size = new Vector2(480, 360);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(360, 220), MaximumSize = new Vector2(1000, 1200) };
        showing = Changelog.Unseen(settings.LastSeenChangelog);
        IsOpen = showing.Count > 0;
    }

    /// Every release's notes, for /corr changelog and the main window's button.
    public void ShowAll()
    {
        showing = Changelog.Entries;
        IsOpen = true;
    }

    public override void OnClose()
    {
        if (settings.LastSeenChangelog == Changelog.Latest) return;
        settings.LastSeenChangelog = Changelog.Latest;
        settings.Save();
    }

    public override void Draw()
    {
        using var content = Theme.Content();
        var footer = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;
        ImGui.BeginChild("##notes", new Vector2(0, -footer));
        foreach (var entry in showing)
        {
            Theme.Heading(FontAwesomeIcon.Star, $"Version {entry.Version}");
            foreach (var note in entry.Notes)
            {
                ImGui.Bullet();
                ImGui.SameLine();
                ImGui.TextWrapped(note);
            }
            ImGuiHelpers.ScaledDummy(4f);
        }
        ImGui.EndChild();

        if (Theme.PrimaryButton(FontAwesomeIcon.Check, "Got it")) IsOpen = false;
    }
}
