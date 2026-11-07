using System;
using System.Collections.Generic;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;

namespace Corruption.Mods;

/// The Penumbra calls Corruption uses. The only writes are temporary settings for tier visuals, under the source
/// "Corruption", which never touch the player's saved settings. Every call is guarded, so a missing Penumbra reads as
/// unavailable and nothing throws. Framework thread.
public sealed class PenumbraIpc : IDisposable
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

    private readonly GetModList getModList = new(Plugin.PluginInterface);
    private readonly GetModPath getModPath = new(Plugin.PluginInterface);
    private readonly GetModDirectory getModDirectory = new(Plugin.PluginInterface);
    private readonly ResolvePlayerPaths resolvePlayerPaths = new(Plugin.PluginInterface);
    private readonly GetAvailableModSettings getAvailableModSettings = new(Plugin.PluginInterface);
    private readonly GetCollectionForObject getCollectionForObject = new(Plugin.PluginInterface);
    private readonly GetCurrentModSettings getCurrentModSettings = new(Plugin.PluginInterface);
    private readonly SetTemporaryModSettings setTemporaryModSettings = new(Plugin.PluginInterface);
    private readonly RemoveTemporaryModSettings removeTemporaryModSettings = new(Plugin.PluginInterface);
    private readonly RedrawObject redrawObject = new(Plugin.PluginInterface);

    public const string TemporarySource = "Corruption";

    private readonly IDisposable initialized;
    private readonly IDisposable disposed;
    private readonly IDisposable settingChanged;
    private readonly IDisposable modAdded;
    private readonly IDisposable modDeleted;
    private readonly IDisposable modMoved;

    private DateTime nextCheck = DateTime.MinValue;

    public bool IsAvailable { get; private set; }

    /// Bumped by any mod setting change, so detection re-checks soon. These events can come off the framework thread.
    public volatile int SettingsVersion;

    /// Raised when the installed mods change, or when Penumbra becomes available.
    public volatile bool ModsDirty = true;

    public PenumbraIpc()
    {
        initialized = Initialized.Subscriber(Plugin.PluginInterface, () => { nextCheck = DateTime.MinValue; ModsDirty = true; });
        disposed = Disposed.Subscriber(Plugin.PluginInterface, () => IsAvailable = false);
        settingChanged = ModSettingChanged.Subscriber(Plugin.PluginInterface,
            (ModSettingChange _, Guid _, string _, bool _) => SettingsVersion++);
        modAdded = ModAdded.Subscriber(Plugin.PluginInterface, _ => ModsDirty = true);
        modDeleted = ModDeleted.Subscriber(Plugin.PluginInterface, _ => ModsDirty = true);
        modMoved = ModMoved.Subscriber(Plugin.PluginInterface, (_, _) => ModsDirty = true);
        Refresh();
    }

    public void Dispose()
    {
        initialized.Dispose();
        disposed.Dispose();
        settingChanged.Dispose();
        modAdded.Dispose();
        modDeleted.Dispose();
        modMoved.Dispose();
    }

    /// Run regularly: picks Penumbra up when it appears, without spending exceptions on every frame.
    public void Refresh()
    {
        var now = DateTime.UtcNow;
        if (IsAvailable || now < nextCheck) return;
        nextCheck = now + RetryInterval;
        if (TryGetModDirectory() is { Length: > 0 })
        {
            IsAvailable = true;
            ModsDirty = true;
        }
    }

    public string? TryGetModDirectory()
    {
        try { return getModDirectory.Invoke(); }
        catch { return null; }
    }

    public Dictionary<string, string>? TryGetModList()
    {
        try { return getModList.Invoke(); }
        catch (Exception ex) { Fail("GetModList", ex); return null; }
    }

    /// The mod's path in Penumbra's folder tree, such as "Animations/Couples/Mod".
    public string? TryGetModPath(string modDirectory, string modName)
    {
        try
        {
            var (ec, fullPath, _, _) = getModPath.Invoke(modDirectory, modName);
            return ec == PenumbraApiEc.Success ? fullPath : null;
        }
        catch (Exception ex) { Fail("GetModPath", ex); return null; }
    }

    /// What Penumbra serves the local character for these game paths, in the same order.
    public string[]? TryResolve(string[] gamePaths)
    {
        try { return resolvePlayerPaths.Invoke(gamePaths, []).Item1; }
        catch (Exception ex) { Fail("ResolvePlayerPaths", ex); return null; }
    }

    /// Each option group of a mod, with its option names. Null when it can't be read.
    public IReadOnlyDictionary<string, string[]>? TryGetGroups(string modDirectory)
    {
        try
        {
            var groups = getAvailableModSettings.Invoke(modDirectory);
            if (groups is null) return null;
            var result = new Dictionary<string, string[]>();
            foreach (var (group, (options, _)) in groups) result[group] = options;
            return result;
        }
        catch (Exception ex) { Fail("GetAvailableModSettings", ex); return null; }
    }

    /// The collection the local player uses. Null when they aren't drawn, or Penumbra can't say.
    public Guid? TryGetPlayerCollection()
    {
        try
        {
            var (valid, _, effective) = getCollectionForObject.Invoke(0);
            return valid ? effective.Id : null;
        }
        catch (Exception ex) { Fail("GetCollectionForObject", ex); return null; }
    }

    /// Turns the mod on in the collection as a temporary setting, with these options and its other current selections
    /// and priority kept. False when Penumbra refused.
    public bool TrySetTemporary(Guid collection, string modDirectory, IReadOnlyDictionary<string, string> chosen)
    {
        try
        {
            var (_, current) = getCurrentModSettings.Invoke(collection, modDirectory);
            var priority = 0;
            var selections = new Dictionary<string, IReadOnlyList<string>>();
            if (current is { } c)
            {
                priority = c.Item2;
                foreach (var (group, options) in c.Item3) selections[group] = options;
            }
            foreach (var (group, option) in chosen) selections[group] = [option];
            var ec = setTemporaryModSettings.Invoke(collection, modDirectory, inherit: false, enabled: true, priority: priority,
                settings: selections, source: TemporarySource);
            if (ec != PenumbraApiEc.Success) Plugin.Log.Warning($"Penumbra SetTemporaryModSettings for {modDirectory}: {ec}");
            return ec == PenumbraApiEc.Success;
        }
        catch (Exception ex) { Fail("SetTemporaryModSettings", ex); return false; }
    }

    /// Removes only Corruption's own temporary setting on the mod.
    public bool TryRemoveTemporary(Guid collection, string modDirectory)
    {
        try { return removeTemporaryModSettings.Invoke(collection, modDirectory) is PenumbraApiEc.Success or PenumbraApiEc.NothingChanged; }
        catch (Exception ex) { Fail("RemoveTemporaryModSettings", ex); return false; }
    }

    /// A settings change isn't seen on a character that's already drawn until it's redrawn.
    public void TryRedrawPlayer()
    {
        try { redrawObject.Invoke(0); }
        catch { /* best effort */ }
    }

    private void Fail(string call, Exception ex)
    {
        Plugin.Log.Warning($"Penumbra {call} failed: {ex.Message}");
        IsAvailable = false;
    }
}
