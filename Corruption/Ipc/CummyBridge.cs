using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace Corruption.Ipc;

/// Cummy's IPC (Cummy/Ipc/CummyApi.cs). Corruption only ever asks Cummy about, or layers, the local player: when a
/// partner's climax counts, this client layers itself, so no pairing or tell is needed and Cummy's own blocks still
/// apply. Load order doesn't matter: availability is re-read whenever the plugin list changes.
public sealed class CummyBridge : IDisposable
{
    /// Version 2 adds the layer count, and version 3 layering yourself.
    private const int MinimumApiVersion = 1;
    private const int LayerCountVersion = 2;
    private const int LayerSelfVersion = 3;

    private readonly ICallGateSubscriber<int> apiVersion;
    private readonly ICallGateSubscriber<int> getLayerCount;
    private readonly ICallGateSubscriber<int, object> layersChanged;
    private readonly ICallGateSubscriber<string, string, string, string> layerSelf;
    private int version;

    public bool IsAvailable { get; private set; }

    /// Layer stages the character wears, from Cummy version 2. 0 when Cummy is older, missing or can't say.
    public int LayerCount { get; private set; }

    public bool HasLayerCount => IsAvailable && version >= LayerCountVersion;

    /// Partners' climaxes can layer this player, from Cummy version 3.
    public bool CanLayerSelf => IsAvailable && version >= LayerSelfVersion;

    /// What works, for the UI.
    public string Status { get; private set; } = "Not installed";

    public CummyBridge()
    {
        var pi = Plugin.PluginInterface;
        apiVersion = pi.GetIpcSubscriber<int>("Cummy.ApiVersion");
        getLayerCount = pi.GetIpcSubscriber<int>("Cummy.GetLayerCount");
        layersChanged = pi.GetIpcSubscriber<int, object>("Cummy.OnLayersChanged");
        layersChanged.Subscribe(OnLayersChanged);
        layerSelf = pi.GetIpcSubscriber<string, string, string, string>("Cummy.LayerSelf");
        pi.ActivePluginsChanged += OnPluginsChanged;
        Refresh();
    }

    public void Dispose()
    {
        Plugin.PluginInterface.ActivePluginsChanged -= OnPluginsChanged;
        layersChanged.Unsubscribe(OnLayersChanged);
    }

    private void OnLayersChanged(int count)
    {
        if (HasLayerCount) LayerCount = Math.Max(0, count);
    }

    /// Reads the count once, such as when the bridge connects or a character logs in.
    public void ReadLayerCount()
    {
        if (!HasLayerCount)
        {
            LayerCount = 0;
            return;
        }
        try { LayerCount = Math.Max(0, getLayerCount.InvokeFunc()); }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"Cummy.GetLayerCount failed: {ex.Message}");
            LayerCount = 0;
        }
    }

    private void OnPluginsChanged(IActivePluginsChangedEventArgs args) => Refresh();

    private void Refresh()
    {
        try
        {
            version = apiVersion.InvokeFunc();
            IsAvailable = version >= MinimumApiVersion;
            Status = !IsAvailable ? $"Off: Cummy's API is version {version}, Corruption needs {MinimumApiVersion} or later"
                : CanLayerSelf ? "Connected"
                : "Connected. Update Cummy so partners' climaxes layer you and your layers count";
        }
        catch
        {
            IsAvailable = false;
            version = 0;
            Status = "Not installed, or too old to have the plugin API";
        }
        ReadLayerCount();
    }

    /// Layers the local player as if that player had. Cummy's outcome, or null when Cummy is missing or too old.
    public string? LayerSelf(string partId, string sourceName, string sourceWorld)
    {
        if (!CanLayerSelf) return null;
        try
        {
            return layerSelf.InvokeFunc(partId, sourceName, sourceWorld);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"Cummy.LayerSelf failed: {ex.Message}");
            Refresh();
            return null;
        }
    }
}
