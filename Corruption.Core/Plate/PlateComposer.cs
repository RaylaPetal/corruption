using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace Corruption.Core;

public readonly record struct PlateValues(int Level, double Arousal, int Prestige = 0)
{
    public Tier Tier => Progression.TierOf(Level);
}

/// The user's three texts, and how they cycle.
public readonly record struct PlateTexts(string Steady, string Act, string Climax, double CycleSeconds, double ClimaxSeconds);

/// What a character is doing, for choosing their text.
public readonly record struct PlateScene(bool InAct, DateTime? LastClimaxUtc);

/// The text over a nameplate, with its colour and, at high arousal, a glowing outline.
public sealed record RenderedPlate(string Text, Vector3 Color, Vector3? Glow);

public static partial class PlateComposer
{
    public const string DefaultTemplate = "{tier} ♥ Lv. {level}";

    /// Every earlier default, so saved settings can move on. Text the user wrote isn't touched.
    private static readonly string[] OldDefaults = ["{tier} ♥{arousal}", "{tier}", "{tier} Lv {level}", "{tier} ♥ {level}"];

    public static string UpgradeTemplate(string template) => OldDefaults.Contains(template) ? DefaultTemplate : template;
    public const string DefaultActTemplate = "♥ {arousal}%";
    public const string DefaultClimaxTemplate = "♥ Climaxing ♥";

    public const double GlowFrom = 80;

    private static readonly Vector3 Red = new(0.95f, 0.1f, 0.15f);

    private static readonly string[] KnownTokens = ["tier", "level", "arousal", "arousalBar", "prestige"];

    [GeneratedRegex(@"\{([A-Za-z]+)\}")]
    private static partial Regex TokenPattern();

    public static string Render(string template, PlateValues values)
        => TokenPattern().Replace(template, match => match.Groups[1].Value switch
        {
            "tier" => values.Tier.ToString(),
            "level" => values.Level.ToString(CultureInfo.InvariantCulture),
            "arousal" => ArousalNumber(values.Arousal).ToString(CultureInfo.InvariantCulture),
            "arousalBar" => ArousalBar(values.Arousal),
            "prestige" => PrestigeRules.Label(values.Prestige),
            _ => match.Value,
        });

    /// Tokens the template uses that this version doesn't know, so the editor can flag them.
    public static IReadOnlyList<string> UnknownTokens(string template)
        => TokenPattern().Matches(template).Select(m => m.Groups[1].Value)
            .Where(t => !KnownTokens.Contains(t)).Distinct().ToList();

    public static int ArousalNumber(double arousal) => (int)Math.Floor(Math.Clamp(arousal, 0, 100));

    /// Five hearts, one per 20 arousal.
    public static string ArousalBar(double arousal)
    {
        var filled = Math.Clamp(ArousalNumber(arousal) / 20, 0, 5);
        return new StringBuilder().Insert(0, "♥", filled).Insert(filled, "♡", 5 - filled).ToString();
    }

    public static RenderedPlate Compose(string template, PlateValues values, bool forceGlow = false)
    {
        var t = (float)Math.Clamp(values.Arousal / 100, 0, 1);
        return new RenderedPlate(Render(template, values), Vector3.Lerp(TierColor(values.Tier), Red, t), forceGlow || values.Arousal >= GlowFrom ? Red : null);
    }

    /// The text showing right now. The steady text alone when arousal means nothing. During an act, or with any arousal
    /// left, it cycles between arousal and the steady text. Just after a climax it cycles between the climax text, which goes first, and arousal.
    public static RenderedPlate ComposeNow(DateTime now, PlateTexts texts, PlateValues values, PlateScene scene)
    {
        var interval = Math.Max(1, texts.CycleSeconds);
        if (scene.LastClimaxUtc is { } climax && (now - climax).TotalSeconds < texts.ClimaxSeconds)
        {
            var showClimax = (long)Math.Floor((now - climax).TotalSeconds / interval) % 2 == 0;
            return showClimax ? Compose(texts.Climax, values, forceGlow: true) : Compose(texts.Act, values);
        }
        if (!scene.InAct && ArousalNumber(values.Arousal) == 0) return Compose(texts.Steady, values);

        var showArousal = (long)Math.Floor(now.Subtract(DateTime.UnixEpoch).TotalSeconds / interval) % 2 == 0;
        return Compose(showArousal ? texts.Act : texts.Steady, values);
    }

    public static Vector3 TierColor(Tier tier) => tier switch
    {
        Tier.Pure => new Vector3(0.95f, 0.95f, 0.98f),
        Tier.Curious => new Vector3(0.98f, 0.8f, 0.9f),
        Tier.Tempted => new Vector3(0.95f, 0.6f, 0.85f),
        Tier.Tainted => new Vector3(0.8f, 0.45f, 0.9f),
        Tier.Corrupted => new Vector3(0.6f, 0.3f, 0.85f),
        Tier.Depraved => new Vector3(0.45f, 0.15f, 0.65f),
        _ => new Vector3(0.3f, 0.05f, 0.35f),
    };
}
