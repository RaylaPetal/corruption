using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Corruption.Bridges;
using Corruption.Core;
using Corruption.Game;
using Corruption.Ipc;
using Corruption.Mods;
using Corruption.Plate;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;

namespace Corruption.Scene;

/// The plugin's one running loop: finds the logged-in character, samples the game once a second, and connects the
/// engine to the tag, Cummy and the nameplate text. Framework thread.
public sealed class SceneService : IDisposable, IEngineHost
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

    /// How long a climax keeps showing on the nameplate.
    private const double PlateClimaxSeconds = 15;

    /// How far away tags are read, for nameplates. Scenes need far less.
    private const float TagReadRangeYalms = 50;

    private readonly Settings settings;
    private readonly Balance balance;
    private readonly PenumbraIpc penumbra;
    private readonly ActsService acts;
    private readonly ActDetector detector;
    private readonly SimpleHeelsIpc heels;
    private readonly CummyBridge cummy;
    private readonly PlateLabels plates;
    private readonly BridgeHub bridges;
    private readonly Action<GoalReward> celebrate;
    private readonly string progressPath;
    private readonly ProgressFile progress;

    private ulong contentId;
    private DateTime nextSample = DateTime.MinValue;
    private bool safewordHeld;
    private string? startupError;

    /// Null while no character is logged in.
    public SceneEngine? Engine { get; private set; }

    /// Who's nearby with Corruption, as of the last sample.
    public IReadOnlyList<VisiblePlayer> Nearby { get; private set; } = [];

    public SceneService(Settings settings, Balance balance, PenumbraIpc penumbra, ActsService acts, ActDetector detector,
        SimpleHeelsIpc heels, CummyBridge cummy, PlateLabels plates, BridgeHub bridges, Action<GoalReward> celebrate, string configDirectory)
    {
        this.bridges = bridges;
        this.settings = settings;
        this.balance = balance;
        this.penumbra = penumbra;
        this.acts = acts;
        this.detector = detector;
        this.heels = heels;
        this.cummy = cummy;
        this.plates = plates;
        this.celebrate = celebrate;

        progressPath = Path.Combine(configDirectory, "progress.json");
        progress = JsonStore.Load<ProgressFile>(progressPath, out var error);
        startupError = error ?? acts.LoadError;

        heels.BecameAvailable += OnHeelsAvailable;
        cummy.LayerReceived += OnLayerReceived;
        Plugin.Framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnUpdate;
        cummy.LayerReceived -= OnLayerReceived;
        heels.BecameAvailable -= OnHeelsAvailable;
        Engine?.Flush();
        bridges.EndSession();
    }

    private void OnUpdate(IFramework framework)
    {
        CheckSafeword();

        var now = DateTime.UtcNow;
        if (Engine is { } current) bridges.UpdateToys(now, current);
        if (now < nextSample) return;
        nextSample = now + SampleInterval;

        penumbra.Refresh();
        acts.Update();
        UpdateSession(now);

        if (Engine is { } engine && Plugin.ObjectTable.LocalPlayer is { } me)
        {
            var pose = PoseReader.Read(me);
            var act = detector.Update(now, pose, PoseReader.RaceCode(me));
            var labelled = ReadNearby(me);
            Nearby = labelled.Select(l => l.Player).ToList();
            var self = $"{me.Name.TextValue}@{me.HomeWorld.Value.Name.ExtractText()}";
            engine.Sample(now, new SceneSample(act, pose, me.Position, Nearby, CountWatchers(me, engine.Status.Partner),
                cummy.LayerCount, self));
            UpdatePlates(engine, me, labelled);
            bridges.Update(now, engine, self);
        }
        else if (plates.Current.Count > 0)
        {
            plates.Current = new Dictionary<uint, RenderedPlate>();
            Nearby = [];
        }
    }

    private void OnHeelsAvailable() => Engine?.RepublishTag();

    /// Who gets nameplate text: this character, and nearby players whose tag this client can read.
    private void UpdatePlates(SceneEngine engine, IPlayerCharacter me, List<(uint EntityId, VisiblePlayer Player)> nearby)
    {
        var now = DateTime.UtcNow;
        var texts = new PlateTexts(settings.PlateTemplate, settings.PlateActTemplate, settings.PlateClimaxTemplate,
            settings.PlateCycleSeconds, PlateClimaxSeconds);
        var labels = new Dictionary<uint, RenderedPlate>();
        if (settings.PlateShowOwn)
            labels[me.EntityId] = PlateComposer.ComposeNow(now, texts, engine.OwnPlate, engine.OwnPlateScene);

        if (settings.PlateShowPartners)
        {
            foreach (var (entityId, player) in nearby)
            {
                if (entityId != 0 && engine.PlateOf(player.Player) is { } values)
                    labels[entityId] = PlateComposer.ComposeNow(now, texts, values, engine.PlateSceneOf(player.Player));
            }
        }
        plates.Current = labels;
    }

    private void UpdateSession(DateTime now)
    {
        var id = Plugin.PlayerState.ContentId;
        if (id == contentId) return;

        Engine?.Flush();
        bridges.EndSession();
        contentId = id;
        Engine = null;
        heels.Publish(null);
        heels.PublishExtra(null);
        if (id == 0) return;
        cummy.ReadLayerCount();

        var save = progress.ForCharacter(id, now);
        Engine = new SceneEngine(save, balance, this);

        if (startupError is { } error)
        {
            Notifier.Error(error);
            startupError = null;
        }
        if (save.Paused) Notifier.Info("You're still paused. /corr resume to continue.");
    }

    /// Every nearby player with a Corruption tag. Reading a tag only touches this client's SimpleHeels, so it costs no
    /// traffic. Tags are limited to what's close enough to matter.
    private List<(uint EntityId, VisiblePlayer Player)> ReadNearby(IPlayerCharacter me)
    {
        var nearby = new List<(uint, VisiblePlayer)>();
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj is not IPlayerCharacter pc || pc.Address == me.Address) continue;
            var distance = Vector3.Distance(me.Position, pc.Position);
            if (distance > TagReadRangeYalms || heels.Read(pc.ObjectIndex) is not { } tag) continue;
            var player = new PeerRecord { Name = pc.Name.TextValue, World = pc.HomeWorld.Value.Name.ExtractText() };
            nearby.Add((pc.EntityId, new VisiblePlayer(player, distance, PoseReader.Read(pc), tag, heels.ReadExtra(pc.ObjectIndex))));
        }
#if DEBUG
        if (DebugPartnerActive)
            nearby.Add((0, new VisiblePlayer(new PeerRecord { Name = DebugName, World = DebugWorld }, DebugDistance, DebugTag.Pose, DebugTag, DebugExtra)));
#endif
        return nearby;
    }

    /// Other players watching this scene. The scene partner doesn't count, and only the number is kept.
    private int CountWatchers(IPlayerCharacter me, PeerRecord? partner)
    {
        var partnerId = ulong.MaxValue;
        var others = new List<IPlayerCharacter>();
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj is not IPlayerCharacter pc || pc.Address == me.Address) continue;
            if (partner is not null && partner.Matches(pc.Name.TextValue, pc.HomeWorld.Value.Name.ExtractText())) partnerId = pc.GameObjectId;
            else others.Add(pc);
        }

        var count = 0;
        foreach (var pc in others)
        {
            var targetsScene = pc.TargetObjectId == me.GameObjectId || pc.TargetObjectId == partnerId;
            if (WatcherCheck.IsWatching(pc.Position, pc.Rotation, targetsScene, me.Position, balance)) count++;
        }
        return count;
    }

    private void CheckSafeword()
    {
        var keys = Plugin.KeyState;
        var down = settings.SafewordKey != VirtualKey.NO_KEY
                && keys[settings.SafewordKey]
                && (!settings.SafewordCtrl || keys[VirtualKey.CONTROL])
                && (!settings.SafewordShift || keys[VirtualKey.SHIFT])
                && (!settings.SafewordAlt || keys[VirtualKey.MENU]);
        if (down && !safewordHeld) Pause();
        safewordHeld = down;
    }

    /// The safeword: toys stop on this frame, then scenes stop.
    public void Pause()
    {
        bridges.StopToysNow();
        Engine?.Pause();
    }

    public void SetHidden(bool hidden)
    {
        if (hidden) bridges.StopToysNow();
        Engine?.SetHidden(hidden);
    }

    private void OnLayerReceived(string partId, string sourceName, string sourceWorld)
    {
#if DEBUG
        DebugLog($"Cummy reported a layer: {partId} from {sourceName}@{sourceWorld}");
#endif
        Engine?.HandleLayerReceived(DateTime.UtcNow, sourceName, sourceWorld);
    }

    // ── Engine host ────────────────────────────────────────────────────────────

    public bool PublishTag(string? value) => heels.Publish(value);

    public bool PublishExtraTag(string? value) => heels.PublishExtra(value);

    public string? SendLayer(PeerRecord to, string partId)
    {
#if DEBUG
        if (to.Matches(DebugName, DebugWorld))
        {
            DebugLog($"Would layer the simulated partner: {partId}");
            return "sent";
        }
#endif
        var outcome = cummy.SendLayer(to.Name, to.World, partId);
        Plugin.Log.Debug($"Cummy.SendLayer({to.Key}, {partId}): {outcome ?? "bridge off"}");
        return outcome;
    }

    public void Notify(string text) => Notifier.Info(text);

    public void Celebrate(GoalReward reward) => celebrate(reward);

    /// Public too, for windows that change the save directly, such as tier visual choices.
    public void SaveProgress()
    {
        try { JsonStore.Save(progressPath, progress); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Plugin.Log.Error(ex, "Couldn't save progress.json."); }
    }

#if DEBUG
    // ── Debug partner simulator ────────────────────────────────────────────────

    public const string DebugName = "Debug Partner";
    public const string DebugWorld = "Debug";

    public bool DebugPartnerActive { get; set; }
    public float DebugDistance { get; set; } = 1;
    public PresenceTag DebugTag { get; set; } = new(new PoseKey(50, 0), 30, 40, 0, false);
    public ExtraTag? DebugExtra { get; set; }

    private readonly List<string> debugLog = [];

    public IReadOnlyList<string> DebugMessages => debugLog;

    public void DebugLog(string line)
    {
        Plugin.Log.Debug(line);
        debugLog.Add($"{DateTime.Now:HH:mm:ss} {line}");
        if (debugLog.Count > 12) debugLog.RemoveAt(0);
    }

    /// A real layer on this character through Cummy, so the whole path can be seen working.
    public void DebugLayerMe()
    {
        var part = CummyParts.All[Random.Shared.Next(CummyParts.All.Count)];
        DebugLog(cummy.DebugLayerSelf(part) is { } outcome
            ? $"Asked Cummy to layer {part}: {outcome}. A report should follow, unless that part is blocked or full."
            : "Couldn't reach Cummy's test call. Load a Debug build of Cummy.");
    }
#endif
}
