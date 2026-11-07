using System.Numerics;

namespace Corruption.Core;

/// What the engine asks of the outside world.
public interface IEngineHost
{
    /// Writes this character's tag, or removes it when null. False when it couldn't be written, so it's tried again.
    bool PublishTag(string? value);

    /// Writes the extra tag (stars and bond request), or removes it when null. False when it couldn't be written.
    bool PublishExtraTag(string? value);

    /// Writes the body tag, or removes it when null. False when it couldn't be written.
    bool PublishBodyTag(string? value);

    /// Asks this player's own Cummy to layer them as if <paramref name="source"/> had. Cummy's outcome (layered, advanced,
    /// full, blocked, unknown-part, no-character), or null when Cummy can't do it.
    string? LayerSelf(PeerRecord source, string partId);

    /// Cummy parts from nearest to farthest from the giver's genitals, read from both skeletons. Null when either
    /// character's bones can't be read, such as when the giver isn't drawn.
    IReadOnlyList<string>? AimParts(PeerRecord giver, Anatomy anatomy);

    void Notify(string text);

    /// A goal or achievement was completed, with the XP it actually paid.
    void Celebrate(GoalReward reward);

    /// Asks for a save now, rather than waiting for the periodic flush.
    void SaveProgress();
}

/// A nearby player whose Corruption tag this client can read: distance, and the looping pose this client can see on
/// them (null when they aren't in a loop).
/// Extra is null when they publish no extra tag, such as a client without prestige and bonds. Body is null for a client
/// without the body tag, which still sends Cummy layer tells.
public readonly record struct VisiblePlayer(PeerRecord Player, double Distance, PoseKey? Pose, PresenceTag Tag, ExtraTag? Extra = null,
    BodyTag? Body = null);

/// One second's worth of game state.
/// <param name="LayersWorn">Cummy layer stages worn, 0 when Cummy can't say.</param>
/// <param name="SelfKey">This character's `Name@World`, for reading bond requests.</param>
public readonly record struct SceneSample(ActEntry? OwnAct, PoseKey? OwnPose, Vector3 OwnPosition, IReadOnlyList<VisiblePlayer> Visible, int Watchers = 0,
    int LayersWorn = 0, string? SelfKey = null, ActEntry? Excluded = null);

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
    TickFactors Factors,
    ActEntry? Excluded = null)
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
    private readonly BodyTagPublisher bodyPublisher = new();
    private readonly Dictionary<string, BodyTag> bodies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PresenceTag> tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ExtraTag> extras = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> climaxCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly SceneTicker ticker = new();
    private readonly Dictionary<string, DateTime> partnerClimaxes = new(StringComparer.OrdinalIgnoreCase);
    private DateTime? lastOwnClimax;
    private DateTime? lastBondedClimax;
    private DateTime? lastTogetherPaid;
    private int ownClimaxCount;
    private bool tagPublished;
    private bool extraPublished;
    private bool bodyPublished;
    private int layersWorn;

    private readonly Dictionary<string, DateTime> lastClimaxCredit = new(StringComparer.OrdinalIgnoreCase);

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

    /// "Get layered when a partner climaxes", from Settings.
    public bool LayerOnPartnerClimax { get; set; } = true;

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
        bodyPublisher.Forget();
    }

    /// A nearby player's body tag, or null when their client has none.
    public BodyTag? BodyOf(PeerRecord player) => bodies.TryGetValue(player.Key, out var body) ? body : null;

    /// What this character's body tag says now.
    public BodyTag OwnBodyAt(DateTime now)
    {
        var p = save.Pregnancy;
        var anatomy = Anatomies.Flags(save.Anatomy);
        if (!p.Enabled) return BodyTag.Empty with { Flags = anatomy };
        var flags = anatomy | (p.CanCarry ? BodyFlags.CanCarry : 0) | (p.CanBreed ? BodyFlags.CanBreed : 0)
                    | (p.CanBreed && ItemBook.Virile(save, now) ? BodyFlags.Virile : 0);
        var father = PregnancyBook.IsPregnant(p) && p.FatherKey is { } key ? BondHashes.Of(key) : null;
        return new BodyTag(flags, PregnancyBook.StageOf(p, now), father);
    }

    /// Run about once per second.
    public void Sample(DateTime now, in SceneSample sample)
    {
        Arousal.ApplyDecay(save, now, balance);
        ApplyFading(now);
        if (save.Setup is not null)
        {
            UpdatePregnancy(now);
            if (CravingBook.Roll(save.Pregnancy, now, save.Goals.Seed, save.BondFormed) is { } craving)
                host.Notify(save.Pregnancy.Unsatisfied
                    ? $"Today you crave: {CravingBook.Describe(craving).ToLowerInvariant()}. You're still unsatisfied from yesterday."
                    : $"Today you crave: {CravingBook.Describe(craving).ToLowerInvariant()}.");
        }
        if (save.Setup is not null)
        {
            goals.Roll(now);
            CheckIn(now);
        }
        watchers = sample.Watchers;
        layersWorn = Math.Max(0, sample.LayersWorn);
        ItemBook.Expire(save, now);

        ReadTags(now, sample.Visible);
        NoticeFatherhood(sample.SelfKey, sample.Visible);
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

        var failure = SceneCheck.Evaluate(new SceneInput(
            save.Paused,
            sample.OwnAct is not null,
            nearest is not null,
            nearest?.Distance ?? 0,
            nearest?.Tag.Pose,
            nearest?.Pose,
            save.Setup is null,
            save.Hidden,
            sample.OwnAct is null && sample.Excluded is not null), balance.SceneRangeYalms);

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
            save.LongestSceneSeconds = Math.Max(save.LongestSceneSeconds, (lastPassUtc - sceneStarted.Value).TotalSeconds);
            sceneStarted = null;
            host.SaveProgress();
            dirty = false;
            lastSave = now;
        }

        var partner = nearest?.Player;
        var fatigue = verified && !ItemBook.NoFatigue(save, now) ? Progression.Fatigue(now - fatigueStart, balance) : 1.0;
        if (ticker.Sample(now, verified, partner?.Key, sample.OwnAct?.Key, balance.TickSeconds))
            AwardTick(now, sample.OwnAct!, partner!, fatigue);

        if (dirty && (now - lastSave).TotalSeconds >= balance.SaveIntervalSeconds)
        {
            host.SaveProgress();
            dirty = false;
            lastSave = now;
        }

        Status = new SceneStatus(StateOf(failure), failure, partner, sample.OwnAct, sceneStarted, fatigue, lastTickXp, watchers, lastFactors,
            sample.OwnAct is null ? sample.Excluded : null);
        PublishTag(now, sample.OwnAct?.Pose);
        PayRewards();
    }

    private void CheckIn(DateTime now)
    {
        var paid = Sins.CheckIn(save.Wallet, now);
        if (paid <= 0) return;
        var streak = save.Wallet.CheckInStreak;
        host.Notify(streak > 1
            ? $"Daily check-in: +{paid} Sins ({streak} days in a row)."
            : $"Daily check-in: +{paid} Sins.");
        dirty = true;
    }

    private void PayRewards()
    {
        if (save.Setup is null) return;
        foreach (var reward in goals.TakeRewards())
        {
            var sins = Sins.ForReward(reward.Kind);
            Sins.Earn(save.Wallet, sins);
            host.Notify($"{reward.Message} +{sins} Sins.");
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
            if (bodyPublished && host.PublishBodyTag(null)) bodyPublished = false;
            publisher.Forget();
            extraPublisher.Forget();
            bodyPublisher.Forget();
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
        if (bodyPublisher.Update(OwnBodyAt(now)) is { } body)
        {
            if (host.PublishBodyTag(body.Format())) bodyPublished = true;
            else bodyPublisher.Forget();
        }
    }

    /// Takes in what nearby players' tags say. A climax shows as their count going up. The first time a player is seen
    /// doesn't count, since the count could be from earlier.
    private void ReadTags(DateTime now, IReadOnlyList<VisiblePlayer> visible)
    {
        tags.Clear();
        extras.Clear();
        bodies.Clear();
        foreach (var player in visible)
        {
            tags[player.Player.Key] = player.Tag;
            if (player.Extra is { } extra) extras[player.Player.Key] = extra;
            if (player.Body is { } body) bodies[player.Player.Key] = body;
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

        Progression.UpdateStreak(save, now, () => ItemBook.TrySpendUse(save, ItemIds.StreakCharm));
        var bondLevel = Bonds.LevelWith(partner.Key);
        var seenWatchers = watchers * ItemBook.WatcherMultiplier(save, now);
        lastFactors = Progression.Factors(save.Level, PlateOf(partner)?.Level, seenWatchers, save.StreakDays, fatigue, balance,
            bondLevel, layersWorn, save.Prestige) with
            {
                Items = ItemBook.TickXpFactor(save, now),
                Pregnancy = PregnancyBook.TickXpFactor(save.Pregnancy),
            };
        var xp = balance.TickXp * lastFactors.Total;
        if (save.SeenPartners.Add(partner.Key)) xp += balance.FirstPartnerXp;
        if (save.SeenActs.Add(act.Key)) xp += balance.FirstActXp;
        lastTickXp = xp;
        GiveXp(xp);
        RecordTick(now, act, partner);
        GrowBond(partner);
        Sins.ForScene(save.Wallet, balance.TickSeconds);

        var refractory = Arousal.InRefractory(save, now);
        var tier = Progression.TierOf(save.Level);
        save.Arousal = Math.Min(100, save.Arousal + Arousal.Gain(tier, seenWatchers, refractory, balance) * (save.Setup?.ArousalGain ?? 1)
            * ArousalFactor(now, bondLevel));
        save.ArousalAnchorUtc = now;
        dirty = true;

        if (save.Arousal >= 100 && !refractory && save.Setup?.AutoClimax != false) FireClimax(now, partner);
    }

    /// Everything beyond tier, watchers and the setup gain that speeds or slows arousal on a tick.
    private double ArousalFactor(DateTime now, int bondLevel)
        => (bondLevel >= BondLevels.InTuneLevel ? balance.InTuneArousalFactor : 1) * ItemBook.ArousalFactor(save, now)
           * PregnancyBook.ArousalFactor(save.Pregnancy, now);

    /// Time with the bonded partner raises the bond's level.
    private void GrowBond(PeerRecord partner)
    {
        if (!Bonds.PaysWith(partner.Key)) return;
        var before = Bonds.Level;
        save.BondSeconds += balance.TickSeconds;
        var after = Bonds.Level;
        if (after > before)
        {
            host.Notify($"Your bond with {partner.Name} reached level {after}. {BondLevels.QuirkOf(after)}.");
            host.SaveProgress();
        }
    }

    /// At bond level 3, the player's own climax and the bonded partner's credited one within 30 seconds pay once.
    private void CheckTogether(DateTime now, PeerRecord partner)
    {
        if (Bonds.LevelWith(partner.Key) < BondLevels.TogetherLevel) return;
        if (lastOwnClimax is not { } mine || lastBondedClimax is not { } theirs) return;
        if (Math.Abs((mine - theirs).TotalSeconds) > balance.TogetherSeconds) return;
        var pair = mine > theirs ? mine : theirs;
        if (lastTogetherPaid == pair) return;
        lastTogetherPaid = pair;
        GiveXp(balance.TogetherXp);
        Sins.Earn(save.Wallet, Sins.Together);
        goals.Record(GoalKind.ClimaxesTogether);
        host.Notify($"You and {partner.Name} climaxed together. +{Sins.Together} Sins.");
    }

    private void RecordTick(DateTime now, ActEntry act, PeerRecord partner)
    {
        CravingsFromTick(now, act, partner);
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
            var sins = 0;
            for (var level = save.Level - result.LevelsGained + 1; level <= save.Level; level++) sins += Sins.ForLevel(level);
            Sins.Earn(save.Wallet, sins);
            host.Notify(result.NewTier is { } tier
                ? $"You reached level {save.Level} and the {tier} tier. +{sins} Sins."
                : $"You reached level {save.Level}. +{sins} Sins.");
            if (save.Level >= balance.LevelCap)
                host.Notify("You've reached the top. You can prestige from the Profile page or with /corr prestige.");
            host.SaveProgress();
        }
    }

    private void FireClimax(DateTime now, PeerRecord partner)
    {
        lastOwnClimax = now;
        ownClimaxCount++;
        Crave(now, CravingKind.Climax);
        goals.Record(GoalKind.Climaxes);
        GiveXp(balance.ClimaxXp);
        save.Arousal = balance.ArousalAfterClimax;
        save.ArousalAnchorUtc = now;
        save.RefractoryUntilUtc = now.AddSeconds(RefractorySeconds(now, partner));

        // The partner's own client layers them when it credits this climax, so nothing is sent from here.
        host.Notify("You climaxed.");
        CheckTogether(now, partner);
        host.SaveProgress();
    }

    private double RefractorySeconds(DateTime now, PeerRecord partner)
    {
        var seconds = balance.RefractorySeconds * ItemBook.RefractoryFactor(save, now);
        if (Bonds.LevelWith(partner.Key) >= BondLevels.DevotedLevel) seconds /= 2;
        return Math.Max(balance.MinimumRefractorySeconds, seconds);
    }

    /// `/corr climax`. Null when it fired, otherwise why it didn't.
    public string? TryManualClimax(DateTime now)
    {
        if (save.Paused) return "You're paused.";
        if (!verified || verifiedAct is null || nearestInRange is null) return "There's no verified scene right now.";
        if (Arousal.InRefractory(save, now)) return "You're still recovering from the last one.";
        var hairTrigger = ItemBook.IsActive(save, ItemIds.HairTrigger, now);
        var minimum = hairTrigger ? Math.Min(ItemBook.HairTriggerMinimum, balance.ManualClimaxMinimum) : balance.ManualClimaxMinimum;
        if (save.Arousal < minimum)
            return $"Your arousal needs to be at least {minimum:0} (it's {Math.Floor(save.Arousal):0}).";
        if (save.Arousal < balance.ManualClimaxMinimum) ItemBook.TrySpendUse(save, ItemIds.HairTrigger);
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
        GiveXp(balance.ClimaxXp * balance.PartnerClimaxShare);
        goals.Record(GoalKind.PartnerClimaxes);
        host.Notify($"{peer.Name} climaxed.");
        if (Bonds.PaysWith(peer.Key))
        {
            lastBondedClimax = now;
            CheckTogether(now, peer);
        }
        var groin = LayerSelfFor(peer);
        if (groin) RollConception(now, peer);
    }

    /// Layers this character through its own Cummy for a partner's credited climax: a random part, trying others while
    /// Cummy says the part is blocked. Only for partners whose client layers itself too (they publish the body tag); an
    /// older client still sends Cummy's own tell instead.
    /// The shot goes to the part nearest the giver's genitals, then the next nearest while a part is full or blocked.
    /// Random only when the bones can't be read. True when it reached the groin, the only place conception can come
    /// from; blocking the groin in Cummy keeps a carrier from conceiving.
    private bool LayerSelfFor(PeerRecord peer)
    {
        if (!LayerOnPartnerClimax || BodyOf(peer) is null) return false;
        string[] parts;
        if (host.AimParts(peer, BodyOf(peer)?.Anatomy ?? Anatomy.NotSet) is { Count: > 0 } aimed) parts = aimed.ToArray();
        else
        {
            parts = CummyParts.All.ToArray();
            random.Shuffle(parts);
        }
        var groin = false;
        var wanted = ItemBook.IsActive(save, ItemIds.CumMagnet, DateTime.MaxValue) ? 2 : 1;
        var landed = 0;
        foreach (var part in parts)
        {
            var outcome = host.LayerSelf(peer, part);
            if (part == PregnancyBook.GroinPart && outcome is "layered" or "advanced" or "full") groin = true;
            if (outcome is "layered" or "advanced")
            {
                GiveXp(balance.LayerXp);
                goals.Record(GoalKind.LayersReceived);
                Crave(lastTickUtc, CravingKind.Layer);
                if (++landed >= wanted) break;
                continue;
            }
            // A full part still took the shot, so try another for the layer.
            if (outcome is not ("blocked" or "full")) break;
        }
        if (landed > 0 && wanted > 1) ItemBook.TrySpendUse(save, ItemIds.CumMagnet);
        return groin;
    }


    // ── Pregnancy ──────────────────────────────────────────────────────────────

    private void UpdatePregnancy(DateTime now)
    {
        switch (PregnancyBook.Update(save.Pregnancy, now))
        {
            case PregnancyEvent.Birth:
                Sins.Earn(save.Wallet, Sins.Birth);
                var father = save.Pregnancy.BirthLog.LastOrDefault()?.FatherKey is { Length: > 0 } key ? $" {key.Split('@')[0]}'s child" : "";
                host.Notify($"You gave birth{(father.Length > 0 ? " to" + father : "")}. +{Sins.Birth} Sins. Rest for two days before your cycle starts again.");
                host.SaveProgress();
                break;
            case PregnancyEvent.RecoveryEnded:
                if (save.Pregnancy.CanCarry) host.Notify("You've recovered, and your cycle starts again at day 1.");
                host.SaveProgress();
                break;
        }
    }

    /// A carrier nearby whose body tag names this player as the father: told once per pregnancy. The tag only holds a hash,
    /// so nobody else can tell.
    private void NoticeFatherhood(string? selfKey, IReadOnlyList<VisiblePlayer> visible)
    {
        if (selfKey is null || save.Setup is null) return;
        var myHash = BondHashes.Of(selfKey);
        var told = save.Pregnancy.ToldFather;
        foreach (var player in visible)
        {
            if (player.Body is not { } body) continue;
            if (!body.IsPregnant || body.FatherHash != myHash)
            {
                told.Remove(player.Player.Key);
                continue;
            }
            if (!told.Add(player.Player.Key)) continue;
            save.Pregnancy.Fathered++;
            host.Notify($"{player.Player.Name} is carrying your child.");
            host.SaveProgress();
        }
    }

    /// A breeder's credited climax can lead to conception, decided here on the carrier's side.
    private void RollConception(DateTime now, PeerRecord peer)
    {
        if (BodyOf(peer) is not { CanBreed: true } body) return;
        var chance = PregnancyBook.ConceptionChance(save, now, body.Virile, Bonds.LevelWith(peer.Key), save.Paused || save.Hidden);
        if (chance <= 0 || random.NextDouble() >= chance) return;
        PregnancyBook.Conceive(save.Pregnancy, now, peer.Key);
        host.Notify($"You've conceived. {peer.Name} is the father.");
        host.SaveProgress();
    }

    private void CravingsFromTick(DateTime now, ActEntry act, PeerRecord partner)
    {
        var p = save.Pregnancy;
        if (!PregnancyBook.IsPregnant(p) || p.Craving is not { } craving || p.CravingMet) return;
        p.CravingSeconds += balance.TickSeconds;
        var newToday = !save.Goals.DaySeen.Contains($"{GoalKind.DistinctAnimations}:{act.Key}");
        var met = craving switch
        {
            CravingKind.BondedScene => Bonds.PaysWith(partner.Key),
            CravingKind.TenMinutes => p.CravingSeconds >= CravingBook.TenMinutesSeconds,
            CravingKind.Watched => watchers > 0,
            CravingKind.NewAnimation => newToday,
            _ => false,
        };
        if (met) Crave(now, craving);
    }

    private void Crave(DateTime now, CravingKind kind)
    {
        if (CravingBook.Met(save.Pregnancy, kind)) Satisfied(now);
    }

    private bool SatisfyCraving(DateTime now)
    {
        if (!CravingBook.Treat(save.Pregnancy)) return false;
        Satisfied(now);
        return true;
    }

    private void Satisfied(DateTime now)
    {
        Sins.Earn(save.Wallet, Sins.Craving);
        goals.Record(GoalKind.CravingsSatisfied);
        save.Arousal = Math.Min(100, save.Arousal + CravingBook.SatisfiedArousal);
        save.ArousalAnchorUtc = now;
        host.Notify($"Your craving is satisfied. +{Sins.Craving} Sins.");
        dirty = true;
    }

    /// Settings: turn pregnancy on with these roles. Null when done, otherwise why not.
    public string? EnablePregnancy(bool carry, bool breed, int startDay, DateTime now)
    {
        if (PregnancyBook.Enable(save.Pregnancy, carry, breed, startDay, now) is { } refusal) return refusal;
        host.SaveProgress();
        return null;
    }

    /// The welcome window's choices, all at once. The profile setup only applies before setup is done, since it's locked
    /// afterward. Null when done, otherwise why not, with nothing changed.
    public string? CompleteWelcome(WelcomeChoices choices, DateTime now)
    {
        var p = choices.Pregnancy;
        if (save.Setup is null && choices.Setup is null) return "Choose your profile setup first.";
        if (p.On && !p.Carry && !p.Breed) return "Choose at least one pregnancy role, or leave pregnancy off.";

        if (save.Setup is null) CompleteSetup(choices.Setup!, now);
        save.Anatomy = choices.Anatomy;
        if (p.On)
        {
            // Roles can't change while pregnant, so an ongoing pregnancy keeps its roles.
            if (!PregnancyBook.IsPregnant(save.Pregnancy)) PregnancyBook.Enable(save.Pregnancy, p.Carry, p.Breed, p.StartDay, now);
            save.Pregnancy.Enabled = true;
            save.Pregnancy.ChanceFactor = Math.Clamp(p.ChanceFactor, PregnancyBook.MinChanceFactor, PregnancyBook.MaxChanceFactor);
        }
        else if (save.Pregnancy.Enabled) PregnancyBook.Disable(save.Pregnancy);

        save.WelcomeVersion = Welcome.CurrentVersion;
        host.Notify("You're all set. Everything here can be changed later in Settings, except your locked profile.");
        host.SaveProgress();
        return null;
    }

    /// Settings: what this character has, sent to partners in the body tag.
    public void SetAnatomy(Anatomy anatomy)
    {
        save.Anatomy = anatomy;
        host.SaveProgress();
    }

    /// Settings, after asking: ends any pregnancy, the cycle and cravings.
    public void DisablePregnancy()
    {
        PregnancyBook.Disable(save.Pregnancy);
        host.Notify("Pregnancy is off.");
        host.SaveProgress();
    }

    // ── Items ──────────────────────────────────────────────────────────────────

    /// Null when bought, otherwise why not.
    public string? BuyItem(string id)
    {
        if (ItemBook.Buy(save, id) is { } refusal) return refusal;
        host.SaveProgress();
        return null;
    }

    /// Null when used, otherwise why not. Instant items go through <see cref="UseInstant"/>.
    public string? UseItem(string id, DateTime now)
    {
        if (ItemBook.Use(save, id, now, item => UseInstant(item, now)) is { } refusal) return refusal;
        host.Notify($"You used {ItemCatalog.Find(id)!.Name}.");
        goals.Record(GoalKind.ItemsUsed);
        host.SaveProgress();
        return null;
    }

    /// Instant items act on pregnancy. Null when it applied, otherwise why not.
    private string? UseInstant(ItemDef item, DateTime now) => item.Id switch
    {
        ItemIds.MorningAfter => PregnancyBook.MorningAfter(save.Pregnancy, now),
        ItemIds.HormoneTea => PregnancyBook.HormoneTea(save.Pregnancy, now),
        ItemIds.SwiftLabour => PregnancyBook.SwiftLabour(save.Pregnancy),
        ItemIds.CravingTreat => SatisfyCraving(now) ? null : "You have no craving to satisfy right now.",
        _ => $"{item.Name} can't be used right now.",
    };

    // ── Safeword ───────────────────────────────────────────────────────────────

    /// Stops everything at once. The tag goes out on the next sample, and nothing here waits for the partner.
    public void Pause()
    {
        if (save.Paused) return;
        save.Paused = true;
        ticker.Reset();
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
        if (Bonds.Request(player.Key) is { } refusal) return refusal;
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
