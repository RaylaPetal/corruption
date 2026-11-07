using System;
using Corruption.Core;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;

namespace Corruption;

[Serializable]
public sealed class Settings : IPluginConfiguration
{
    public const int CurrentVersion = 4;

    public int Version { get; set; } = CurrentVersion;

    public bool PlateShowOwn { get; set; } = true;
    public bool PlateShowPartners { get; set; } = true;
    public string PlateTemplate { get; set; } = PlateComposer.DefaultTemplate;
    public string PlateActTemplate { get; set; } = PlateComposer.DefaultActTemplate;
    public string PlateClimaxTemplate { get; set; } = PlateComposer.DefaultClimaxTemplate;
    public float PlateCycleSeconds { get; set; } = 3f;
    public float PlateScale { get; set; } = 1f;
    public float PlateOffsetY { get; set; }

    /// Where the equipped title shows, for every plate this client draws.
    public TitlePosition TitlePosition { get; set; } = TitlePosition.Above;

    public bool ShowHud { get; set; } = true;

    /// Shows the HUD while a scene with a partner is running, even with ShowHud off.
    public bool HudDuringScenes { get; set; } = true;
    public bool HudLocked { get; set; }
    public float HudX { get; set; } = 300;
    public float HudY { get; set; } = 300;
    public int HudSize { get; set; } = 1;

    public bool ShowInCutscenesAndGpose { get; set; }

    public VirtualKey SafewordKey { get; set; } = VirtualKey.END;
    public bool SafewordCtrl { get; set; } = true;
    public bool SafewordShift { get; set; } = true;
    public bool SafewordAlt { get; set; }

    public bool ShowRewardPopup { get; set; } = true;

    /// The summary popup when a scene of a minute or more ends.
    public bool ShowScenePopup { get; set; } = true;
    public bool PlayRewardSound { get; set; } = true;

    /// The game's chat sound, as in <se.1> to <se.16>.
    public int RewardSound { get; set; } = 6;

    /// A scene partner's counted climax layers you through your own Cummy. No pairing or tell is involved.
    public bool LayerOnPartnerClimax { get; set; } = true;

    // Bridges: all off until the player turns them on.

    public bool TierVisualsEnabled { get; set; }
    public bool RemoveVisualWhilePaused { get; set; }

    public bool ToysEnabled { get; set; }
    public string IntifaceAddress { get; set; } = "ws://127.0.0.1:12345";

    /// 0 to 1: the strongest vibration, reached at 100 arousal and during a climax burst.
    public float ToyMaxIntensity { get; set; } = 1f;

    public bool MoodlesEnabled { get; set; }

    /// True when saved settings changed, so the caller can save them.
    public bool Migrate()
    {
        if (Version >= CurrentVersion) return false;
        PlateTemplate = PlateComposer.UpgradeTemplate(PlateTemplate);
        Version = CurrentVersion;
        return true;
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
