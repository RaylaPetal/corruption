// Corruption plugin API client (API version 1). Copy this file into your Dalamud plugin to read Corruption's state, hear
// its events and, where the player allows it, breed, change arousal or ask for a climax, with types and named outcomes
// instead of raw call-gate strings. Corruption's README ("Plugin API") documents every gate.
//
//     var corruption = new CorruptionIpcClient.CorruptionIpc(PluginInterface);
//     corruption.ArousalChanged += arousal => { /* start a breeding timer once it rises */ };
//     corruption.PartnerClimax += (name, world) => { /* the bull finished */ };
//     if (corruption.GetPregnancy() is { Ready: true, Stage: "none" })
//         var outcome = corruption.Breed("Bull Name", "World", "My Plugin");   // "conceived", "not-conceived", ...
//     ...
//     corruption.Dispose();   // in your plugin's Dispose
//
// Call everything from the framework thread (Framework.Update, RunOnFrameworkThread, or your UI's Draw); other threads
// get "wrong-thread" or an empty value. While Corruption isn't loaded, calls return "unavailable" or an empty value
// instead of throwing. Event subscriptions survive Corruption reloads: subscribe once, here, and watch Ready.
// Enums arrive as lower-case strings, and "no time" is default(DateTime). Times are UTC.

using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;

namespace CorruptionIpcClient;

/// The strings Corruption returns and raises. Compare against these rather than literals.
public static class CorruptionStrings
{
    /// Client-side only: Corruption isn't loaded.
    public const string Unavailable = "unavailable";

    public const string WrongThread = "wrong-thread";
    public const string Error = "error";
    public const string NotAllowed = "not-allowed";
    public const string NotSetUp = "not-set-up";
    public const string NoCharacter = "no-character";
    public const string Paused = "paused";

    // Breed
    public const string Conceived = "conceived";
    public const string NotConceived = "not-conceived";
    public const string PregnancyOff = "pregnancy-off";
    public const string CantCarry = "cant-carry";
    public const string Pregnant = "pregnant";
    public const string Recovering = "recovering";
    public const string Protected = "protected";
    public const string Hidden = "hidden";
    public const string NotABreeder = "not-a-breeder";
    public const string TooSoon = "too-soon";

    // AddArousal
    public const string Ok = "ok";
    public const string Limited = "limited";
    public const string Refractory = "refractory";

    // Climax
    public const string Climaxed = "climaxed";
    public const string TooLow = "too-low";
    public const string NoScene = "no-scene";

    /// OnConceived's source for a conception in a scene; otherwise it's the breeding plugin's name.
    public const string SceneSource = "scene";

    // Scene states: GetScene().State
    public const string Idle = "idle";
    public const string Waiting = "waiting";
    public const string Earning = "earning";

    // Cycle phases: GetCycle().Phase and OnCyclePhaseChanged
    public const string Period = "period";
    public const string Follicular = "follicular";
    public const string Fertile = "fertile";
    public const string Ovulation = "ovulation";
    public const string Luteal = "luteal";

    // Pregnancy stages: GetPregnancy().Stage, GetNearby() and OnTrimesterChanged
    public const string None = "none";
    public const string First = "first";
    public const string Second = "second";
    public const string Third = "third";
}

/// The local character. Ready is false before setup or while logged out, and every other field is then empty.
public sealed record CorruptionProfile(bool Ready, string Name, string World, int Level, string Tier, double Xp, double XpToNext,
    int Prestige, string Title, string Personality, string Anatomy, long Sins, int StreakDays, bool Paused, bool Hidden);

/// Arousal as shown (0 to 100), refractory left, at 100 and held, and "auto" or "manual" climaxes.
public sealed record CorruptionArousal(bool Ready, int Arousal, double RefractorySeconds, bool Edging, string ClimaxMode);

/// The scene: idle, waiting, earning or paused; the partner (empty alone or with none); watchers; seconds since it began.
public sealed record CorruptionScene(bool Ready, string State, bool Solo, string PartnerName, string PartnerWorld, int Watchers, double Seconds);

/// The bond or bond request; Level is 0 until formed.
public sealed record CorruptionBond(bool Ready, string Name, string World, bool Formed, int Level);

/// The fertility cycle. Day is 0 and Chance 0 without a running cycle (pregnancy off, can't carry, pregnant or
/// recovering). Chance is from this character's side alone; BreedChance adds a breeder's factors.
public sealed record CorruptionCycle(bool Ready, bool Enabled, bool CanCarry, bool CanBreed, bool Running, int Day, string Phase,
    double Chance, bool Protected, DateTime NextPhaseUtc);

/// The pregnancy: Stage is none, first, second, third or recovering. Craving is empty unless pregnant.
public sealed record CorruptionPregnancy(bool Ready, string Stage, double DaysPregnant, DateTime ConceivedUtc, DateTime DueUtc,
    string FatherName, string FatherWorld, DateTime RecoveryUntilUtc, string Craving, bool CravingMet, bool Unsatisfied,
    int Conceptions, int Births, int Fathered);

public sealed record CorruptionBirth(DateTime BornUtc, DateTime ConceivedUtc, string FatherName, string FatherWorld);

/// A nearby player with Corruption: only what their tags broadcast. The body fields need their body tag (HasBodyTag).
public sealed record CorruptionNearbyPlayer(string Name, string World, double Distance, int Level, int Arousal, bool InAct,
    int ClimaxCount, bool HasBodyTag, bool CanCarry, bool CanBreed, bool Virile, string Anatomy, string Stage);

/// Typed access to Corruption's plugin API.
public sealed class CorruptionIpc : IDisposable
{
    /// The API version this file was written for.
    public const int ClientVersion = 1;

    public static readonly CorruptionProfile NoProfile = new(false, "", "", 0, "", 0, 0, 0, "", "", "", 0, 0, false, false);
    public static readonly CorruptionArousal NoArousal = new(false, 0, 0, false, "");
    public static readonly CorruptionScene NoScene = new(false, "", false, "", "", 0, 0);
    public static readonly CorruptionBond NoBond = new(false, "", "", false, 0);
    public static readonly CorruptionCycle NoCycle = new(false, false, false, false, false, 0, "", 0, false, default);
    public static readonly CorruptionPregnancy NoPregnancy = new(false, "", 0, default, default, "", "", default, "", false, false, 0, 0, 0);

    /// Corruption loaded or reloaded: refresh what you read from it.
    public event Action? Ready;

    /// Corruption is going away.
    public event Action? Disposing;

    /// The local character climaxed.
    public event Action? Climax;

    /// (name, world): a scene partner's climax was credited.
    public event Action<string, string>? PartnerClimax;

    /// (partner name, partner world, solo): a verified scene started. The partner is empty alone.
    public event Action<string, string, bool>? SceneStarted;

    /// (verified seconds, XP): a verified scene ended.
    public event Action<double, double>? SceneEnded;

    public event Action<int>? LevelUp;

    /// The new phase: period, follicular, fertile, ovulation or luteal.
    public event Action<string>? CyclePhaseChanged;

    /// (father name, father world, source): source is "scene" or the plugin that called Breed.
    public event Action<string, string, string>? Conceived;

    /// first, second or third.
    public event Action<string>? TrimesterChanged;

    /// (father name, father world).
    public event Action<string, string>? Birth;

    public event Action? RecoveryEnded;

    /// The shown arousal, 0 to 100, when its whole number changes; at most once a second.
    public event Action<int>? ArousalChanged;

    /// Something any read returns changed; at most once a second. Ticking durations alone don't count.
    public event Action? StateChanged;

    private readonly ICallGateSubscriber<int> apiVersion;
    private readonly ICallGateSubscriber<object> ready;
    private readonly ICallGateSubscriber<object> disposing;
    private readonly ICallGateSubscriber<(bool, string, string, int, string, double, double, int, string, string, string, long, int, bool, bool)> getProfile;
    private readonly ICallGateSubscriber<(bool, int, double, bool, string)> getArousal;
    private readonly ICallGateSubscriber<(bool, string, bool, string, string, int, double)> getScene;
    private readonly ICallGateSubscriber<(bool, string, string, bool, int)> getBond;
    private readonly ICallGateSubscriber<(bool, bool, bool, bool, bool, int, string, double, bool, DateTime)> getCycle;
    private readonly ICallGateSubscriber<(bool, string, double, DateTime, DateTime, string, string, DateTime, string, bool, bool, int, int, int)> getPregnancy;
    private readonly ICallGateSubscriber<List<(DateTime, DateTime, string, string)>> getBirths;
    private readonly ICallGateSubscriber<List<(string, string, double, int, int, bool, int, bool, bool, bool, bool, string, string)>> getNearby;
    private readonly ICallGateSubscriber<string, string, double> breedChance;
    private readonly ICallGateSubscriber<string, string, string, string> breed;
    private readonly ICallGateSubscriber<double, string, string> addArousal;
    private readonly ICallGateSubscriber<string, string> climax;
    private readonly ICallGateSubscriber<object> onClimax;
    private readonly ICallGateSubscriber<string, string, object> onPartnerClimax;
    private readonly ICallGateSubscriber<string, string, bool, object> onSceneStarted;
    private readonly ICallGateSubscriber<double, double, object> onSceneEnded;
    private readonly ICallGateSubscriber<int, object> onLevelUp;
    private readonly ICallGateSubscriber<string, object> onCyclePhaseChanged;
    private readonly ICallGateSubscriber<string, string, string, object> onConceived;
    private readonly ICallGateSubscriber<string, object> onTrimesterChanged;
    private readonly ICallGateSubscriber<string, string, object> onBirth;
    private readonly ICallGateSubscriber<object> onRecoveryEnded;
    private readonly ICallGateSubscriber<int, object> onArousalChanged;
    private readonly ICallGateSubscriber<object> onStateChanged;

    public CorruptionIpc(IDalamudPluginInterface pi)
    {
        apiVersion = pi.GetIpcSubscriber<int>("Corruption.ApiVersion");
        ready = pi.GetIpcSubscriber<object>("Corruption.Ready");
        disposing = pi.GetIpcSubscriber<object>("Corruption.Disposing");
        getProfile = pi.GetIpcSubscriber<(bool, string, string, int, string, double, double, int, string, string, string, long, int, bool, bool)>("Corruption.GetProfile");
        getArousal = pi.GetIpcSubscriber<(bool, int, double, bool, string)>("Corruption.GetArousal");
        getScene = pi.GetIpcSubscriber<(bool, string, bool, string, string, int, double)>("Corruption.GetScene");
        getBond = pi.GetIpcSubscriber<(bool, string, string, bool, int)>("Corruption.GetBond");
        getCycle = pi.GetIpcSubscriber<(bool, bool, bool, bool, bool, int, string, double, bool, DateTime)>("Corruption.GetCycle");
        getPregnancy = pi.GetIpcSubscriber<(bool, string, double, DateTime, DateTime, string, string, DateTime, string, bool, bool, int, int, int)>("Corruption.GetPregnancy");
        getBirths = pi.GetIpcSubscriber<List<(DateTime, DateTime, string, string)>>("Corruption.GetBirths");
        getNearby = pi.GetIpcSubscriber<List<(string, string, double, int, int, bool, int, bool, bool, bool, bool, string, string)>>("Corruption.GetNearby");
        breedChance = pi.GetIpcSubscriber<string, string, double>("Corruption.BreedChance");
        breed = pi.GetIpcSubscriber<string, string, string, string>("Corruption.Breed");
        addArousal = pi.GetIpcSubscriber<double, string, string>("Corruption.AddArousal");
        climax = pi.GetIpcSubscriber<string, string>("Corruption.Climax");
        onClimax = pi.GetIpcSubscriber<object>("Corruption.OnClimax");
        onPartnerClimax = pi.GetIpcSubscriber<string, string, object>("Corruption.OnPartnerClimax");
        onSceneStarted = pi.GetIpcSubscriber<string, string, bool, object>("Corruption.OnSceneStarted");
        onSceneEnded = pi.GetIpcSubscriber<double, double, object>("Corruption.OnSceneEnded");
        onLevelUp = pi.GetIpcSubscriber<int, object>("Corruption.OnLevelUp");
        onCyclePhaseChanged = pi.GetIpcSubscriber<string, object>("Corruption.OnCyclePhaseChanged");
        onConceived = pi.GetIpcSubscriber<string, string, string, object>("Corruption.OnConceived");
        onTrimesterChanged = pi.GetIpcSubscriber<string, object>("Corruption.OnTrimesterChanged");
        onBirth = pi.GetIpcSubscriber<string, string, object>("Corruption.OnBirth");
        onRecoveryEnded = pi.GetIpcSubscriber<object>("Corruption.OnRecoveryEnded");
        onArousalChanged = pi.GetIpcSubscriber<int, object>("Corruption.OnArousalChanged");
        onStateChanged = pi.GetIpcSubscriber<object>("Corruption.OnStateChanged");

        ready.Subscribe(OnReady);
        disposing.Subscribe(OnDisposing);
        onClimax.Subscribe(OnClimax);
        onPartnerClimax.Subscribe(OnPartnerClimax);
        onSceneStarted.Subscribe(OnSceneStarted);
        onSceneEnded.Subscribe(OnSceneEnded);
        onLevelUp.Subscribe(OnLevelUp);
        onCyclePhaseChanged.Subscribe(OnCyclePhaseChanged);
        onConceived.Subscribe(OnConceived);
        onTrimesterChanged.Subscribe(OnTrimesterChanged);
        onBirth.Subscribe(OnBirth);
        onRecoveryEnded.Subscribe(OnRecoveryEnded);
        onArousalChanged.Subscribe(OnArousalChanged);
        onStateChanged.Subscribe(OnStateChanged);
    }

    public void Dispose()
    {
        ready.Unsubscribe(OnReady);
        disposing.Unsubscribe(OnDisposing);
        onClimax.Unsubscribe(OnClimax);
        onPartnerClimax.Unsubscribe(OnPartnerClimax);
        onSceneStarted.Unsubscribe(OnSceneStarted);
        onSceneEnded.Unsubscribe(OnSceneEnded);
        onLevelUp.Unsubscribe(OnLevelUp);
        onCyclePhaseChanged.Unsubscribe(OnCyclePhaseChanged);
        onConceived.Unsubscribe(OnConceived);
        onTrimesterChanged.Unsubscribe(OnTrimesterChanged);
        onBirth.Unsubscribe(OnBirth);
        onRecoveryEnded.Unsubscribe(OnRecoveryEnded);
        onArousalChanged.Unsubscribe(OnArousalChanged);
        onStateChanged.Unsubscribe(OnStateChanged);
    }

    // Named handlers, so Dispose unsubscribes exactly what the constructor subscribed.
    private void OnReady() => Ready?.Invoke();
    private void OnDisposing() => Disposing?.Invoke();
    private void OnClimax() => Climax?.Invoke();
    private void OnPartnerClimax(string name, string world) => PartnerClimax?.Invoke(name, world);
    private void OnSceneStarted(string name, string world, bool solo) => SceneStarted?.Invoke(name, world, solo);
    private void OnSceneEnded(double seconds, double xp) => SceneEnded?.Invoke(seconds, xp);
    private void OnLevelUp(int level) => LevelUp?.Invoke(level);
    private void OnCyclePhaseChanged(string phase) => CyclePhaseChanged?.Invoke(phase);
    private void OnConceived(string name, string world, string source) => Conceived?.Invoke(name, world, source);
    private void OnTrimesterChanged(string stage) => TrimesterChanged?.Invoke(stage);
    private void OnBirth(string name, string world) => Birth?.Invoke(name, world);
    private void OnRecoveryEnded() => RecoveryEnded?.Invoke();
    private void OnArousalChanged(int arousal) => ArousalChanged?.Invoke(arousal);
    private void OnStateChanged() => StateChanged?.Invoke();

    private static T Call<T>(Func<T> call, T unavailable)
    {
        try { return call(); }
        catch (IpcNotReadyError) { return unavailable; }
        catch (IpcTypeMismatchError) { return unavailable; }
    }

    /// Corruption's API version, or 0 when it isn't loaded.
    public int ApiVersion() => Call(() => apiVersion.InvokeFunc(), 0);

    public bool IsAvailable => ApiVersion() >= ClientVersion;

    // ── Reads ────────────────────────────────────────────────────────────────

    public CorruptionProfile GetProfile() => Call(() =>
    {
        var p = getProfile.InvokeFunc();
        return new CorruptionProfile(p.Item1, p.Item2, p.Item3, p.Item4, p.Item5, p.Item6, p.Item7, p.Item8, p.Item9, p.Item10, p.Item11,
            p.Item12, p.Item13, p.Item14, p.Item15);
    }, NoProfile);

    public CorruptionArousal GetArousal() => Call(() =>
    {
        var a = getArousal.InvokeFunc();
        return new CorruptionArousal(a.Item1, a.Item2, a.Item3, a.Item4, a.Item5);
    }, NoArousal);

    public CorruptionScene GetScene() => Call(() =>
    {
        var s = getScene.InvokeFunc();
        return new CorruptionScene(s.Item1, s.Item2, s.Item3, s.Item4, s.Item5, s.Item6, s.Item7);
    }, NoScene);

    public CorruptionBond GetBond() => Call(() =>
    {
        var b = getBond.InvokeFunc();
        return new CorruptionBond(b.Item1, b.Item2, b.Item3, b.Item4, b.Item5);
    }, NoBond);

    public CorruptionCycle GetCycle() => Call(() =>
    {
        var c = getCycle.InvokeFunc();
        return new CorruptionCycle(c.Item1, c.Item2, c.Item3, c.Item4, c.Item5, c.Item6, c.Item7, c.Item8, c.Item9, c.Item10);
    }, NoCycle);

    public CorruptionPregnancy GetPregnancy() => Call(() =>
    {
        var p = getPregnancy.InvokeFunc();
        return new CorruptionPregnancy(p.Item1, p.Item2, p.Item3, p.Item4, p.Item5, p.Item6, p.Item7, p.Item8, p.Item9, p.Item10, p.Item11,
            p.Item12, p.Item13, p.Item14);
    }, NoPregnancy);

    public List<CorruptionBirth> GetBirths() => Call(() => getBirths.InvokeFunc()
        .Select(b => new CorruptionBirth(b.Item1, b.Item2, b.Item3, b.Item4)).ToList(), []);

    public List<CorruptionNearbyPlayer> GetNearby() => Call(() => getNearby.InvokeFunc()
        .Select(n => new CorruptionNearbyPlayer(n.Item1, n.Item2, n.Item3, n.Item4, n.Item5, n.Item6, n.Item7, n.Item8, n.Item9, n.Item10,
            n.Item11, n.Item12, n.Item13)).ToList(), []);

    /// The chance a Breed by that player would have now, without rolling; 0 when Breed would refuse.
    public double BreedChance(string breederName, string breederWorld) => Call(() => breedChance.InvokeFunc(breederName, breederWorld), 0);

    // ── Writes (each needs the player's consent in Corruption's Settings) ───────

    /// One conception roll for the local character as if bred by that player. conceived, not-conceived, not-allowed,
    /// not-set-up, pregnancy-off, cant-carry, pregnant, recovering, protected, paused, hidden, not-a-breeder, too-soon or
    /// no-character. pluginName is shown to the player and passed on in OnConceived.
    public string Breed(string breederName, string breederWorld, string pluginName)
        => Call(() => breed.InvokeFunc(breederName, breederWorld, pluginName), CorruptionStrings.Unavailable);

    /// Changes arousal by up to 25 a call and 60 a minute. ok, limited, not-allowed, not-set-up, paused or refractory.
    public string AddArousal(double amount, string pluginName)
        => Call(() => addArousal.InvokeFunc(amount, pluginName), CorruptionStrings.Unavailable);

    /// A climax as /corr climax would. climaxed, not-allowed, not-set-up, paused, no-scene, refractory or too-low.
    public string RequestClimax(string pluginName) => Call(() => climax.InvokeFunc(pluginName), CorruptionStrings.Unavailable);
}
