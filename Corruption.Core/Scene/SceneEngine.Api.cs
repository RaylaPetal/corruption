namespace Corruption.Core;

/// What other plugins can see happen, and do with the player's consent, through Corruption's plugin API. Every write goes
/// through the same rules the game uses, and none gives XP, Sins or items.
public sealed partial class SceneEngine
{
    // ── Events ─────────────────────────────────────────────────────────────────

    /// The local character climaxed, after its effects are applied.
    public event Action? Climaxed;

    /// A partner's climax was credited.
    public event Action<PeerRecord>? PartnerClimaxed;

    /// A verified scene started: the partner (null alone), and whether it's solo.
    public event Action<PeerRecord?, bool>? SceneStarted;

    /// A verified scene ended: its verified seconds and the XP it paid.
    public event Action<double, double>? SceneFinished;

    public event Action<int>? LeveledUp;
    public event Action<CyclePhase>? CyclePhaseChanged;

    /// (father name, father world, source): source is "scene" or the calling plugin's name.
    public event Action<string, string, string>? Conceived;

    public event Action<PregnancyStage>? TrimesterChanged;

    /// (father name, father world).
    public event Action<string, string>? Born;

    public event Action? RecoveryEnded;

    /// The shown whole-number arousal changed since the last sample.
    public event Action<int>? ArousalShown;

    private CyclePhase? lastPhase;
    private PregnancyStage lastStage;
    private int lastShownArousal = -1;
    private bool transitionsSeen;

    /// Raised once per sample, so each is at most once a second. The first sample only records where things stand.
    private void NoteApiTransitions(DateTime now)
    {
        if (save.Setup is null) return;
        var p = save.Pregnancy;
        CyclePhase? phase = PregnancyBook.HasCycle(p, now) ? CycleClock.PhaseOf(CycleClock.DayAt(p.CycleStartUtc, now)).Phase : null;
        var stage = PregnancyBook.StageOf(p, now);
        var shown = PlateComposer.ArousalNumber(save.Arousal);
        if (transitionsSeen)
        {
            if (phase is { } next && phase != lastPhase) CyclePhaseChanged?.Invoke(next);
            if (stage != lastStage && stage is PregnancyStage.First or PregnancyStage.Second or PregnancyStage.Third)
                TrimesterChanged?.Invoke(stage);
            if (shown != lastShownArousal) ArousalShown?.Invoke(shown);
        }
        transitionsSeen = true;
        lastPhase = phase;
        lastStage = stage;
        lastShownArousal = shown;
    }

    // ── Reads ──────────────────────────────────────────────────────────────────

    /// This character as `Name@World`, from the last sample; null before one or when logged out.
    public string? SelfKey => selfKey;

    // ── Writes ─────────────────────────────────────────────────────────────────

    private readonly ArousalAllowance arousalAllowance = new();
    private readonly Dictionary<string, DateTime> lastPluginBreed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PluginCall> pluginCalls = new(StringComparer.OrdinalIgnoreCase);

    /// Each plugin's last write this session, newest first, for Settings.
    public IReadOnlyList<PluginCall> PluginCalls => pluginCalls.Values.OrderByDescending(c => c.AtUtc).ToList();

    private string Logged(DateTime now, string plugin, string call, string outcome)
    {
        var name = string.IsNullOrWhiteSpace(plugin) ? "Unnamed plugin" : plugin.Trim();
        pluginCalls[name] = new PluginCall(name, call, outcome, now);
        return outcome;
    }

    /// Why a breeding by this player can't happen now, or null when it can.
    private string? BreedRefusal(DateTime now, PeerRecord breeder, out BodyTag? body)
    {
        body = BodyOf(breeder);
        var p = save.Pregnancy;
        if (save.Setup is null) return ApiOutcomes.NotSetUp;
        if (selfKey is null) return ApiOutcomes.NoCharacter;
        if (!save.PluginConsent.Breed) return ApiOutcomes.NotAllowed;
        if (!p.Enabled) return ApiOutcomes.PregnancyOff;
        if (!p.CanCarry) return ApiOutcomes.CantCarry;
        if (PregnancyBook.IsPregnant(p)) return ApiOutcomes.Pregnant;
        if (PregnancyBook.IsRecovering(p, now)) return ApiOutcomes.Recovering;
        if (save.Paused) return ApiOutcomes.Paused;
        if (save.Hidden) return ApiOutcomes.Hidden;
        if (ItemBook.PreventsConception(save, now)) return ApiOutcomes.Protected;
        if (body is { CanBreed: false }) return ApiOutcomes.NotABreeder;
        if (lastPluginBreed.TryGetValue(breeder.Key, out var last) && now - last < PluginLimits.BreedGap) return ApiOutcomes.TooSoon;
        return null;
    }

    /// One conception roll as if bred by that player, under the usual odds. A breeder whose body tag is seen needs the
    /// breeding role; one without Corruption breeds with no virility factor.
    public string BreedFromPlugin(DateTime now, PeerRecord breeder, string plugin)
    {
        if (BreedRefusal(now, breeder, out var body) is { } refusal) return Logged(now, plugin, "Breed", refusal);
        lastPluginBreed[breeder.Key] = now;
        var source = string.IsNullOrWhiteSpace(plugin) ? "Unnamed plugin" : plugin.Trim();
        var conceived = TryConceive(now, breeder, ConceptionChanceFor(now, breeder, body), source);
        return Logged(now, plugin, "Breed", conceived ? ApiOutcomes.Conceived : ApiOutcomes.NotConceived);
    }

    /// The chance a Breed by that player would have now, without rolling; 0 when Breed would refuse.
    public double BreedChanceFor(DateTime now, PeerRecord breeder)
        => BreedRefusal(now, breeder, out var body) is null ? ConceptionChanceFor(now, breeder, body) : 0;

    /// Changes arousal by up to 25 a call and 60 a minute for this plugin. A rise isn't taken during refractory. Reaching
    /// 100 behaves as in a scene: automatic climax fires on the scene's next tick.
    public string AddArousalFromPlugin(DateTime now, double amount, string plugin)
    {
        string outcome;
        if (save.Setup is null) outcome = ApiOutcomes.NotSetUp;
        else if (!save.PluginConsent.Arousal) outcome = ApiOutcomes.NotAllowed;
        else if (save.Paused) outcome = ApiOutcomes.Paused;
        else if (amount > 0 && Arousal.InRefractory(save, now)) outcome = ApiOutcomes.Refractory;
        else
        {
            var key = string.IsNullOrWhiteSpace(plugin) ? "Unnamed plugin" : plugin.Trim();
            var allowed = arousalAllowance.Allow(key, amount, now);
            var before = save.Arousal;
            save.Arousal = Math.Clamp(before + allowed, 0, 100);
            if (save.Arousal > before) save.ArousalAnchorUtc = now;
            arousalAllowance.Spend(key, save.Arousal - before, now);
            dirty = true;
            outcome = Math.Abs(allowed) < Math.Abs(amount) ? ApiOutcomes.Limited : ApiOutcomes.Ok;
        }
        return Logged(now, plugin, "AddArousal", outcome);
    }

    /// A climax under the same rules and with the same effects as /corr climax, which needs a verified scene.
    public string ClimaxFromPlugin(DateTime now, string plugin)
    {
        string outcome;
        if (save.Setup is null) outcome = ApiOutcomes.NotSetUp;
        else if (!save.PluginConsent.Climax) outcome = ApiOutcomes.NotAllowed;
        else if (save.Paused) outcome = ApiOutcomes.Paused;
        else if (!verified || verifiedAct is null || (nearestInRange is null && !solo)) outcome = ApiOutcomes.NoScene;
        else if (Arousal.InRefractory(save, now)) outcome = ApiOutcomes.Refractory;
        else outcome = TryManualClimax(now) is null ? ApiOutcomes.Climaxed : ApiOutcomes.TooLow;
        return Logged(now, plugin, "Climax", outcome);
    }
}
