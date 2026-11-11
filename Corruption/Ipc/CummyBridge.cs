using System;
using System.Collections.Generic;
using System.Linq;
using CummyIpcClient;
using Dalamud.Plugin;

namespace Corruption.Ipc;

/// Corruption's view of Cummy, built on Cummy's own client (CummyIpc.cs). Corruption only ever asks Cummy about, or
/// layers, the local player, and licks partners under Cummy's own rules. Everything here runs on the framework thread:
/// Cummy answers other threads with empty values, which would read as "no layers".
public sealed class CummyBridge : IDisposable
{
    /// Version 2 adds the layer count, 3 layering yourself, and 5 stages, blocks, licks and part events.
    private const int MinimumApiVersion = 1;
    private const int LayerCountVersion = 2;
    private const int LayerSelfVersion = 3;
    public const int FullApiVersion = 5;

    private readonly CummyIpc client;
    private int version;

    public bool IsAvailable { get; private set; }

    /// Layer stages the character wears, from Cummy version 2. 0 when Cummy is older, missing or can't say.
    public int LayerCount { get; private set; }

    public bool HasLayerCount => IsAvailable && version >= LayerCountVersion;

    /// Partners' climaxes can layer this player, from Cummy version 3.
    public bool CanLayerSelf => IsAvailable && version >= LayerSelfVersion;

    /// Stages, blocks, part sizes, licks and part events, from Cummy version 5.
    public bool HasV5 => IsAvailable && version >= FullApiVersion;

    /// Each layered part's stage, by part id. Empty below version 5.
    public IReadOnlyDictionary<string, int> Stages { get; private set; } = new Dictionary<string, int>();

    /// The wearer's blocked part ids. Empty below version 5.
    public IReadOnlyCollection<string> Blocked { get; private set; } = [];

    /// Each stored part's stage count and display name, by part id. Empty below version 5.
    public IReadOnlyDictionary<string, int> PartSizes { get; private set; } = new Dictionary<string, int>();

    public IReadOnlyDictionary<string, string> PartNames { get; private set; } = new Dictionary<string, string>();

    /// What works, for the UI.
    public string Status { get; private set; } = "Not installed";

    /// (name, world, part, licked|nothing): the answer to a lick this client sent.
    public event Action<string, string, string, string>? LickResult;

    /// (part, old stage, new stage, cause, source name, source world): a part of the wearer changed.
    public event Action<string, int, int, string, string, string>? PartChanged;

    public CummyBridge()
    {
        client = new CummyIpc(Plugin.PluginInterface);
        client.Ready += OnReady;
        client.Disposing += OnDisposing;
        client.CharacterChanged += OnCharacterChanged;
        client.LayersChanged += OnLayersChanged;
        client.BlocksChanged += OnBlocksChanged;
        client.LickResult += OnLickResult;
        client.PartChanged += OnPartChanged;
        // Older Cummy versions never announce Ready, so a change in the plugin list still triggers a refresh, run on
        // the framework thread where Cummy answers.
        Plugin.PluginInterface.ActivePluginsChanged += OnPluginsChanged;
        Plugin.Framework.RunOnFrameworkThread(Refresh);
    }

    public void Dispose()
    {
        Plugin.PluginInterface.ActivePluginsChanged -= OnPluginsChanged;
        client.Ready -= OnReady;
        client.Disposing -= OnDisposing;
        client.CharacterChanged -= OnCharacterChanged;
        client.LayersChanged -= OnLayersChanged;
        client.BlocksChanged -= OnBlocksChanged;
        client.LickResult -= OnLickResult;
        client.PartChanged -= OnPartChanged;
        client.Dispose();
    }

    private void OnPluginsChanged(IActivePluginsChangedEventArgs args) => Plugin.Framework.RunOnFrameworkThread(Refresh);

    private void OnReady() => Refresh();

    /// Cummy is going away: nothing from it holds until it's ready again.
    private void OnDisposing()
    {
        IsAvailable = false;
        version = 0;
        Clear();
        Status = "Cummy is reloading";
    }

    private void OnCharacterChanged(bool loggedIn) => ReadState();

    private void OnLayersChanged(int count)
    {
        if (HasLayerCount) LayerCount = Math.Max(0, count);
        if (HasV5) Stages = client.GetStages();
    }

    private void OnBlocksChanged(List<string> blocked)
    {
        if (HasV5) Blocked = blocked.ToHashSet(StringComparer.Ordinal);
    }

    private void OnLickResult(string name, string world, string part, string result) => LickResult?.Invoke(name, world, part, result);

    private void OnPartChanged(string part, int from, int to, string cause, string name, string world)
        => PartChanged?.Invoke(part, from, to, cause, name, world);

    private void Refresh()
    {
        version = client.ApiVersion();
        IsAvailable = version >= MinimumApiVersion;
        Status = version == 0 ? "Not installed, or too old to have the plugin API"
            : !IsAvailable ? $"Off: Cummy's API is version {version}, Corruption needs {MinimumApiVersion} or later"
            : HasV5 ? "Connected"
            : CanLayerSelf ? "Connected. Update Cummy for licks, Full Body and per-part layers"
            : "Connected. Update Cummy so partners' climaxes layer you and your layers count";
        ReadState();
    }

    /// Reads the count, and from version 5 the stages, blocks and part sizes, such as on login or when Cummy is ready.
    public void ReadLayerCount() => ReadState();

    private void ReadState()
    {
        LayerCount = HasLayerCount ? Math.Max(0, client.GetLayerCount()) : 0;
        if (!HasV5)
        {
            Clear();
            return;
        }
        Stages = client.GetStages();
        Blocked = client.GetBlockedParts().ToHashSet(StringComparer.Ordinal);
        var parts = client.GetParts().Where(p => !p.IsTarget).ToList();
        PartSizes = parts.ToDictionary(p => p.Id, p => p.Stages);
        PartNames = parts.ToDictionary(p => p.Id, p => p.Name);
    }

    private void Clear()
    {
        Stages = new Dictionary<string, int>();
        Blocked = [];
        PartSizes = new Dictionary<string, int>();
        PartNames = new Dictionary<string, string>();
    }

    /// Layers the local player as if that player had. Cummy's outcome, or null when Cummy is missing or too old.
    public string? LayerSelf(string partId, string sourceName, string sourceWorld)
    {
        if (!CanLayerSelf) return null;
        var outcome = client.LayerSelf(partId, sourceName, sourceWorld);
        if (outcome is CummyStrings.Unavailable)
        {
            Refresh();
            return null;
        }
        return outcome;
    }

    /// Licks a partner's part under Cummy's own rules. Cummy's outcome, or null below version 5.
    public string? SendLick(string name, string world, string partId)
    {
        if (!HasV5) return null;
        var outcome = client.SendLick(name, world, partId);
        return outcome is CummyStrings.Unavailable ? null : outcome;
    }

    /// The parts a Cummy partner is last known to block, or null when they aren't a Cummy partner.
    public IReadOnlyCollection<string>? PartnerBlocks(string name, string world)
        => !HasV5 ? null
            : client.GetPartners().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
                                                      && string.Equals(p.World, world, StringComparison.OrdinalIgnoreCase)) is { Name: not null } partner
                ? partner.Blocked
                : null;
}
