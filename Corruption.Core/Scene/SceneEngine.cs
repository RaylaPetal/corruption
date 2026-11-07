using System.Numerics;

namespace Corruption.Core;

/// What the engine asks of the outside world.
public interface IEngineHost
{
    /// Writes this character's tag, or removes it when null. False when it couldn't be written, so it's tried again.
    bool PublishTag(string? value);

    /// Writes the extra tag (stars and bond request), or removes it when null. False when it couldn't be written.
    bool PublishExtraTag(string? value);

    /// Asks Cummy to layer the partner. Cummy's outcome string, or null when Cummy isn't available.
    string? SendLayer(PeerRecord to, string partId);

    void Notify(string text);

    /// A goal or achievement was completed, with the XP it actually paid.
    void Celebrate(GoalReward reward);

    /// Asks for a save now, rather than waiting for the periodic flush.
    void SaveProgress();
}

/// A nearby player whose Corruption tag this client can read: distance, and the looping pose this client can see on
/// them (null when they aren't in a loop).
/// Extra is null when they publish no extra tag, such as a client without prestige and bonds.
public readonly record struct VisiblePlayer(PeerRecord Player, double Distance, PoseKey? Pose, PresenceTag Tag, ExtraTag? Extra = null);

/// One second's worth of game state.
/// <param name="LayersWorn">Cummy layer stages worn, 0 when Cummy can't say.</param>
/// <param name="SelfKey">This character's `Name@World`, for reading bond requests.</param>
public readonly record struct SceneSample(ActEntry? OwnAct, PoseKey? OwnPose, Vector3 OwnPosition, IReadOnlyList<VisiblePlayer> Visible, int Watchers = 0,
    int LayersWorn = 0, string? SelfKey = null);

public enum SceneState { Idle, Waiting, Earning, Paused }

public sealed record SceneStatus(
    SceneState State,
    SceneFailure Failure,
    PeerRecord? Partner,
    ActEntry? Act,
    DateTime? SceneStartedUtc,
    double Fatigue,
    double LastTickXp,
    int Watchers,
    TickFactors Factors)
{
    public static readonly SceneStatus Empty = new(SceneState.Idle, SceneFailure.NoKnownAct, null, null, null, 1, 0, 0, TickFactors.None);
}

/// One character's scene loop: ties the scene check to XP, arousal, climaxes and the tag. Everything here is decided from
/// this client's own view; all it shares is its tag.
public sealed class SceneEngine(CharacterSave save, Balance balance, IEngineHost host, Random? random = null)
{
    private readonly Random random = random ?? Random.Shared;

    private readonly GoalBook goals = new(save);
    private readonly TagPublisher publisher = new();
    private readonly ExtraTagPublisher extraPublisher = new();
    private readonly Dictionary<string, PresenceTag> tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ExtraTag> extras = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> climaxCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (int Count, DateTime At)> earlyLayers = new(StringComparer.OrdinalIgnoreCase);
    private readonly SceneTicker ticker = new();
    private readonly AfkTracker afk = new();
    private readonly Dictionary<string, DateTime> partnerClimaxes = new(StringComparer.OrdinalIgnoreCase);
    private DateTime? lastOwnClimax;
    private int ownClimaxCount;
    private bool tagPublished;
    private bool extraPublished;
    private int layersWorn;

    private sealed record PendingLayer(DateTime Due, PeerRecord To);
    private readonly List<PendingLayer> pendingLayers = [];

    private readonly Dictionary<string, DateTime> lastClimaxCredit = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> creditedClimax = new(StringComparer.OrdinalIgnoreCase);

    private string? lastTickPartner;
    private DateTime lastTickUtc = DateTime.MinValue;
    private DateTime lastPassUtc = DateTime.MinValue;
    private DateTime? sceneStarted;
    private string? fatigueAct;
    private DateTime fatigueStart;
    private double lastTickXp;
    private TickFactors lastFactors = TickFactors.None;
    private int watchers;
    private DateTime lastSave = DateTime.MinValue;
    private bool dirty;
    private DateTime nextFadeCheck = DateTime.MinValue;

    private bool verified;
    private ActEntry? verifiedAct;
    private PeerRecord? nearestInRange;

    public SceneStatus Status { get; private set; } = SceneStatus.Empty;

    public CharacterSave Save => save;

    public Balance Balance => balance;

    public bool IsPaused => save.Paused;

    public BondBook Bonds { get; } = new(save);

    /// A verified scene is running this second.
    public bool IsVerified => verified;

    public DateTime? LastOwnClimaxUtc => lastOwnClimax;

    /// The layer count from the last sample.
    public int LayersWorn => layersWorn;

    /// What a nearby player's tag says about them, for their nameplate. Null when no tag can be read.
    public PlateValues? PlateOf(PeerRecord player)
        => tags.TryGetValue(player.Key, out var tag) ? new PlateValues(tag.Level, tag.Arousal, StarsOf(player)) : null;

    /// A nearby player's stars, from their extra tag; 0 when they have none.
    public int StarsOf(PeerRecord player) => extras.TryGetValue(player.Key, out var extra) ? extra.Stars : 0;

    public PlateValues OwnPlate => new(save.Level, save.Arousal, save.Prestige);

    public PlateScene OwnPlateScene => new(Status.Act is not null && !save.Paused, lastOwnClimax);

    public PlateScene PlateSceneOf(PeerRecord player)
        => new(tags.TryGetValue(player.Key, out var tag) && tag.Pose is not null,
            partnerClimaxes.TryGetValue(player.Key, out var at) ? at : null);

    /// The tag has to be written again, such as when SimpleHeels has just appeared.
    public void RepublishTag()
    {
        publisher.Forget();
        extraPublisher.Forget();
    }

    /// Run about once per second.
    public void Sample(DateTime now, in SceneSample sample)
    {
        Arousal.ApplyDecay(save, now, balance);
        ApplyFading(now);
        if (save.Setup is not null) goals.Roll(now);
        FlushPendingLayers(now);
        watchers = sample.Watchers;
        layersWorn = Math.Max(0, sample.LayersWorn);

        ReadTags(now, sample.Visible);
        if (Bonds.Observe(sample.SelfKey, sample.Visible.Select(v => (v.Player, v.Extra))) is { } bondNews)
        {
            host.Notify(bondNews);
            host.SaveProgress();
        }

        // The partner is the nearest player actually in an act, if one's in range; otherwise whoever is nearest, so the
        // status can say what's missing.
        var inRange = sample.Visible.Where(v => v.Distance <= balance.SceneRangeYalms && v.Tag.Pose is not null).ToList();
        VisiblePlayer? nearest = inRange.Count > 0 ? inRange.MinBy(v => v.Distance)
            : sample.Visible.Count > 0 ? sample.Visible.MinBy(v => v.Distance) : null;
        nearestInRange = nearest is { } n && n.Distance <= balance.SceneRangeYalms ? n.Player : null;

        afk.Update(now, sample.OwnPose, sample.OwnPosition);
        var failure = SceneCheck.Evaluate(new SceneInput(
            save.Paused,
            sample.OwnAct is not null,
            nearest is not null,
            nearest?.Distance ?? 0,
            nearest?.Tag.Pose,
            nearest?.Pose,
            afk.IsAfk(now, balance.AfkMinutes),
            save.Setup is null,
            save.Hidden), balance.SceneRangeYalms);

        verified = failure == SceneFailure.None;
        verifiedAct = verified ? sample.OwnAct : null;

        if (verified)
        {
            // A long break also starts fatigue afresh, so yesterday's scene doesn't count against today's.
            if (sample.OwnAct!.Key != fatigueAct || (now - lastPassUtc).TotalMinutes >= balance.FatigueForgetMinutes)
            {
                fatigueAct = sample.OwnAct.Key;
                fatigueStart = now;
            }
            lastPassUtc = now;
            if (sceneStarted is null)
            {
                sceneStarted = now;
                goals.RecordPartnerScene(nearest!.Value.Player.Key);
            }
        }
        else if (sceneStarted is not null && (now - lastPassUtc).TotalSeconds >= balance.SceneEndSeconds)
        {
            sceneStarted = null;
            host.SaveProgress();
            dirty = false;
            lastSave = now;
        }

        var partner = nearest?.Player;
        var fatigue = verified ? Progression.Fatigue(now - fatigueStart, balance) : 1.0;
        if (ticker.Sample(now, verified, partner?.Key, sample.OwnAct?.Key, balance.TickSeconds))
            AwardTick(now, sample.OwnAct!, partner!, fatigue);

        if (dirty && (now - lastSave).TotalSeconds >= balance.SaveIntervalSeconds)
        {
            host.SaveProgress();
            dirty = false;
            lastSave = now;
        }

        Status = new SceneStatus(StateOf(failure), failure, partner, sample.OwnAct, sceneStarted, fatigue, lastTickXp, watchers, lastFactors);
        PublishTag(now, sample.OwnAct?.Pose);
        PayRewards();
    }

    private void PayRewards()
    {
        if (save.Setup is null) return;
        foreach (var reward in goals.TakeRewards())
        {
            host.Notify(reward.Message);
            GiveXp(reward.Xp);
            host.Celebrate(reward with { Xp = reward.Xp * save.Setup.XpMultiplier });
            host.SaveProgress();
        }
    }

    /// No setup or being hidden means no tag, so nobody counts this character as having Corruption.
    private void PublishTag(DateTime now, PoseKey? ownAct)
    {
        if (save.Setup is null || save.Hidden)
        {
            if (tagPublished && host.PublishTag(null)) tagPublished = false;
            if (extraPublished && host.PublishExtraTag(null)) extraPublished = false;
            publisher.Forget();
            extraPublisher.Forget();
            return;
        }
        if (publisher.Update(now, ownAct, save.Level, save.Arousal, ownClimaxCount, save.Paused) is { } tag)
        {
            if (host.PublishTag(tag.Format())) tagPublished = true;
            else publisher.Forget();
        }
        if (extraPublisher.Update(save.Prestige, Bonds.OwnTagHash) is { } extra)
        {
            if (host.PublishExtraTag(extra.Format())) extraPublished = true;
            else extraPublisher.Forget();
        }
    }

    /// Takes in what nearby players' tags say. A climax shows as their count going up. The first time a player is seen
    /// doesn't count, since the count could be from earlier.
    private void ReadTags(DateTime now, IReadOnlyList<VisiblePlayer> visible)
    {
        tags.Clear();
        extras.Clear();
        foreach (var player in visible)
        {
            tags[player.Player.Key] = player.Tag;
            if (player.Extra is { } extra) extras[player.Player.Key] = extra;
            var count = player.Tag.ClimaxCount;
            if (climaxCounts.TryGetValue(player.Player.Key, out var last) && count != last && count > 0)
            {
                partnerClimaxes[player.Player.Key] = now;
                CreditPartnerClimax(now, player.Player);
            }
            climaxCounts[player.Player.Key] = count;
        }
    }

    private static SceneState StateOf(SceneFailure failure) => failure switch
    {
        SceneFailure.None => SceneState.Earning,
        SceneFailure.Paused => SceneState.Paused,
        SceneFailure.NoKnownAct => SceneState.Idle,
        _ => SceneState.Waiting,
    };

    private void ApplyFading(DateTime now)
    {
        if (now < nextFadeCheck) return;
        nextFadeCheck = now.AddMinutes(1);
        if (Progression.ApplyFading(save, now, balance))
        {
            host.Notify($"Your corruption is fading. You're level {save.Level}.");
            host.SaveProgress();
        }
    }

    /// The first-time setup is done, so scenes can earn. Time before now doesn't count as being away.
    public void CompleteSetup(ProfileSetup setup, DateTime now)
    {
        save.Setup = setup;
        save.LastVerifiedUtc = now;
        save.FadedThroughUtc = now;
        host.SaveProgress();
    }

    private void AwardTick(DateTime now, ActEntry act, PeerRecord partner, double fatigue)
    {
        lastTickPartner = partner.Key;
        lastTickUtc = now;
        save.LastVerifiedUtc = now;

        Progression.UpdateStreak(save, now);
        lastFactors = Progression.Factors(save.Level, PlateOf(partner)?.Level, watchers, save.StreakDays, fatigue, balance,
            Bonds.PaysWith(partner.Key), layersWorn, save.Prestige);
        var xp = balance.TickXp * lastFactors.Total;
        if (save.SeenPartners.Add(partner.Key)) xp += balance.FirstPartnerXp;
        if (save.SeenActs.Add(act.Key)) xp += balance.FirstActXp;
        lastTickXp = xp;
        GiveXp(xp);
        RecordTick(now, act, partner);

        var refractory = Arousal.InRefractory(save, now);
        var tier = Progression.TierOf(save.Level);
        save.Arousal = Math.Min(100, save.Arousal + Arousal.Gain(tier, watchers, refractory, balance) * (save.Setup?.ArousalGain ?? 1));
        save.ArousalAnchorUtc = now;
        dirty = true;

        if (save.Arousal >= 100 && !refractory && save.Setup?.AutoClimax != false) FireClimax(now, partner);
    }

    private void RecordTick(DateTime now, ActEntry act, PeerRecord partner)
    {
        var seconds = (long)balance.TickSeconds;
        goals.Record(GoalKind.SceneSeconds, seconds);
        if (watchers > 0) goals.Record(GoalKind.WatchedSeconds, seconds);
        if (watchers >= 3) goals.Record(GoalKind.CrowdSeconds, seconds);
        if (save.Arousal >= 100) goals.Record(GoalKind.EdgedSeconds, seconds);
        goals.RecordOnce(GoalKind.DistinctAnimations, act.Key);
        goals.RecordOnce(GoalKind.DistinctPartners, partner.Key);
        goals.RecordOnce(GoalKind.ActiveDays, now.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
    }

    private void GiveXp(double amount)
    {
        var result = Progression.AddXp(save, amount * (save.Setup?.XpMultiplier ?? 1), balance);
        dirty = true;
        if (result.LevelsGained > 0)
        {
            host.Notify(result.NewTier is { } tier
                ? $"You reached level {save.Level} and the {tier} tier."
                : $"You reached level {save.Level}.");
            if (save.Level >= balance.LevelCap)
                host.Notify("You've reached the top. You can prestige from the Profile page or with /corr prestige.");
            host.SaveProgress();
        }
    }

    private void FireClimax(DateTime now, PeerRecord partner)
    {
        lastOwnClimax = now;
        ownClimaxCount++;
        goals.Record(GoalKind.Climaxes);
        GiveXp(balance.ClimaxXp);
        save.Arousal = balance.ArousalAfterClimax;
        save.ArousalAnchorUtc = now;
        save.RefractoryUntilUtc = now.AddSeconds(balance.RefractorySeconds);

        pendingLayers.Add(new PendingLayer(now.AddSeconds(balance.LayerDelaySeconds), partner));
        host.Notify("You climaxed.");
        host.SaveProgress();
    }

    private void FlushPendingLayers(DateTime now)
    {
        for (var i = pendingLayers.Count - 1; i >= 0; i--)
        {
            var pending = pendingLayers[i];
            if (now < pending.Due) continue;
            pendingLayers.RemoveAt(i);
            SendRandomLayer(pending.To);
        }
    }

    /// Layers a random part. A part the partner blocks costs nothing to find out, so it tries another until one is sent
    /// or Cummy says no for some other reason.
    private void SendRandomLayer(PeerRecord to)
    {
        var parts = CummyParts.All.ToArray();
        random.Shuffle(parts);
        foreach (var part in parts)
        {
            var outcome = host.SendLayer(to, part);
            if (outcome == "sent")
            {
                GiveXp(balance.LayerXp);
                goals.Record(GoalKind.LayersGiven);
            }
            if (outcome != "blocked") return;
        }
    }

    /// `/corr climax`. Null when it fired, otherwise why it didn't.
    public string? TryManualClimax(DateTime now)
    {
        if (save.Paused) return "You're paused.";
        if (!verified || verifiedAct is null || nearestInRange is null) return "There's no verified scene right now.";
        if (Arousal.InRefractory(save, now)) return "You're still recovering from the last one.";
        if (save.Arousal < balance.ManualClimaxMinimum)
            return $"Your arousal needs to be at least {balance.ManualClimaxMinimum:0} (it's {Math.Floor(save.Arousal):0}).";
        FireClimax(now, nearestInRange);
        return null;
    }

    // ── Partner's climax and layers ────────────────────────────────────────────

    private void CreditPartnerClimax(DateTime now, PeerRecord peer)
    {
        if (save.Paused) return;
        if (!string.Equals(lastTickPartner, peer.Key, StringComparison.OrdinalIgnoreCase)
            || (now - lastTickUtc).TotalSeconds > balance.PartnerClimaxRecencySeconds) return;
        if (lastClimaxCredit.TryGetValue(peer.Key, out var last)
            && (now - last).TotalSeconds < balance.PartnerClimaxLimitSeconds) return;

        lastClimaxCredit[peer.Key] = now;
        creditedClimax[peer.Key] = now;
        GiveXp(balance.ClimaxXp * balance.PartnerClimaxShare);
        goals.Record(GoalKind.PartnerClimaxes);
        host.Notify($"{peer.Name} climaxed.");

        // Cummy's tell and this tag travel separately, so the layer may have got here first.
        if (earlyLayers.Remove(peer.Key, out var early) && (now - early.At).TotalSeconds <= balance.LayerWindowSeconds)
        {
            GiveXp(balance.LayerXp * early.Count);
            goals.Record(GoalKind.LayersReceived, early.Count);
        }
    }

    /// Cummy reports a partner's layer changed a stage on this character.
    public void HandleLayerReceived(DateTime now, string sourceName, string sourceWorld)
    {
        if (save.Paused) return;
        var key = new PeerRecord { Name = sourceName, World = sourceWorld }.Key;
        if (creditedClimax.TryGetValue(key, out var climax) && (now - climax).TotalSeconds <= balance.LayerWindowSeconds)
        {
            GiveXp(balance.LayerXp);
            goals.Record(GoalKind.LayersReceived);
            return;
        }
        // Their climax may not have reached this client yet. Without one, this earns nothing.
        var count = earlyLayers.TryGetValue(key, out var earlier) && (now - earlier.At).TotalSeconds <= balance.LayerWindowSeconds ? earlier.Count : 0;
        earlyLayers[key] = (count + 1, now);
    }

    // ── Safeword ───────────────────────────────────────────────────────────────

    /// Stops everything at once. The tag goes out on the next sample, and nothing here waits for the partner.
    public void Pause()
    {
        if (save.Paused) return;
        save.Paused = true;
        ticker.Reset();
        pendingLayers.Clear();
        verified = false;
        verifiedAct = null;
        Status = Status with { State = SceneState.Paused, Failure = SceneFailure.Paused };
        host.Notify("Paused: scene XP and arousal have stopped. /corr resume to continue.");
        host.SaveProgress();
    }

    /// Hiding takes this character's tag down on the next sample, and cancels any layer still waiting to go out.
    public void SetHidden(bool hidden)
    {
        if (save.Hidden == hidden) return;
        save.Hidden = hidden;
        if (hidden) pendingLayers.Clear();
        host.Notify(hidden
            ? "You're hidden: other players can't see you have Corruption, and scenes can't earn."
            : "You're visible again.");
        host.SaveProgress();
    }

    public void Resume()
    {
        if (!save.Paused) return;
        save.Paused = false;
        host.Notify("Resumed.");
        host.SaveProgress();
    }

    /// `/corr prestige confirm` or the Profile button. Null when it happened, otherwise why not.
    public string? Prestige(DateTime now)
    {
        if (PrestigeRules.Refusal(save, balance) is { } refusal) return refusal;
        PrestigeRules.Apply(save, now, balance);
        ticker.Reset();
        lastTickXp = 0;
        host.Notify($"You prestiged. You're back at level 1 with {PrestigeRules.Label(save.Prestige, balance.StarsWithBonus)}.");
        host.SaveProgress();
        PayRewards();
        return null;
    }

    /// Bond, or Bond back. Null on success, otherwise why not.
    public string? RequestBond(PeerRecord player)
    {
        var theirLevel = PlateOf(player)?.Level ?? 0;
        if (Bonds.Request(player.Key, save.Level, theirLevel) is { } refusal) return refusal;
        host.SaveProgress();
        return null;
    }

    public void Unbond()
    {
        if (save.BondWith is null) return;
        Bonds.Unbond();
        host.SaveProgress();
    }

    public void ResetProgress(DateTime now)
    {
        save.ResetProgress(now);
        ticker.Reset();
        lastTickXp = 0;
        host.SaveProgress();
    }

    /// Ends the session cleanly: the final save happens through the host.
    public void Flush()
    {
        host.SaveProgress();
        dirty = false;
    }
}
