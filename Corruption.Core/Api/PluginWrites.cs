namespace Corruption.Core;

/// What other plugins may do to this character through the API. Everything is off until the player turns it on.
public sealed class PluginConsent
{
    public bool Breed { get; set; }
    public bool Arousal { get; set; }
    public bool Climax { get; set; }
}

/// The API's outcome strings, shared by the gates and the client file.
public static class ApiOutcomes
{
    public const string WrongThread = "wrong-thread";
    public const string Error = "error";
    public const string NotAllowed = "not-allowed";
    public const string NotSetUp = "not-set-up";
    public const string NoCharacter = "no-character";
    public const string Paused = "paused";

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

    public const string Ok = "ok";
    public const string Limited = "limited";
    public const string Refractory = "refractory";

    public const string Climaxed = "climaxed";
    public const string TooLow = "too-low";
    public const string NoScene = "no-scene";

    /// Where a scene conception comes from, in OnConceived.
    public const string SceneSource = "scene";
}

/// Limits on what plugins can do: arousal per call and per minute, and how often one breeder can breed.
public static class PluginLimits
{
    public const double ArousalPerCall = 25;
    public const double ArousalPerMinute = 60;
    public static readonly TimeSpan ArousalWindow = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan BreedGap = TimeSpan.FromSeconds(60);
}

/// One plugin's last write, for the Settings list.
public sealed record PluginCall(string Plugin, string Call, string Outcome, DateTime AtUtc);

/// Arousal each plugin changed in the last minute, so no plugin moves it more than the limit. Memory only.
public sealed class ArousalAllowance
{
    private readonly Dictionary<string, List<(DateTime At, double Amount)>> spent = new(StringComparer.OrdinalIgnoreCase);

    /// How much of <paramref name="amount"/> this plugin may apply now, after the per-call and per-minute limits.
    public double Allow(string plugin, double amount, DateTime now)
    {
        var perCall = Math.Clamp(amount, -PluginLimits.ArousalPerCall, PluginLimits.ArousalPerCall);
        var list = spent.TryGetValue(plugin, out var l) ? l : spent[plugin] = [];
        list.RemoveAll(e => now - e.At >= PluginLimits.ArousalWindow);
        var left = Math.Max(0, PluginLimits.ArousalPerMinute - list.Sum(e => e.Amount));
        return Math.Sign(perCall) * Math.Min(Math.Abs(perCall), left);
    }

    public void Spend(string plugin, double amount, DateTime now)
    {
        if (amount == 0) return;
        (spent.TryGetValue(plugin, out var l) ? l : spent[plugin] = []).Add((now, Math.Abs(amount)));
    }
}
