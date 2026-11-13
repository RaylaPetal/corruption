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
    public SceneEngine? Engine
    {
        get => currentEngine;
        private set
        {
            if (currentEngine == value) return;
            currentEngine = value;
            EngineChanged?.Invoke(value);
        }
    }

    private SceneEngine? currentEngine;

    /// The engine changed, such as on a character change or logout (null). For the plugin API's event forwarding.
    public event Action<SceneEngine?>? EngineChanged;

    /// The engine was sampled, once a second on the framework thread. For the plugin API's change detection.
    public event Action<DateTime>? Sampled;

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
        cummy.LickResult += OnLickResult;
        cummy.PartChanged += OnPartChanged;
        cummy.CreampieChanged += OnCreampieChanged;
        Plugin.Framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnUpdate;
        heels.BecameAvailable -= OnHeelsAvailable;
        cummy.LickResult -= OnLickResult;
        cummy.PartChanged -= OnPartChanged;
        cummy.CreampieChanged -= OnCreampieChanged;
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
            engine.LayerOnPartnerClimax = settings.LayerOnPartnerClimax;
            engine.LayerSelfWhenAlone = settings.LayerSelfWhenAlone;
            engine.Sample(now, new SceneSample(act, pose, me.Position, Nearby, CountWatchers(me, engine.Status.Partner),
                cummy.LayerCount, self, detector.Excluded,
                cummy.HasV5 ? cummy.Stages : null, cummy.HasV5 ? cummy.Blocked : null, cummy.HasV5 ? cummy.PartSizes : null,
                cummy.HasV6 ? cummy.Creampies : null));
            UpdatePlates(engine, me, labelled);
            bridges.Update(now, engine, self);
            Sampled?.Invoke(now);
        }
        else if (plates.Current.Count > 0)
        {
            plates.Current = new Dictionary<uint, RenderedPlate>();
            Nearby = [];
        }
    }

    private void OnHeelsAvailable() => Engine?.RepublishTag();

    /// Cummy's answer to a lick sent from Lick clean.
    private void OnLickResult(string name, string world, string part, string result) => Engine?.LickAnswered(name, world, result);

    /// A part of the wearer changed. Only a lick or kiss that cleaned it, by someone named, matters to Corruption.
    private void OnPartChanged(string part, int from, int to, string cause, string name, string world)
    {
        if (to >= from || name.Length == 0 || cause is not (CummyIpcClient.CummyStrings.CauseLick or CummyIpcClient.CummyStrings.CauseKiss)) return;
        Engine?.CleanedBy(new PeerRecord { Name = name, World = world }, DateTime.UtcNow);
    }

    /// A creampie on the wearer started or ended. A new one can lead to conception; one licked away by someone named is
    /// being licked out.
    private void OnCreampieChanged(string hole, bool active, int secondsLeft, string cause, string name, string world)
    {
        if (name.Length == 0) return;
        var who = new PeerRecord { Name = name, World = world };
        if (active && cause == CummyIpcClient.CummyStrings.CauseCreampie) Engine?.CreampieFromCummy(who, hole, DateTime.UtcNow);
        else if (!active && cause == CummyIpcClient.CummyStrings.CauseLick) Engine?.LickedOut(who, DateTime.UtcNow);
    }

    /// Who gets nameplate text: this character, and nearby players whose tag this client can read.
    private void UpdatePlates(SceneEngine engine, IPlayerCharacter me, List<(uint EntityId, VisiblePlayer Player)> nearby)
    {
        var now = DateTime.UtcNow;
        var texts = new PlateTexts(settings.PlateTemplate, settings.PlateActTemplate, settings.PlateClimaxTemplate,
            settings.PlateCycleSeconds, PlateClimaxSeconds);
        var labels = new Dictionary<uint, RenderedPlate>();
        var titles = settings.TitlePosition != TitlePosition.Off;

        // With the text turned off, the title can still show on its own.
        RenderedPlate? Label(bool showText, PlateValues values, PlateScene scene)
        {
            var label = showText ? PlateComposer.ComposeNow(now, texts, values, scene) : titles ? PlateComposer.TitleOnly(values) : null;
            return titles || label is null ? label : label with { Title = null };
        }

        if (Label(settings.PlateShowOwn, engine.OwnPlate, engine.OwnPlateScene) is { } own) labels[me.EntityId] = own;
        foreach (var (entityId, player) in nearby)
        {
            if (entityId != 0 && engine.PlateOf(player.Player) is { } values
                && Label(settings.PlateShowPartners, values, engine.PlateSceneOf(player.Player)) is { } label)
                labels[entityId] = label;
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
        heels.PublishBody(null);
        heels.PublishTitle(null);
        heels.PublishPersona(null);
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
            nearby.Add((pc.EntityId, new VisiblePlayer(player, distance, PoseReader.Read(pc), tag, heels.ReadExtra(pc.ObjectIndex),
                heels.ReadBody(pc.ObjectIndex), heels.ReadTitle(pc.ObjectIndex), heels.ReadPersona(pc.ObjectIndex))));
        }
#if DEBUG
        if (DebugPartnerActive)
            nearby.Add((0, new VisiblePlayer(new PeerRecord { Name = DebugName, World = DebugWorld }, DebugDistance, DebugTag.Pose, DebugTag, DebugExtra, DebugBody, DebugTitle, DebugPersona)));
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

    // ── Engine host ────────────────────────────────────────────────────────────

    public bool PublishTag(string? value) => heels.Publish(value);

    public bool PublishExtraTag(string? value) => heels.PublishExtra(value);

    public bool PublishBodyTag(string? value) => heels.PublishBody(value);

    public bool PublishTitleTag(string? value) => heels.PublishTitle(value);

    public bool PublishPersonaTag(string? value) => heels.PublishPersona(value);

    /// A scene long enough to be worth it gets a summary popup. The Last scene card shows every scene.
    public void SceneEnded(SceneSummary summary)
    {
        if (SceneSummaries.WorthAPopup(summary)) celebrate(SceneSummaries.Reward(summary));
    }

    public string? LayerSelf(PeerRecord source, string partId)
    {
        var outcome = cummy.LayerSelf(partId, source.Name, source.World);
        Plugin.Log.Debug($"Cummy.LayerSelf({partId}, from {source.Key}): {outcome ?? "bridge off or too old"}");
        // Worth a line in the log: these mean nothing was layered for a reason the player may want to know.
        if (outcome is CummyIpcClient.CummyStrings.Blacklisted or CummyIpcClient.CummyStrings.WrongThread or CummyIpcClient.CummyStrings.Error)
            Plugin.Log.Information($"Cummy didn't layer you on {partId} for {source.Key}: {outcome}.");
#if DEBUG
        DebugLog($"Layered myself on {partId} for {source.Name}: {outcome ?? "Cummy off or too old"}");
#endif
        return outcome;
    }

    public IReadOnlyList<string>? AimSelf(Anatomy anatomy)
        => Plugin.ObjectTable.LocalPlayer is { } me ? AimFrom(me, me, anatomy) : null;

    public IReadOnlyList<string>? AimOnto(PeerRecord partner, Anatomy own)
        => Plugin.ObjectTable.LocalPlayer is { } me && Find(partner, me) is { } them ? AimFrom(them, me, own) : null;

    public string? SendLick(PeerRecord partner, string partId) => cummy.SendLick(partner.Name, partner.World, partId);

    public string? InsideOf(PeerRecord giver, Anatomy giverAnatomy, Anatomy own)
        => Plugin.ObjectTable.LocalPlayer is { } me && Find(giver, me) is { } them ? InsideFrom(them, me, giverAnatomy, own) : null;

    public string? InsideOnto(PeerRecord partner, Anatomy own, Anatomy theirs)
        => Plugin.ObjectTable.LocalPlayer is { } me && Find(partner, me) is { } them ? InsideFrom(me, them, own, theirs) : null;

    /// The hole on <paramref name="receiver"/> that <paramref name="giver"/>'s penis tip is inside, or null.
    private string? InsideFrom(IPlayerCharacter giver, IPlayerCharacter receiver, Anatomy giverAnatomy, Anatomy receiverAnatomy)
    {
        if (BoneReader.Snapshot(giver) is not { } from || BoneReader.Snapshot(receiver) is not { } to) return null;
        ShotAim.BoneLookup giverBones = names => BoneReader.Average(from.Bones, names);
        ShotAim.BoneLookup receiverBones = names => BoneReader.Average(to.Bones, names);
        var hole = CreampieAim.Inside(giverBones, receiverBones, giverAnatomy, receiverAnatomy);
#if DEBUG
        DebugLog($"Inside test ({giver.Name.TextValue} into {receiver.Name.TextValue}): {hole ?? "outside"}; "
                 + DescribeDistances(CreampieAim.Distances(giverBones, receiverBones)));
#endif
        return hole;
    }

    public string? CreampieSelf(PeerRecord source, string hole)
    {
        var outcome = cummy.CreampieSelf(hole, source.Name, source.World);
        Plugin.Log.Debug($"Cummy.CreampieSelf({hole}, from {source.Key}): {outcome ?? "bridge off or too old"}");
        if (outcome is CummyIpcClient.CummyStrings.Blacklisted or CummyIpcClient.CummyStrings.WrongThread or CummyIpcClient.CummyStrings.Error)
            Plugin.Log.Information($"Cummy didn't creampie you ({hole}) for {source.Key}: {outcome}.");
#if DEBUG
        DebugLog($"Creampied myself ({hole}) for {source.Name}: {outcome ?? "Cummy off or too old"}");
#endif
        return outcome;
    }

    public IReadOnlyCollection<string>? PartnerBlocks(PeerRecord partner) => cummy.PartnerBlocks(partner.Name, partner.World);

    /// A drawn player other than the local one, by name and world.
    private static IPlayerCharacter? Find(PeerRecord player, IPlayerCharacter me)
    {
        foreach (var obj in Plugin.ObjectTable)
            if (obj is IPlayerCharacter pc && pc.Address != me.Address && player.Matches(pc.Name.TextValue, pc.HomeWorld.Value.Name.ExtractText()))
                return pc;
        return null;
    }

    public IReadOnlyList<string>? AimParts(PeerRecord giver, Anatomy anatomy)
    {
        if (Plugin.ObjectTable.LocalPlayer is not { } me) return null;
        IPlayerCharacter? them = null;
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj is IPlayerCharacter pc && pc.Address != me.Address && giver.Matches(pc.Name.TextValue, pc.HomeWorld.Value.Name.ExtractText()))
            {
                them = pc;
                break;
            }
        }
        return them is null ? null : AimFrom(me, them, anatomy);
    }

    /// Parts on <paramref name="me"/> by distance from <paramref name="them"/>'s genitals, or null without bones.
    private IReadOnlyList<string>? AimFrom(IPlayerCharacter me, IPlayerCharacter them, Anatomy anatomy)
    {
        if (BoneReader.Snapshot(them) is not { } theirs || BoneReader.Snapshot(me) is not { } mine) return null;

        var shot = ShotAim.GiverPoint(names => BoneReader.Average(theirs.Bones, names), ShotAim.Facing(theirs.Yaw), anatomy);
        if (shot is not { } from) return null;
        var points = ShotAim.PartPoints(names => BoneReader.Average(mine.Bones, names), ShotAim.Facing(mine.Yaw));
        var order = ShotAim.Order(from, points, CummyParts.All);
#if DEBUG
        DebugLog("Aimed: " + string.Join(", ", order.Take(4).Select(p => points.TryGetValue(p, out var at) ? $"{p} {Vector3.Distance(at, from):0.00}y" : p)));
#endif
        return order;
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
    public BodyTag? DebugBody { get; set; } = BodyTag.Empty;
    public string? DebugTitle { get; set; }
    public PersonaTag? DebugPersona { get; set; }

    private readonly List<string> debugLog = [];

    public IReadOnlyList<string> DebugMessages => debugLog;

    public void DebugLog(string line)
    {
        Plugin.Log.Debug(line);
        debugLog.Add($"{DateTime.Now:HH:mm:ss} {line}");
        if (debugLog.Count > 12) debugLog.RemoveAt(0);
    }

    /// Where a climax from the current target would land on this character, without layering anything.
    public void DebugAimFromTarget()
    {
        if (Plugin.ObjectTable.LocalPlayer is not { } me || Plugin.TargetManager.Target is not IPlayerCharacter target)
        {
            DebugLog("Target a player first.");
            return;
        }
        var anatomy = Engine?.BodyOf(new PeerRecord { Name = target.Name.TextValue, World = target.HomeWorld.Value.Name.ExtractText() })?.Anatomy ?? Anatomy.NotSet;
        DebugLog($"Their anatomy: {anatomy}.");
        if (AimFrom(me, target, anatomy) is null) DebugLog("Couldn't read both skeletons.");
        DebugInside(target, me, "Their tip to my holes");
        DebugInside(me, target, "My tip to their holes");
    }

    /// The tip-to-hole distances between two characters, for tuning the inside radius.
    private void DebugInside(IPlayerCharacter giver, IPlayerCharacter receiver, string label)
    {
        if (BoneReader.Snapshot(giver) is not { } from || BoneReader.Snapshot(receiver) is not { } to)
        {
            DebugLog($"{label}: couldn't read both skeletons.");
            return;
        }
        var distances = CreampieAim.Distances(names => BoneReader.Average(from.Bones, names), names => BoneReader.Average(to.Bones, names));
        DebugLog($"{label}: {DescribeDistances(distances)} (inside within {ShotAim.InsideRadius:0.00}y).");
    }

    private static string DescribeDistances(IReadOnlyDictionary<string, float> distances)
        => distances.Count == 0 ? "no tip or hole bones" : string.Join(", ", distances.Select(d => $"{d.Key} {d.Value:0.000}y"));

    /// A real layer on this character through Cummy, as a partner's climax would give.
    public void DebugLayerMe()
    {
        var part = CummyParts.All[Random.Shared.Next(CummyParts.All.Count)];
        DebugLog(cummy.LayerSelf(part, DebugName, DebugWorld) is { } outcome
            ? $"Cummy layered {part}: {outcome}."
            : "Cummy is missing or older than plugin API version 3.");
    }
#endif
}
