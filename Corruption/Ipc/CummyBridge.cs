using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace Corruption.Ipc;

/// Cummy's IPC (Cummy/Ipc/CummyApi.cs). Cummy keeps its own pairing, block and range rules, so all this does is ask.
/// Load order doesn't matter: availability is re-read whenever the plugin list changes.
public sealed class CummyBridge : IDisposable
{
    /// Version 1 layers on climax; version 2 adds the layer count.
    private const int MinimumApiVersion = 1;
    private const int LayerCountVersion = 2;

    private readonly ICallGateSubscriber<int> apiVersion;
    private readonly ICallGateSubscriber<string, string, string, string> sendLayer;
    private readonly ICallGateSubscriber<string, string, string, object> layerReceived;
    private readonly ICallGateSubscriber<int> getLayerCount;
    private readonly ICallGateSubscriber<int, object> layersChanged;
    private int version;

    public bool IsAvailable { get; private set; }

    /// Layer stages the character wears, from Cummy version 2. 0 when Cummy is older, missing or can't say.
    public int LayerCount { get; private set; }

    public bool HasLayerCount => IsAvailable && version >= LayerCountVersion;

    /// Why the bridge is off, for the UI.
    public string Status { get; private set; } = "Not installed";

    /// (part id, source name, source world).
    public event Action<string, string, string>? LayerReceived;

    public CummyBridge()
    {
        var pi = Plugin.PluginInterface;
        apiVersion = pi.GetIpcSubscriber<int>("Cummy.ApiVersion");
        sendLayer = pi.GetIpcSubscriber<string, string, string, string>("Cummy.SendLayer");
        layerReceived = pi.GetIpcSubscriber<string, string, string, object>("Cummy.OnLayerReceived");
        layerReceived.Subscribe(OnLayerReceived);
        getLayerCount = pi.GetIpcSubscriber<int>("Cummy.GetLayerCount");
        layersChanged = pi.GetIpcSubscriber<int, object>("Cummy.OnLayersChanged");
        layersChanged.Subscribe(OnLayersChanged);
        pi.ActivePluginsChanged += OnPluginsChanged;
        Refresh();
    }

    public void Dispose()
    {
        Plugin.PluginInterface.ActivePluginsChanged -= OnPluginsChanged;
        layerReceived.Unsubscribe(OnLayerReceived);
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

    private void OnLayerReceived(string partId, string sourceName, string sourceWorld)
    {
        if (IsAvailable) LayerReceived?.Invoke(partId, sourceName, sourceWorld);
    }

    private void Refresh()
    {
        try
        {
            version = apiVersion.InvokeFunc();
            IsAvailable = version >= MinimumApiVersion;
            Status = !IsAvailable ? $"Off: Cummy's API is version {version}, Corruption needs {MinimumApiVersion} or later"
                : HasLayerCount ? "Connected"
                : "Connected. Update Cummy for the marked bonus";
        }
        catch
        {
            IsAvailable = false;
            version = 0;
            Status = "Not installed, or too old to have the plugin API";
        }
        ReadLayerCount();
    }

#if DEBUG
    /// Debug builds only: asks a Debug build of Cummy to layer this character, as if a partner had. Null when it can't.
    public string? DebugLayerSelf(string partId)
    {
        try
        {
            return Plugin.PluginInterface.GetIpcSubscriber<string, string>("Cummy.DebugLayerSelf").InvokeFunc(partId);
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"Cummy.DebugLayerSelf failed: {ex.Message}");
            return null;
        }
    }
#endif

    /// Cummy's outcome, or null when the bridge is off.
    public string? SendLayer(string name, string world, string partId)
    {
        if (!IsAvailable) return null;
        try
        {
            return sendLayer.InvokeFunc(name, world, partId);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"Cummy.SendLayer failed: {ex.Message}");
            Refresh();
            return null;
        }
    }
}
