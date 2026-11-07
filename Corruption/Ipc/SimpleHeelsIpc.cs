using System;
using Corruption.Core;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace Corruption.Ipc;

/// SimpleHeels' tag IPC (API 2.x), adapted from CoPose. Tags on object 0, the local player, are reported to sync services
/// such as Lightless, which deliver them to players they sync with. Missing SimpleHeels reads as unavailable.
public sealed class SimpleHeelsIpc : IDisposable
{
    private const int SupportedMajorVersion = 2;
    private const int LocalPlayer = 0;

    private readonly ICallGateSubscriber<(int, int)> apiVersion;
    private readonly ICallGateSubscriber<int, string, string, object?> setTag;
    private readonly ICallGateSubscriber<int, string, string?> getTag;
    private readonly ICallGateSubscriber<int, string, object?> removeTag;

    public bool IsAvailable { get; private set; }

    /// SimpleHeels has just appeared, so the tag has to be written again.
    public event Action? BecameAvailable;

    public SimpleHeelsIpc()
    {
        var pi = Plugin.PluginInterface;
        apiVersion = pi.GetIpcSubscriber<(int, int)>("SimpleHeels.ApiVersion");
        setTag = pi.GetIpcSubscriber<int, string, string, object?>("SimpleHeels.SetTag");
        getTag = pi.GetIpcSubscriber<int, string, string?>("SimpleHeels.GetTag");
        removeTag = pi.GetIpcSubscriber<int, string, object?>("SimpleHeels.RemoveTag");
        pi.ActivePluginsChanged += OnPluginsChanged;
        Refresh();
    }

    public void Dispose()
    {
        Plugin.PluginInterface.ActivePluginsChanged -= OnPluginsChanged;
        Publish(null);
        PublishExtra(null);
        PublishBody(null);
        PublishTitle(null);
        PublishPersona(null);
    }

    private void OnPluginsChanged(IActivePluginsChangedEventArgs args) => Refresh();

    private void Refresh()
    {
        var was = IsAvailable;
        try { IsAvailable = apiVersion.InvokeFunc().Item1 == SupportedMajorVersion; }
        catch { IsAvailable = false; }
        if (IsAvailable && !was) BecameAvailable?.Invoke();
    }

    /// Writes the local player's tag, or removes it when null. False when it couldn't be written.
    public bool Publish(string? value) => Write(PresenceTag.Key, value);

    /// Writes the local player's extra tag (stars and bond request), or removes it when null.
    public bool PublishExtra(string? value) => Write(ExtraTag.Key, value);

    /// Writes the local player's body tag (pregnancy and the self-layering marker), or removes it when null.
    public bool PublishBody(string? value) => Write(BodyTag.Key, value);

    /// Writes the local player's title tag (the equipped title's id), or removes it when null.
    public bool PublishTitle(string? value) => Write(TitleTag.Key, value);

    /// Writes the local player's persona tag (personality and traits), or removes it when null.
    public bool PublishPersona(string? value) => Write(PersonaTag.Key, value);

    /// Another player's persona, or null when their client has none.
    public PersonaTag? ReadPersona(int objectIndex)
    {
        if (!IsAvailable) return null;
        try { return PersonaTag.Parse(getTag.InvokeFunc(objectIndex, PersonaTag.Key)); }
        catch { return null; }
    }

    private bool Write(string key, string? value)
    {
        if (!IsAvailable) return false;
        try
        {
            if (value is null) removeTag.InvokeAction(LocalPlayer, key);
            else setTag.InvokeAction(LocalPlayer, key, value);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"SimpleHeels tag write failed: {ex.Message}");
            return false;
        }
    }

    /// Another player's tag, or null when they have none.
    public PresenceTag? Read(int objectIndex)
    {
        if (!IsAvailable) return null;
        try { return PresenceTag.Parse(getTag.InvokeFunc(objectIndex, PresenceTag.Key)); }
        catch { return null; }
    }

    /// Another player's body tag, or null when their client has none.
    public BodyTag? ReadBody(int objectIndex)
    {
        if (!IsAvailable) return null;
        try { return BodyTag.Parse(getTag.InvokeFunc(objectIndex, BodyTag.Key)); }
        catch { return null; }
    }

    /// Another player's title id, or null when they have none or their client has no titles.
    public string? ReadTitle(int objectIndex)
    {
        if (!IsAvailable) return null;
        try { return TitleTag.Parse(getTag.InvokeFunc(objectIndex, TitleTag.Key))?.Id; }
        catch { return null; }
    }

    /// Another player's extra tag, or null when they have none, such as a client without prestige and bonds.
    public ExtraTag? ReadExtra(int objectIndex)
    {
        if (!IsAvailable) return null;
        try { return ExtraTag.Parse(getTag.InvokeFunc(objectIndex, ExtraTag.Key)); }
        catch { return null; }
    }
}
