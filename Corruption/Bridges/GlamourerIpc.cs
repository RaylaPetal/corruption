using System;
using System.Collections.Generic;
using System.Linq;
using Glamourer.Api.Enums;
using Glamourer.Api.Helpers;
using Glamourer.Api.IpcSubscribers;

namespace Corruption.Bridges;

public readonly record struct GlamourerDesign(Guid Id, string Name, string Path);

/// The Glamourer calls tier visuals use, on the local player only, adapted from Oathbound. Never locks anything.
public sealed class GlamourerIpc : IDisposable
{
    private const int LocalPlayer = 0;

    private readonly ApiVersion apiVersion = new(Plugin.PluginInterface);
    private readonly ApplyDesign applyDesign = new(Plugin.PluginInterface);
    private readonly RevertToAutomation revertToAutomation = new(Plugin.PluginInterface);
    private readonly GetDesignListExtended getDesigns = new(Plugin.PluginInterface);
    private readonly EventSubscriber<nint, StateChangeType> stateChanged;
    private readonly EventSubscriber<nint, StateFinalizationType> stateFinalized;

    /// Anything changed the local player's Glamourer state.
    public event Action? LocalPlayerChanged;

    public GlamourerIpc()
    {
        stateChanged = StateChangedWithType.Subscriber(Plugin.PluginInterface, (actor, _) => OnState(actor));
        stateFinalized = StateFinalized.Subscriber(Plugin.PluginInterface, (actor, _) => OnState(actor));
    }

    public void Dispose()
    {
        stateChanged.Dispose();
        stateFinalized.Dispose();
    }

    public bool IsAvailable
    {
        get
        {
            try { apiVersion.Invoke(); return true; }
            catch { return false; }
        }
    }

    private void OnState(nint actor)
    {
        if (actor != 0 && actor == Plugin.ObjectTable.LocalPlayer?.Address) LocalPlayerChanged?.Invoke();
    }

    /// Every saved design, by folder path. Empty when Glamourer is missing.
    public IReadOnlyList<GlamourerDesign> Designs()
    {
        try
        {
            return getDesigns.Invoke()
                .Select(kv => new GlamourerDesign(kv.Key, kv.Value.DisplayName, kv.Value.FullPath))
                .OrderBy(d => d.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch { return []; }
    }

    public bool Apply(Guid design)
    {
        try { return applyDesign.Invoke(design, LocalPlayer, 0, ApplyFlagEx.DesignDefault) == GlamourerApiEc.Success; }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"Glamourer ApplyDesign failed: {ex.Message}");
            return false;
        }
    }

    public bool Revert()
    {
        try { return revertToAutomation.Invoke(LocalPlayer) is GlamourerApiEc.Success or GlamourerApiEc.NothingDone; }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"Glamourer RevertToAutomation failed: {ex.Message}");
            return false;
        }
    }
}
