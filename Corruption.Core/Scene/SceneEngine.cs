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

    /// Writes the title tag, or removes it when null. False when it couldn't be written.
    bool PublishTitleTag(string? value);

    /// Writes the persona tag, or removes it when null. False when it couldn't be written.
    bool PublishPersonaTag(string? value);

    /// Asks this player's own Cummy to layer them as if <paramref name="source"/> had. Cummy's outcome (layered, advanced,
    /// full, blocked, unknown-part, no-character), or null when Cummy can't do it.
    string? LayerSelf(PeerRecord source, string partId);

    /// Cummy parts from nearest to farthest from the giver's genitals, read from both skeletons. Null when either
    /// character's bones can't be read, such as when the giver isn't drawn.
    IReadOnlyList<string>? AimParts(PeerRecord giver, Anatomy anatomy);

    /// The local character's own parts from nearest to farthest from their own genitals. Null when the bones can't be read.
    IReadOnlyList<string>? AimSelf(Anatomy anatomy);

    /// A partner's parts from nearest to farthest from the local character's genitals: where their climax most likely
    /// landed on the partner. Null when either character's bones can't be read.
    IReadOnlyList<string>? AimOnto(PeerRecord partner, Anatomy own);

    /// The hole ("pussy", "ass") the giver's penis tip is inside on the local character, by bones. Null when it's outside,
    /// the giver has no penis, or either skeleton can't be read.
    string? InsideOf(PeerRecord giver, Anatomy giverAnatomy, Anatomy own);

    /// The hole the local character's penis tip is inside on a partner, by bones. Null as for <see cref="InsideOf"/>.
    string? InsideOnto(PeerRecord partner, Anatomy own, Anatomy theirs);

    /// Asks this player's own Cummy to creampie them in a hole as if <paramref name="source"/> had. Cummy's outcome
    /// (creampied, refreshed, blocked, blacklisted, unknown-hole, no-character), or null below Cummy version 6.
    string? CreampieSelf(PeerRecord source, string hole);

    /// Asks Cummy to lick a partner's part. Cummy's outcome, or null below Cummy version 5.
    string? SendLick(PeerRecord partner, string partId);

    /// The parts a Cummy partner is last known to block, or null when they aren't a Cummy partner.
    IReadOnlyCollection<string>? PartnerBlocks(PeerRecord partner);

    void Notify(string text);

    /// A goal or achievement was completed, with the XP it actually paid.
    void Celebrate(GoalReward reward);

    /// A scene has ended, with what it added up to.
    void SceneEnded(SceneSummary summary);

    /// Asks for a save now, rather than waiting for the periodic flush.
    void SaveProgress();
}

/// A nearby player whose Corruption tag this client can read: distance, and the looping pose this client can see on
/// them (null when they aren't in a loop).
/// Extra is null when they publish no extra tag, such as a client without prestige and bonds. Body is null for a client
/// without the body tag, which still sends Cummy layer tells.
/// TitleId is the id from their title tag, null for none or a client without titles. Persona is null for a client
/// without personalities.
public readonly record struct VisiblePlayer(PeerRecord Player, double Distance, PoseKey? Pose, PresenceTag Tag, ExtraTag? Extra = null,
    BodyTag? Body = null, string? TitleId = null, PersonaTag? Persona = null);

/// One second's worth of game state.
/// <param name="LayersWorn">Cummy layer stages worn, 0 when Cummy can't say.</param>
/// <param name="SelfKey">This character's `Name@World`, for reading bond requests.</param>
/// <param name="Stages">From Cummy version 5: each layered part's stage. Null below it.</param>
/// <param name="Blocked">From Cummy version 5: the parts the wearer blocks. Null below it.</param>
/// <param name="PartSizes">From Cummy version 5: each part's stage count. Null below it.</param>
/// <param name="Creampies">From Cummy version 6: seconds left per dripping hole ("pussy", "ass"). Null below it.</param>
public readonly record struct SceneSample(ActEntry? OwnAct, PoseKey? OwnPose, Vector3 OwnPosition, IReadOnlyList<VisiblePlayer> Visible, int Watchers = 0,
    int LayersWorn = 0, string? SelfKey = null, ActEntry? Excluded = null, IReadOnlyDictionary<string, int>? Stages = null,
    IReadOnlyCollection<string>? Blocked = null, IReadOnlyDictionary<string, int>? PartSizes = null,
    IReadOnlyDictionary<string, int>? Creampies = null);

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
    ActEntry? Excluded = null,
    bool Solo = false)
{
    public static readonly SceneStatus Empty = new(SceneState.Idle, SceneFailure.NoKnownAct, null, null, null, 1, 0, 0, TickFactors.None);
}

/// One character's scene loop: ties the scene check to XP, arousal, climaxes and the tag. Everything here is decided from
/// this client's own view; all it shares is its tag.
public sealed partial class SceneEngine(CharacterSave save, Balance balance, IEngineHost host, Random? random = null)
{
    private readonly Random random = random ?? Random.Shared;

    private readonly GoalBook goals = new(save);
    private readonly TagPublisher publisher = new();
    private readonly ExtraTagPublisher extraPublisher = new();
    private readonly BodyTagPublisher bodyPublisher = new();
    private readonly TitleTagPublisher titlePublisher = new();
    private readonly PersonaTagPublisher personaPublisher = new();
    private readonly Dictionary<string, PersonaTag> personas = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BodyTag> bodies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PresenceTag> tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ExtraTag> extras = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> titles = new(StringComparer.OrdinalIgnoreCase);
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
    private bool titlePublished;
    private bool personaPublished;
    private int layersWorn;

    /// Lick clean, after the local character's climax with a partner. Null when none is offered.
    private LickOffer? lickOffer;

    /// When each player last had a verified tick with this character, for being licked clean by a recent partner.
    private readonly Dictionary<string, DateTime> lastTickWith = new(StringComparer.OrdinalIgnoreCase);
    private DateTime lastCleanedArousal = DateTime.MinValue;

    /// The arousal paid at <see cref="lastCleanedArousal"/>, so being licked out can top a plain lick up rather than add to it.
    private double lastCleanedPaid;

    /// What Cummy version 5 says about the wearer: stages, blocks and part sizes. Null below it.
    private IReadOnlyDictionary<string, int>? stages;
    private IReadOnlyCollection<string>? blocked;
    private IReadOnlyDictionary<string, int>? partSizes;

    /// What Cummy version 6 says is dripping: seconds left per hole. Null below it.
    private IReadOnlyDictionary<string, int>? creampies;

    /// The holes dripping now, by Cummy version 6. Empty below it.
    public IReadOnlyCollection<string> DrippingHoles => creampies?.Where(c => c.Value > 0).Select(c => c.Key).ToList() ?? [];

    private readonly Dictionary<string, DateTime> lastClimaxCredit = new(StringComparer.OrdinalIgnoreCase);

    private string? lastTickPartner;
    private DateTime lastTickUtc = DateTime.MinValue;
    private DateTime lastPassUtc = DateTime.MinValue;
    private DateTime? sceneStarted;

    /// Verified seconds in the current scene: one per verified sample, which comes once a second.
    private double sceneVerifiedSeconds;

    /// The running scene's totals, while one is running.
    private SceneSummary? tally;
    private long tallySinsAtStart;
    private string? fatigueAct;
    private DateTime fatigueStart;
    private double lastTickXp;
    private TickFactors lastFactors = TickFactors.None;
    private int watchers;
    private DateTime lastSave = DateTime.MinValue;
    private bool dirty;
    private DateTime nextFadeCheck = DateTime.MinValue;

    private bool verified;

    /// The verified scene this second is solo: no partner, a solo-marked act.
    private bool solo;
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
        => tags.TryGetValue(player.Key, out var tag)
            ? new PlateValues(tag.Level, tag.Arousal, StarsOf(player), TitleOf(player), PersonaOf(player)?.Personality)
            : null;

    /// A nearby player's stars, from their extra tag; 0 when they have none.
    public int StarsOf(PeerRecord player) => extras.TryGetValue(player.Key, out var extra) ? extra.Stars : 0;

    public PlateValues OwnPlate => new(save.Level, save.Arousal, save.Prestige, TitleBook.Equipped(save)?.Id, PersonaBook.Personality(save).Id);

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
        titlePublisher.Forget();
        personaPublisher.Forget();
    }

    /// A nearby player's body tag, or null when their client has none.
    public BodyTag? BodyOf(PeerRecord player) => bodies.TryGetValue(player.Key, out var body) ? body : null;

    /// The title id a nearby player's title tag shows, or null. It may be one this version doesn't know.
    public string? TitleOf(PeerRecord player) => titles.TryGetValue(player.Key, out var title) ? title : null;

    /// What this character's body tag says now.
    public BodyTag OwnBodyAt(DateTime now)
    {
        var p = save.Pregnancy;
        var anatomy = Anatomies.Flags(save.Anatomy);
        if (!p.Enabled) return BodyTag.Empty with { Flags = anatomy };
        var flags = anatomy | (p.CanCarry ? BodyFlags.CanCarry : 0) | (p.CanBreed ? BodyFlags.CanBreed : 0)
                    | (p.CanBreed && ItemBook.Virile(save, now) ? BodyFlags.Virile : 0);
        // A secret pregnancy reads as none, so neither the father nor anyone else can tell.
        var father = PregnancyBook.IsConfirmed(p) && p.FatherKey is { } key ? BondHashes.Of(key) : null;
        return new BodyTag(flags, PregnancyBook.ShownStage(p, now), father);
    }

    /// Run about once per second.
    public void Sample(DateTime now, in SceneSample sample)
    {
        Arousal.ApplyDecay(save, now, balance, ModifierBook.For(save).ArousalDecay);
        ApplyFading(now);
        PregnancyBook.Migrate(save.Pregnancy);
        if (save.Setup is not null)
        {
            UpdatePregnancy(now);
            if (CravingBook.Roll(save.Pregnancy, now, save.Goals.Seed, save.BondFormed, save.HadCummyV6) is { } craving)
                host.Notify(save.Pregnancy.Unsatisfied
                    ? $"Today you crave: {CravingBook.Describe(craving).ToLowerInvariant()}. You're still unsatisfied from yesterday."
                    : $"Today you crave: {CravingBook.Describe(craving).ToLowerInvariant()}.");
        }
        if (save.Setup is not null)
        {
            goals.Roll(now);
            CheckIn(now);
            PartnerBook.Seed(save);
        }
        watchers = sample.Watchers;
        selfKey = sample.SelfKey;
        layersWorn = Math.Max(0, sample.LayersWorn);
        stages = sample.Stages;
        blocked = sample.Blocked;
        partSizes = sample.PartSizes;
        creampies = sample.Creampies;
        NoteCummyStages();
        NoteCreampies();
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

        var (failure, isSolo) = SceneCheck.Check(new SceneInput(
            save.Paused,
            sample.OwnAct is not null,
            nearest is not null,
            nearest?.Distance ?? 0,
            nearest?.Tag.Pose,
            nearest?.Pose,
            save.Setup is null,
            save.Hidden,
            sample.OwnAct is null && sample.Excluded is not null,
            sample.OwnAct?.Solo == true,
            SoloBook.SecondsLeft(save, now, ModifierBook.For(save)) > 0), balance.SceneRangeYalms);

        verified = failure == SceneFailure.None;
        solo = verified && isSolo;
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
            // A solo scene has no partner. One arriving later joins the same scene.
            var partnerKey = solo ? null : nearest!.Value.Player.Key;
            if (sceneStarted is null)
            {
                sceneStarted = now;
                sceneVerifiedSeconds = 0;
                tally = new SceneSummary { StartedUtc = now };
                tallySinsAtStart = save.Wallet.EarnedTotal;
                if (partnerKey is not null) goals.RecordPartnerScene(partnerKey);
                SceneStarted?.Invoke(solo ? null : nearest!.Value.Player, solo);
            }
            if (solo) SoloBook.Spend(save, now);
            sceneVerifiedSeconds++;
            if (save.PentUpSecondsLeft > 0) save.PentUpSecondsLeft = Math.Max(0, save.PentUpSecondsLeft - 1);
            save.LongestVerifiedSceneSeconds = Math.Max(save.LongestVerifiedSceneSeconds, sceneVerifiedSeconds);
            save.PeakWatchers = Math.Max(save.PeakWatchers, watchers);
            if (tally is not null)
            {
                tally.VerifiedSeconds = sceneVerifiedSeconds;
                tally.PeakWatchers = Math.Max(tally.PeakWatchers, watchers);
                if (partnerKey is not null)
                {
                    if (!tally.Partners.Contains(partnerKey, StringComparer.OrdinalIgnoreCase))
                    {
                        tally.Partners.Add(partnerKey);
                        PartnerBook.Started(save, partnerKey, now);
                    }
                    PartnerBook.Together(save, partnerKey, now, 1);
                }
            }
        }
        else if (sceneStarted is not null && (now - lastPassUtc).TotalSeconds >= balance.SceneEndSeconds)
        {
            save.LongestSceneSeconds = Math.Max(save.LongestSceneSeconds, (lastPassUtc - sceneStarted.Value).TotalSeconds);
            sceneStarted = null;
            EndTally();
            host.SaveProgress();
            dirty = false;
            lastSave = now;
        }

        var partner = solo ? null : nearest?.Player;
        var fatigue = verified && !ItemBook.NoFatigue(save, now)
            ? Progression.Fatigue(now - fatigueStart, balance, ModifierBook.For(save).FatigueGraceMinutes)
            : 1.0;
        if (ticker.Sample(now, verified, solo ? SoloRules.TickerKey : partner?.Key, sample.OwnAct?.Key, balance.TickSeconds))
            AwardTick(now, sample.OwnAct!, partner, fatigue);

        if (dirty && (now - lastSave).TotalSeconds >= balance.SaveIntervalSeconds)
        {
            host.SaveProgress();
            dirty = false;
            lastSave = now;
        }

        Status = new SceneStatus(StateOf(failure), failure, partner, sample.OwnAct, sceneStarted, fatigue, lastTickXp, watchers, lastFactors,
            sample.OwnAct is null ? sample.Excluded : null, solo);
        PublishTag(now, sample.OwnAct?.Pose);
        PayRewards();
        SyncTitles();
        NoteApiTransitions(now);
    }

    /// The scene has ended: its summary becomes the last scene and goes to the host.
    private void EndTally()
    {
        if (tally is not { } summary) return;
        tally = null;
        summary.EndedUtc = lastPassUtc;
        summary.Sins = save.Wallet.EarnedTotal - tallySinsAtStart;
        save.LastScene = summary;
        host.SceneEnded(summary);
        SceneFinished?.Invoke(summary.VerifiedSeconds, summary.Xp);
    }

    private void CheckIn(DateTime now)
    {
        var paid = Sins.CheckIn(save.Wallet, now);
        if (paid <= 0) return;
        if (ModifierBook.For(save).CheckInBonus is > 0 and var extra)
        {
            Sins.Earn(save.Wallet, extra);
            paid += extra;
        }
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
            if (titlePublished && host.PublishTitleTag(null)) titlePublished = false;
            if (personaPublished && host.PublishPersonaTag(null)) personaPublished = false;
            publisher.Forget();
            extraPublisher.Forget();
            bodyPublisher.Forget();
            titlePublisher.Forget();
            personaPublisher.Forget();
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
        if (titlePublisher.Update(new TitleTag(TitleBook.Equipped(save)?.Id)) is { } title)
        {
            if (host.PublishTitleTag(title.Format())) titlePublished = true;
            else titlePublisher.Forget();
        }
        if (personaPublisher.Update(PersonaTag.Of(save)) is { } persona)
        {
            if (host.PublishPersonaTag(persona.Format())) personaPublished = true;
            else personaPublisher.Forget();
        }
    }

    /// Takes in what nearby players' tags say. A climax shows as their count going up. The first time a player is seen
    /// doesn't count, since the count could be from earlier.
    private void ReadTags(DateTime now, IReadOnlyList<VisiblePlayer> visible)
    {
        tags.Clear();
        extras.Clear();
        bodies.Clear();
        titles.Clear();
        personas.Clear();
        foreach (var player in visible)
        {
            if (player.Persona is { } persona) personas[player.Player.Key] = persona;
            tags[player.Player.Key] = player.Tag;
            if (player.Extra is { } extra) extras[player.Player.Key] = extra;
            if (player.Body is { } body) bodies[player.Player.Key] = body;
            if (player.TitleId is { } title) titles[player.Player.Key] = title;
        }

        // Every climax seen this second goes into the group window first, so the order players are read in doesn't
        // decide whether a credited climax becomes Full Body.
        var climaxed = new List<PeerRecord>();
        foreach (var player in visible)
        {
            var count = player.Tag.ClimaxCount;
            if (climaxCounts.TryGetValue(player.Player.Key, out var last) && count != last && count > 0)
            {
                climaxed.Add(player.Player);
                if (player.Distance <= balance.SceneRangeYalms && player.Tag.Pose is not null) groupClimaxes.Add(now);
            }
            climaxCounts[player.Player.Key] = count;
        }
        foreach (var player in climaxed)
        {
            partnerClimaxes[player.Key] = now;
            CreditPartnerClimax(now, player);
        }
    }

    /// Times of climaxes from nearby players in acts, credited or not, for Full Body.
    private readonly List<DateTime> groupClimaxes = [];

    /// Three climaxes nearby within the last 60 seconds, with Cummy version 5.
    private bool FullBodyDue(DateTime now)
    {
        groupClimaxes.RemoveAll(t => now - t > FullBodyWindow);
        return stages is not null && groupClimaxes.Count >= FullBodyClimaxes;
    }

    public static readonly TimeSpan FullBodyWindow = TimeSpan.FromSeconds(60);
    public const int FullBodyClimaxes = 3;

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

    /// <paramref name="partner"/> is null for a solo tick.
    private void AwardTick(DateTime now, ActEntry act, PeerRecord? partner, double fatigue)
    {
        lastTickPartner = partner?.Key;
        if (partner is not null) lastTickWith[partner.Key] = now;
        lastTickUtc = now;
        var title = ModifierBook.For(save);
        // Back after a day or more: a Hypersexual character is Pent Up until they've had a while in scenes again.
        if (title.PentUp && save.LastVerifiedUtc != default && now - save.LastVerifiedUtc >= PersonaCatalog.PentUpAfter)
            save.PentUpSecondsLeft = PersonaCatalog.PentUpSeconds;
        save.LastVerifiedUtc = now;

        Progression.UpdateStreak(save, now, () => ItemBook.TrySpendUse(save, ItemIds.StreakCharm), title.StreakEndsOnMiss);
        var bondLevel = partner is null ? 0 : Bonds.LevelWith(partner.Key);
        var seenWatchers = SeenWatchers(now, title, partner is null);
        lastFactors = Progression.Factors(save.Level, partner is null ? null : PlateOf(partner)?.Level, seenWatchers, save.StreakDays, fatigue, balance,
            bondLevel, layersWorn, save.Prestige, title, FullMoonAt(now)) with
            {
                Items = ItemBook.TickXpFactor(save, now),
                Pregnancy = PregnancyBook.TickXpFactor(save.Pregnancy),
                Persona = PersonaTickFactor(partner, title),
                // Cummy version 5 knows each part, so the marked factor can weigh the face and a full coat.
                Marked = stages is not null
                    ? Progression.WeightedMarkedFactor(stages, blocked ?? [], balance, title, DrippingHoles.Count)
                    : Progression.MarkedFactor(layersWorn, balance, title.MarkedCap, title.MarkedPerLayer),
                Solo = partner is null ? SoloRules.TickXp * title.SoloTickXp : 1,
            };
        var xp = balance.TickXp * lastFactors.Total;
        if (partner is not null && save.SeenPartners.Add(partner.Key)) xp += balance.FirstPartnerXp * title.FirstPartnerXp;
        if (save.SeenActs.Add(act.Key)) xp += balance.FirstActXp * title.FirstActXp;
        lastTickXp = xp;
        GiveXp(xp);
        RecordTick(now, act, partner);
        CountFullMoon(now);
        if (partner is not null) GrowBond(partner);
        Sins.ForScene(save.Wallet, balance.TickSeconds * title.SceneSins * SceneSinsMoonFactor(now));

        var refractory = Arousal.InRefractory(save, now);
        var tier = Progression.TierOf(save.Level);
        save.Arousal = Math.Min(100, save.Arousal + Arousal.Gain(tier, seenWatchers, refractory, balance, title.WatcherCap)
            * (save.Setup?.ArousalGain ?? 1) * ArousalFactor(now, bondLevel) * (partner is null ? SoloArousalFactor(now, title) : 1));
        save.ArousalAnchorUtc = now;
        dirty = true;

        if (save.Arousal >= 100 && !refractory && save.Setup?.AutoClimax != false) FireClimax(now, partner);
    }

    /// Everything beyond tier, watchers and the setup gain that speeds or slows arousal on a tick.
    private double ArousalFactor(DateTime now, int bondLevel)
    {
        var mods = ModifierBook.For(save);
        return (bondLevel >= BondLevels.InTuneLevel ? balance.InTuneArousalFactor : 1) * ItemBook.ArousalFactor(save, now)
               * PregnancyBook.ArousalFactor(save.Pregnancy, now) * ArousalMoonFactor(now)
               * mods.Arousal * (save.Arousal >= TitleRules.HighArousal ? mods.HighArousal : 1)
               * (watchers == 0 ? mods.QuietArousal : 1);
    }

    /// The personality's and traits' part of a tick: their own factor, quiet scenes, time away from the bond, Pent Up
    /// and chemistry with the partner.
    private double PersonaTickFactor(PeerRecord? partner, Modifiers mods)
    {
        // Alone isn't time away from the bond, and has no one to have chemistry with.
        var awayFromBond = partner is not null && save.BondFormed && save.BondWith is { } bond
                           && !string.Equals(bond, partner.Key, StringComparison.OrdinalIgnoreCase);
        return mods.PersonaTickXp
               * (watchers == 0 ? mods.QuietTickXp : 1)
               * (awayFromBond ? mods.AwayFromBondTickXp : 1)
               * (IsPentUp ? PersonaCatalog.PentUpXp : 1)
               * (partner is not null && HasChemistryWith(partner) ? PersonaCatalog.ChemistryXp : 1);
    }

    /// Watchers as a tick counts them: each one weighed by items, titles and the persona, and alone, a Vanity Mirror
    /// counts as one more.
    private double SeenWatchers(DateTime now, Modifiers mods, bool alone)
        => (watchers + (alone && ItemBook.IsActive(save, ItemIds.VanityMirror, now) ? 1 : 0))
           * ItemBook.WatcherMultiplier(save, now) * mods.WatcherCount * (alone ? mods.SoloWatcherCount : 1);

    /// What speeds arousal only alone: a Toy, and solo titles.
    private double SoloArousalFactor(DateTime now, Modifiers mods)
        => mods.SoloArousal * (ItemBook.IsActive(save, ItemIds.Toy, now) ? ItemBook.ToyArousal : 1);

    /// The verified scene right now is solo.
    public bool IsSolo => solo;

    /// "Layer yourself when you climax alone", from Settings.
    public bool LayerSelfWhenAlone { get; set; } = true;

    /// This character as `Name@World`, from the last sample, so a solo climax can name them as its source.
    private string? selfKey;

    /// A solo climax layers this character, aimed from their own genitals. Pays no layer XP.
    private void LayerSelfAlone()
    {
        if (!LayerSelfWhenAlone || selfKey is not { } key || key.IndexOf('@') is not (> 0 and var at)) return;
        var self = new PeerRecord { Name = key[..at], World = key[(at + 1)..] };
        foreach (var part in Unblocked(SelfAimOrder(save.Anatomy, host.AimSelf(save.Anatomy))))
        {
            var outcome = host.LayerSelf(self, part);
            if (outcome is "layered" or "advanced") return;
            // A full or blocked part moves on to the next nearest, as with a partner.
            if (outcome is not ("blocked" or "full")) return;
        }
    }

    /// Where a solo climax lands. A vagina gets the groin first. Otherwise the groin is left out, since its anchor sits
    /// at the genitals and would always be nearest; the next nearest is usually a hand. Without bones, Cummy's order.
    public static IReadOnlyList<string> SelfAimOrder(Anatomy anatomy, IReadOnlyList<string>? aimed)
    {
        var order = (aimed is { Count: > 0 } ? aimed : CummyParts.All).ToList();
        if (anatomy == Anatomy.Vagina)
        {
            order.Remove(CummyParts.Groin);
            order.Insert(0, CummyParts.Groin);
            return order;
        }
        order.Remove(CummyParts.Groin);
        return order;
    }

    /// What Cummy version 5's stages unlock: lick goals, Head to Toe and Glazed.
    private void NoteCummyStages()
    {
        if (stages is null || save.Setup is null) return;
        if (!save.HadCummyV5)
        {
            save.HadCummyV5 = true;
            dirty = true;
        }
        if (save.HeadToToe == 0 && Progression.IsFullCoat(stages, blocked ?? []))
        {
            save.HeadToToe = 1;
            dirty = true;
        }
        if (save.Glazed == 0 && partSizes?.GetValueOrDefault("mo") is > 0 and var top && stages.GetValueOrDefault("mo") >= top)
        {
            save.Glazed = 1;
            dirty = true;
        }
    }

    /// What Cummy version 6 unlocks: creampie goals and cravings, and Both Ways.
    private void NoteCreampies()
    {
        if (creampies is null || save.Setup is null) return;
        if (!save.HadCummyV6)
        {
            save.HadCummyV6 = true;
            dirty = true;
        }
        if (save.BothWays == 0 && DrippingHoles.Count >= 2)
        {
            save.BothWays = 1;
            dirty = true;
        }
    }

    /// Parts the wearer doesn't block in Cummy, so Cummy is never asked for one it would refuse. Below version 5 the
    /// blocks aren't known, and Cummy's "blocked" answer moves the aim on instead.
    private IEnumerable<string> Unblocked(IEnumerable<string> parts) => blocked is null ? parts : parts.Where(p => !blocked.Contains(p));

    /// Solo seconds left today.
    public double SoloSecondsLeft(DateTime now) => SoloBook.SecondsLeft(save, now, ModifierBook.For(save));

    /// Pent Up right now, with the verified seconds it has left.
    public bool IsPentUp => save.PentUpSecondsLeft > 0 && ModifierBook.For(save).PentUp;

    /// Whether this character's personality suits the partner's, from their persona tag.
    public bool HasChemistryWith(PeerRecord partner)
        => PersonaCatalog.HaveChemistry(PersonaBook.Personality(save).Id, PersonaOf(partner)?.Personality);

    /// A nearby player's persona tag, or null for a client without personalities.
    public PersonaTag? PersonaOf(PeerRecord player) => personas.TryGetValue(player.Key, out var persona) ? persona : null;

    /// The moon right now, which every client works out the same way.
    public MoonInfo MoonAt(DateTime now) => EorzeaMoon.At(now, balance);

    private bool FullMoonAt(DateTime now) => MoonAt(now).Phase == MoonPhase.FullMoon;

    private double ArousalMoonFactor(DateTime now) => FullMoonAt(now) ? balance.FullMoonArousalFactor : 1;

    private double SceneSinsMoonFactor(DateTime now) => MoonAt(now).Phase == MoonPhase.NewMoon ? balance.NewMoonSceneSinsFactor : 1;

    /// Each Full Moon with a verified tick counts once toward Moon-Touched.
    private void CountFullMoon(DateTime now)
    {
        var moon = MoonAt(now);
        if (moon.Phase != MoonPhase.FullMoon || moon.Cycle == save.LastFullMoonCycle) return;
        save.LastFullMoonCycle = moon.Cycle;
        save.FullMoonsSeen++;
    }

    /// Time with the bonded partner raises the bond's level.
    private void GrowBond(PeerRecord partner)
    {
        if (!Bonds.PaysWith(partner.Key)) return;
        var before = Bonds.Level;
        save.BondSeconds += balance.TickSeconds * ModifierBook.For(save).BondTime;
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
        var title = ModifierBook.For(save);
        if (Math.Abs((mine - theirs).TotalSeconds) > (title.TogetherSeconds ?? balance.TogetherSeconds)) return;
        var pair = mine > theirs ? mine : theirs;
        if (lastTogetherPaid == pair) return;
        lastTogetherPaid = pair;
        var sins = (int)Math.Round(Sins.Together * title.TogetherPay);
        GiveXp(balance.TogetherXp * title.TogetherPay);
        Sins.Earn(save.Wallet, sins);
        goals.Record(GoalKind.ClimaxesTogether);
        host.Notify($"You and {partner.Name} climaxed together. +{sins} Sins.");
    }

    private void RecordTick(DateTime now, ActEntry act, PeerRecord? partner)
    {
        CravingsFromTick(now, act, partner);
        var seconds = (long)balance.TickSeconds;
        goals.Record(GoalKind.SceneSeconds, seconds);
        if (partner is null) goals.Record(GoalKind.SoloSeconds, seconds);
        if (watchers > 0) goals.Record(GoalKind.WatchedSeconds, seconds);
        if (watchers >= 3) goals.Record(GoalKind.CrowdSeconds, seconds);
        if (save.Arousal >= 100) goals.Record(GoalKind.EdgedSeconds, (long)Math.Round(seconds * ModifierBook.For(save).EdgedCount));
        goals.RecordOnce(GoalKind.DistinctAnimations, act.Key);
        if (partner is not null) goals.RecordOnce(GoalKind.DistinctPartners, partner.Key);
        goals.RecordOnce(GoalKind.ActiveDays, now.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
    }

    private void GiveXp(double amount)
    {
        var scaled = amount * (save.Setup?.XpMultiplier ?? 1);
        if (tally is not null && save.Level < balance.LevelCap) tally.Xp += scaled;
        var result = Progression.AddXp(save, scaled, balance);
        dirty = true;
        if (result.LevelsGained > 0)
        {
            var sins = 0;
            for (var level = save.Level - result.LevelsGained + 1; level <= save.Level; level++) sins += Sins.ForLevel(level);
            Sins.Earn(save.Wallet, sins);
            host.Notify(result.NewTier is { } tier
                ? $"You reached level {save.Level} and the {tier} tier. +{sins} Sins."
                : $"You reached level {save.Level}. +{sins} Sins.");
            if (result.NewTier is { } reached)
                host.Celebrate(new GoalReward(RewardKind.Tier, reached.ToString(), "", 0, $"Level {save.Level}"));
            if (save.Level >= balance.LevelCap)
                host.Notify("You've reached the top. You can prestige from the Profile page or with /corr prestige.");
            host.SaveProgress();
            LeveledUp?.Invoke(save.Level);
        }
    }

    /// <paramref name="partner"/> is null for a climax alone.
    private void FireClimax(DateTime now, PeerRecord? partner)
    {
        lastOwnClimax = now;
        ownClimaxCount++;
        if (tally is not null) tally.OwnClimaxes++;
        Crave(now, CravingKind.Climax);
        goals.Record(GoalKind.Climaxes);
        var mods = ModifierBook.For(save);
        var share = 1.0;
        if (partner is null)
        {
            // Alone pays half, unless an Afterglow Candle use makes it whole.
            share = (ItemBook.TrySpendUse(save, ItemIds.AfterglowCandle) ? 1 : SoloRules.ClimaxXp) * mods.SoloClimaxXp;
            goals.Record(GoalKind.SoloClimaxes);
            if (watchers >= 3) save.CaughtInTheAct++;
        }
        GiveXp(balance.ClimaxXp * mods.ClimaxXp * share);
        save.Arousal = mods.ArousalAfterClimax ?? balance.ArousalAfterClimax;
        save.ArousalAnchorUtc = now;
        save.RefractoryUntilUtc = now.AddSeconds(RefractorySeconds(now, partner));

        // The partner's own client layers them when it credits this climax, so nothing is sent from here. Alone, this
        // client layers its own character.
        host.Notify("You climaxed.");
        if (partner is not null) CheckTogether(now, partner);
        else LayerSelfAlone();
        // Aftercare: with Cummy version 5, the partner can be licked clean for a while.
        // With version 6, a climax inside them is licked up from that hole.
        if (partner is not null && stages is not null)
            lickOffer = new LickOffer(partner, now + AftercareRules.OfferFor,
                Hole: creampies is null ? null : host.InsideOnto(partner, save.Anatomy, BodyOf(partner)?.Anatomy ?? Anatomy.NotSet));
        host.SaveProgress();
        Climaxed?.Invoke();
    }

    // ── Aftercare ──────────────────────────────────────────────────────────────

    /// Lick clean as it stands now, while it's offered or waiting; null otherwise. Message is the last answer, if any.
    public LickOffer? LickOfferAt(DateTime now) => lickOffer is { } offer && offer.IsOpen(now) ? offer : null;

    /// The last lick's outcome, kept a little while for the UI even after the offer closes.
    public LickOffer? LastLick => lickOffer;

    /// Licks the partner's part nearest this character's genitals. Null when sent, otherwise why not.
    public string? TryLickClean(DateTime now)
    {
        if (lickOffer is not { } offer || !offer.IsOpen(now)) return "There's nobody to lick clean right now.";
        if (offer.State == LickState.Waiting) return "The lick is on its way.";
        var blocks = host.PartnerBlocks(offer.Partner);
        var holePart = offer.Hole is { } hole ? CreampieAim.PartOf(hole) : null;
        var part = holePart is not null && blocks?.Contains(holePart) != true
            ? holePart
            : (host.AimOnto(offer.Partner, save.Anatomy) ?? []).FirstOrDefault(p => blocks?.Contains(p) != true) ?? AftercareRules.FallbackPart;
        var outcome = host.SendLick(offer.Partner, part);
        if (outcome == "sent")
        {
            lickOffer = offer with { State = LickState.Waiting, Message = null, AtHole = part == holePart };
            return null;
        }
        var why = outcome switch
        {
            null => "Licking needs Cummy's plugin API 5.",
            "not-paired" => $"{offer.Partner.Name} isn't your Cummy partner, and Cummy isn't in Public mode.",
            "out-of-range" => $"{offer.Partner.Name} is too far away to lick.",
            "blacklisted" => $"{offer.Partner.Name} is on your Cummy blacklist.",
            "queue-full" => "Cummy is busy. Try again in a moment.",
            _ => $"Cummy couldn't send the lick ({outcome}).",
        };
        lickOffer = offer with { Message = why };
        return why;
    }

    /// Cummy's answer to a lick this client sent. Pays once per climax for "licked"; "nothing" allows one more try.
    public void LickAnswered(string name, string world, string result)
    {
        if (lickOffer is not { State: LickState.Waiting } offer || !offer.Partner.Matches(name, world)) return;
        if (result != "licked")
        {
            lickOffer = offer.Retried
                ? offer with { State = LickState.Done, Message = "Nothing came off. Your climax must have landed somewhere else." }
                : offer with { State = LickState.Offered, Retried = true, Message = "Nothing came off there. You can try once more." };
            return;
        }
        var mods = ModifierBook.For(save);
        GiveXp((offer.AtHole ? AftercareRules.CreampieLickXp : AftercareRules.LickXp) * mods.LickXp);
        goals.Record(GoalKind.Licks);
        if (save.BondFormed && string.Equals(save.BondWith, offer.Partner.Key, StringComparison.OrdinalIgnoreCase))
            save.BondSeconds += AftercareRules.LickBondSeconds;
        var done = offer.AtHole ? $"You licked your creampie out of {offer.Partner.Name}." : $"You licked {offer.Partner.Name} clean.";
        lickOffer = offer with { State = LickState.Paid, Message = done };
        host.Notify(done);
        host.SaveProgress();
    }

    /// A part of this character was licked or kissed clean by that player. A recent scene partner's touch is arousing.
    public void CleanedBy(PeerRecord who, DateTime now) => TouchedBy(who, now, AftercareRules.CleanedArousal);

    /// A lick from that player ended one of this character's creampies. Cummy also reports the same lick lowering Groin
    /// or Butt, in either order, so within the 30-second gap this tops a plain lick's arousal up to the lick-out amount
    /// instead of adding both.
    public void LickedOut(PeerRecord who, DateTime now) => TouchedBy(who, now, AftercareRules.LickedOutArousal);

    /// Arousal from a recent scene partner's lick or kiss, at most <paramref name="amount"/> in each 30-second gap.
    private void TouchedBy(PeerRecord who, DateTime now, double amount)
    {
        if (save.Paused || !lastTickWith.TryGetValue(who.Key, out var last) || now - last > AftercareRules.RecentPartner) return;
        var pay = amount;
        if (now - lastCleanedArousal < AftercareRules.CleanedGap)
        {
            pay = amount - lastCleanedPaid;
            if (pay <= 0) return;
            lastCleanedPaid = amount;
        }
        else
        {
            lastCleanedArousal = now;
            lastCleanedPaid = amount;
        }
        save.Arousal = Math.Min(100, save.Arousal + pay);
        dirty = true;
    }

    private double RefractorySeconds(DateTime now, PeerRecord? partner)
    {
        var seconds = balance.RefractorySeconds * ItemBook.RefractoryFactor(save, now) * ModifierBook.For(save).Refractory;
        if (partner is not null && Bonds.LevelWith(partner.Key) >= BondLevels.DevotedLevel) seconds /= 2;
        return Math.Max(balance.MinimumRefractorySeconds, seconds);
    }

    // ── Titles ─────────────────────────────────────────────────────────────────

    /// Why the title can't change right now, or null when it can: not during a scene, nor during refractory. Arousal left
    /// over after a scene doesn't count.
    public string? TitleLock(DateTime now)
    {
        if (sceneStarted is not null) return "Titles can only change outside a scene.";
        if (Arousal.InRefractory(save, now))
            return $"Titles can't change while you recover. {Math.Ceiling((save.RefractoryUntilUtc - now).TotalSeconds):0} seconds left.";
        return null;
    }

    /// Equips a title, or none with null. Null when done, otherwise why not.
    public string? TryEquipTitle(string? id, DateTime now)
    {
        if (save.Setup is null) return "Finish your profile setup first.";
        if (TitleBook.EquipRefusal(save, id) is { } refusal) return refusal;
        if (TitleLock(now) is { } locked) return locked;
        if (save.Titles.Equipped == id) return null;
        save.Titles.Equipped = id;
        host.SaveProgress();
        return null;
    }

    /// Buys a shop title. Null when done, otherwise why not.
    public string? BuyTitle(string id)
    {
        if (save.Setup is null) return "Finish your profile setup first.";
        if (TitleCatalog.Find(id) is not { } title) return "There's no such title.";
        if (TitleBook.BuyRefusal(save, title) is { } refusal) return refusal;
        if (!TitleBook.Buy(save, title)) return "It couldn't be bought.";
        host.Notify($"You bought the {title.Name} title for {title.Price} Sins.");
        host.SaveProgress();
        return null;
    }

    /// Titles unlocked since the last sample, for the popups. Taken by the engine itself; kept for tests.
    public IReadOnlyList<TitleDef> LastUnlockedTitles { get; private set; } = [];

    private void SyncTitles()
    {
        if (save.Setup is null) return;
        LastUnlockedTitles = TitleBook.Sync(save);
        foreach (var title in LastUnlockedTitles)
        {
            host.Notify(title.IsCosmetic ? $"Title unlocked: {title.Name}." : $"Title unlocked: {title.Name}. {title.Effect}.");
            host.Celebrate(new GoalReward(RewardKind.Title, title.Name, "", 0, title.IsCosmetic ? "Just for show" : title.Effect));
        }
        if (LastUnlockedTitles.Count > 0) host.SaveProgress();
    }

    /// `/corr climax`. Null when it fired, otherwise why it didn't.
    public string? TryManualClimax(DateTime now)
    {
        if (save.Paused) return "You're paused.";
        if (!verified || verifiedAct is null || (nearestInRange is null && !solo)) return "There's no verified scene right now.";
        if (Arousal.InRefractory(save, now)) return "You're still recovering from the last one.";
        // A title can lower the threshold for free, and a personality can raise every threshold. A Hair Trigger lowers it
        // further, and is only spent when it's needed.
        var mods = ModifierBook.For(save);
        var free = Math.Min(balance.ManualClimaxMinimum, mods.ManualClimaxMinimum ?? double.MaxValue) + mods.ManualClimaxShift;
        var hairTrigger = ItemBook.IsActive(save, ItemIds.HairTrigger, now);
        var minimum = hairTrigger ? Math.Min(ItemBook.HairTriggerMinimum + mods.ManualClimaxShift, free) : free;
        if (save.Arousal < minimum)
            return $"Your arousal needs to be at least {minimum:0} (it's {Math.Floor(save.Arousal):0}).";
        if (save.Arousal < free) ItemBook.TrySpendUse(save, ItemIds.HairTrigger);
        FireClimax(now, solo ? null : nearestInRange);
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
        PartnerClimaxed?.Invoke(peer);
        if (tally is not null) tally.PartnerClimaxes++;
        GiveXp(balance.ClimaxXp * (ModifierBook.For(save).PartnerClimaxShare ?? balance.PartnerClimaxShare));
        goals.Record(GoalKind.PartnerClimaxes);
        host.Notify($"{peer.Name} climaxed.");
        if (Bonds.PaysWith(peer.Key))
        {
            lastBondedClimax = now;
            CheckTogether(now, peer);
        }
        // Inside, the climax is a creampie; it only lands outside when the hole is blocked (or it isn't inside at all).
        if (!CreampieFor(now, peer)) LayerSelfFor(peer, FullBodyDue(now));
    }

    /// A credited partner's climax with their penis tip inside this character: a creampie through its own Cummy, which
    /// steps Groin or Butt itself. True when that settles the climax; false when it should land outside instead, because
    /// it wasn't inside, Cummy is older than version 6, or the hole is blocked. Only a pussy creampie can lead to
    /// conception. Full Body never applies to an inside climax, and its window keeps its climaxes.
    private bool CreampieFor(DateTime now, PeerRecord peer)
    {
        if (!LayerOnPartnerClimax || creampies is null || BodyOf(peer) is not { } body) return false;
        // A plugin forcing creampies fills in when the bones find no hole; one they do find still wins.
        if ((host.InsideOf(peer, body.Anatomy, save.Anatomy) ?? ForcedHole(body)) is not { } hole) return false;
        var outcome = host.CreampieSelf(peer, hole);
        if (outcome == "blocked") return false;
        // Blacklisted, an unknown hole, no character, or Cummy unusable: nothing lands, and the credit still stands.
        if (outcome is not ("creampied" or "refreshed")) return true;

        var mods = ModifierBook.For(save);
        GiveXp(CreampieRules.Xp * mods.CreampieXp);
        goals.Record(GoalKind.Creampies);
        save.CreampiePartners.Add(BondHashes.Of(peer.Key));
        if (outcome == "refreshed")
        {
            save.CreampieRefreshes++;
            if (mods.RefreshArousal > 0) save.Arousal = Math.Min(100, save.Arousal + mods.RefreshArousal);
        }
        Crave(lastTickUtc, CravingKind.Creampie);
        Crave(lastTickUtc, CravingKind.Layer);
        host.Notify(hole == CreampieAim.Pussy ? $"{peer.Name} came inside your pussy." : $"{peer.Name} came inside your ass.");
        // Cum Magnet: one more layer outside, on the nearest part the creampie doesn't already step.
        if (ItemBook.IsActive(save, ItemIds.CumMagnet, DateTime.MaxValue) && LayerOutside(peer, except: [CummyParts.Groin, CummyParts.Butt]))
            ItemBook.TrySpendUse(save, ItemIds.CumMagnet);
        if (hole == CreampieAim.Pussy) RollConception(now, peer);
        dirty = true;
        return true;
    }

    /// One aimed outside layer from a partner, leaving out <paramref name="except"/>. True when one landed.
    private bool LayerOutside(PeerRecord peer, IReadOnlyCollection<string> except)
    {
        foreach (var part in Unblocked(AimOrder(peer)).Where(p => !except.Contains(p)))
        {
            var outcome = host.LayerSelf(peer, part);
            if (outcome is "layered" or "advanced")
            {
                PayLayer();
                return true;
            }
            if (outcome is not ("blocked" or "full")) return false;
        }
        return false;
    }

    /// The giver's aim onto this character, or a shuffled order when the bones can't be read.
    private string[] AimOrder(PeerRecord peer)
    {
        if (host.AimParts(peer, BodyOf(peer)?.Anatomy ?? Anatomy.NotSet) is { Count: > 0 } aimed) return aimed.ToArray();
        var parts = CummyParts.All.ToArray();
        random.Shuffle(parts);
        return parts;
    }

    private void PayLayer()
    {
        GiveXp(balance.LayerXp);
        goals.Record(GoalKind.LayersReceived);
        Crave(lastTickUtc, CravingKind.Layer);
    }

    /// Layers this character through its own Cummy for a partner's credited climax: a random part, trying others while
    /// Cummy says the part is blocked. Only for partners whose client layers itself too (they publish the body tag); an
    /// older client still sends Cummy's own tell instead.
    /// The shot goes to the part nearest the giver's genitals, then the next nearest while a part is full or blocked.
    /// Random only when the bones can't be read. An outside shot never leads to conception, wherever it lands.
    private void LayerSelfFor(PeerRecord peer, bool fullBody = false)
    {
        if (!LayerOnPartnerClimax || BodyOf(peer) is null) return;
        // A group's third climax in a minute coats every unblocked part one stage. Anything but a landed layer falls back
        // to the usual aim.
        if (fullBody && host.LayerSelf(peer, CummyParts.FullBody) is "layered" or "advanced")
        {
            groupClimaxes.Clear();
            PayLayer();
            goals.Record(GoalKind.FullBodyLayers);
            host.Notify("You were drenched head to toe.");
            return;
        }
        var wanted = ItemBook.IsActive(save, ItemIds.CumMagnet, DateTime.MaxValue) ? 2 : 1;
        var landed = 0;
        foreach (var part in Unblocked(AimOrder(peer)))
        {
            var outcome = host.LayerSelf(peer, part);
            if (outcome is "layered" or "advanced")
            {
                PayLayer();
                if (++landed >= wanted) break;
                continue;
            }
            // A full part still took the shot, so try another for the layer.
            if (outcome is not ("blocked" or "full")) break;
        }
        if (landed > 0 && wanted > 1) ItemBook.TrySpendUse(save, ItemIds.CumMagnet);
    }


    // ── Pregnancy ──────────────────────────────────────────────────────────────

    private void UpdatePregnancy(DateTime now)
    {
        switch (PregnancyBook.Update(save.Pregnancy, now))
        {
            case PregnancyEvent.Birth:
                var birthSins = (int)Math.Round(Sins.Birth * ModifierBook.For(save).BirthSins);
                Sins.Earn(save.Wallet, birthSins);
                var father = save.Pregnancy.BirthLog.LastOrDefault()?.FatherKey is { Length: > 0 } key ? $" {key.Split('@')[0]}'s child" : "";
                host.Notify($"You gave birth{(father.Length > 0 ? " to" + father : "")}. +{birthSins} Sins. Rest for two days before your cycle starts again.");
                host.SaveProgress();
                var (fatherName, fatherWorld) = ApiSnapshots.Split(save.Pregnancy.BirthLog.LastOrDefault()?.FatherKey);
                Born?.Invoke(fatherName, fatherWorld);
                break;
            case PregnancyEvent.Revealed:
                host.Notify($"Your belly is starting to show. You're pregnant with {FatherName()}'s child.");
                host.SaveProgress();
                RaiseConceived();
                break;
            case PregnancyEvent.RecoveryEnded:
                if (save.Pregnancy.CanCarry) host.Notify("You've recovered, and your cycle starts again at day 1.");
                host.SaveProgress();
                RecoveryEnded?.Invoke();
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
        TryConceive(now, peer, ConceptionChanceFor(now, peer, body), ApiOutcomes.SceneSource);
    }

    /// The chance this partner's breeding has now. Their title (Sire) and traits (Virile) raise it when their tags are
    /// known; their Virility Tonic counts through their body tag, which an untagged breeder doesn't have.
    /// <paramref name="callerFactor"/> is a plugin's chance factor; <paramref name="shown"/> gives the chance a secret
    /// pregnancy would have had.
    private double ConceptionChanceFor(DateTime now, PeerRecord peer, BodyTag? body, double callerFactor = 1, bool shown = false)
    {
        var partnerTraits = PersonaOf(peer)?.KnownTraits.Select(t => t.Effects) ?? [];
        var partner = Modifiers.Combine(partnerTraits.Prepend(TitleCatalog.Find(TitleOf(peer))?.Bonus ?? Modifiers.None));
        var titles = ModifierBook.For(save).ConceptionOwn * partner.ConceptionPartner * callerFactor;
        var blocked = save.Paused || save.Hidden;
        return shown
            ? PregnancyBook.ShownConceptionChance(save, now, body?.Virile == true, Bonds.LevelWith(peer.Key), blocked, titles)
            : PregnancyBook.ConceptionChance(save, now, body?.Virile == true, Bonds.LevelWith(peer.Key), blocked, titles);
    }

    /// Rolls once. A conception is secret: the partner is the father and the source is kept, but nobody is told until a
    /// test or day 5 confirms it.
    private bool TryConceive(DateTime now, PeerRecord peer, double chance, string source)
    {
        if (chance <= 0 || random.NextDouble() >= chance) return false;
        PregnancyBook.Conceive(save.Pregnancy, now, peer.Key, source);
        host.SaveProgress();
        return true;
    }

    /// The father's name, from the saved `Name@World`.
    private string FatherName() => ApiSnapshots.Split(save.Pregnancy.FatherKey).Name is { Length: > 0 } name ? name : "someone";

    /// Conceived, at confirmation, with the saved father and source.
    private void RaiseConceived()
    {
        var (name, world) = ApiSnapshots.Split(save.Pregnancy.FatherKey);
        Conceived?.Invoke(name, world, save.Pregnancy.ConceivedSource ?? ApiOutcomes.SceneSource);
    }

    /// A Pregnancy Test. Always used up unless the pregnancy is already known.
    private string? TakeTest(DateTime now)
    {
        switch (PregnancyBook.Test(save.Pregnancy, now))
        {
            case PregnancyBook.TestResult.AlreadyKnown:
                return "You already know you're pregnant.";
            case PregnancyBook.TestResult.Positive:
                host.Notify($"Positive. You're pregnant with {FatherName()}'s child.");
                RaiseConceived();
                return null;
            default:
                host.Notify("Negative. You aren't pregnant.");
                return null;
        }
    }

    private void CravingsFromTick(DateTime now, ActEntry act, PeerRecord? partner)
    {
        var p = save.Pregnancy;
        if (!PregnancyBook.IsConfirmed(p) || p.Craving is not { } craving || p.CravingMet) return;
        p.CravingSeconds += balance.TickSeconds;
        var newToday = !save.Goals.DaySeen.Contains($"{GoalKind.DistinctAnimations}:{act.Key}");
        var met = craving switch
        {
            CravingKind.BondedScene => partner is not null && Bonds.PaysWith(partner.Key),
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
        var sins = (int)Math.Round(Sins.Craving * ModifierBook.For(save).CravingSins);
        Sins.Earn(save.Wallet, sins);
        goals.Record(GoalKind.CravingsSatisfied);
        save.Arousal = Math.Min(100, save.Arousal + CravingBook.SatisfiedArousal);
        save.ArousalAnchorUtc = now;
        host.Notify($"Your craving is satisfied. +{sins} Sins.");
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
        // The persona is locked once chosen, so it's only taken when none is set yet.
        var choosing = Welcome.NeedsPersona(save);
        if (choosing && PersonaBook.Refusal(choices.Persona?.Personality, choices.Persona?.Traits ?? []) is { } personaRefusal)
            return personaRefusal;

        if (save.Setup is null) CompleteSetup(choices.Setup!, now);
        if (choosing)
        {
            save.Setup!.Personality = choices.Persona!.Personality;
            save.Setup.Traits = [.. choices.Persona.Traits];
        }
        save.Anatomy = choices.Anatomy;
        if (p.On)
        {
            // Roles can't change while pregnant, so an ongoing pregnancy keeps its roles.
            if (!PregnancyBook.IsConfirmed(save.Pregnancy)) PregnancyBook.Enable(save.Pregnancy, p.Carry, p.Breed, p.StartDay, now);
            save.Pregnancy.Enabled = true;
            save.Pregnancy.ChanceFactor = Math.Clamp(p.ChanceFactor, PregnancyBook.MinChanceFactor, PregnancyBook.MaxChanceFactor);
        }
        else if (save.Pregnancy.Enabled) PregnancyBook.Disable(save.Pregnancy);

        save.WelcomeVersion = Welcome.CurrentVersion;
        host.Notify("You're all set. Everything here can be changed later in Settings, except your locked profile.");
        host.SaveProgress();
        return null;
    }

    /// Profile: climax automatically at 100, or only by hand. Can change any time.
    public void SetAutoClimax(bool auto)
    {
        if (save.Setup is not { } setup || setup.AutoClimax == auto) return;
        setup.AutoClimax = auto;
        host.SaveProgress();
    }

    /// Profile: how fast arousal builds, within the setup's range. Can change any time.
    public void SetArousalGain(double gain)
    {
        if (save.Setup is not { } setup) return;
        setup.ArousalGain = Math.Round(Math.Clamp(gain, ProfileSetup.MinArousalGain, ProfileSetup.MaxArousalGain), 2);
        host.SaveProgress();
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
        // The test and the pill say what happened themselves.
        if (id is not (ItemIds.PregnancyTest or ItemIds.MorningAfter)) host.Notify($"You used {ItemCatalog.Find(id)!.Name}.");
        goals.Record(GoalKind.ItemsUsed);
        host.SaveProgress();
        return null;
    }

    /// A Do Not Disturb Sign: 20 more solo minutes today. Always useful, so never refused.
    private string? AddSoloTime(DateTime now)
    {
        SoloBook.Rollover(save, now);
        save.SoloBonusSecondsToday += SoloBook.SignSeconds;
        return null;
    }

    private string? Said(string message)
    {
        host.Notify(message);
        return null;
    }

    /// Instant items act on pregnancy or solo time. Null when it applied, otherwise why not.
    private string? UseInstant(ItemDef item, DateTime now) => item.Id switch
    {
        ItemIds.PregnancyTest => TakeTest(now),
        ItemIds.MorningAfter => PregnancyBook.MorningAfter(save.Pregnancy, now) is { } tooLate ? tooLate : Said(PregnancyBook.PillTaken),
        ItemIds.HormoneTea => PregnancyBook.HormoneTea(save.Pregnancy, now),
        ItemIds.SwiftLabour => PregnancyBook.SwiftLabour(save.Pregnancy),
        ItemIds.CravingTreat => !PregnancyBook.IsConfirmed(save.Pregnancy) ? PregnancyBook.NeedsConfirmed
            : SatisfyCraving(now) ? null : "You have no craving to satisfy right now.",
        ItemIds.DoNotDisturb => AddSoloTime(now),
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
