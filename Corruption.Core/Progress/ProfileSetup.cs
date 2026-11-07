namespace Corruption.Core;

public enum Difficulty { Relaxed, Normal, Hard }

public enum DecayMode { Off, Gentle, Normal, Harsh }

/// What the player chooses the first time. Nothing here changes afterward without a profile reset.
public sealed class ProfileSetup
{
    public const double MinArousalGain = 0.25;
    public const double MaxArousalGain = 3;

    public Difficulty Difficulty { get; set; } = Difficulty.Normal;
    public DecayMode Decay { get; set; } = DecayMode.Normal;
    public bool AutoClimax { get; set; } = true;
    public double ArousalGain { get; set; } = 1;

    /// The personality's id. Null for a character set up before personalities existed, who plays as Easygoing until
    /// they choose once.
    public string? Personality { get; set; }

    /// Up to two trait ids.
    public List<string> Traits { get; set; } = [];

    public double XpMultiplier => Difficulty switch
    {
        Difficulty.Relaxed => 1.5,
        Difficulty.Hard => 0.7,
        _ => 1,
    };

    /// How long before corruption starts fading, and how much of the current level's requirement goes each day.
    public (double GraceHours, double DailyShare) DecayRule => Decay switch
    {
        DecayMode.Gentle => (168, 0.03),
        DecayMode.Normal => (72, 0.05),
        DecayMode.Harsh => (24, 0.1),
        _ => (double.PositiveInfinity, 0),
    };

    public static string Describe(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Relaxed => "1.5x XP",
        Difficulty.Hard => "0.7x XP",
        _ => "Standard XP",
    };

    public static string Describe(DecayMode decay) => decay switch
    {
        DecayMode.Off => "Corruption never fades",
        DecayMode.Gentle => "After 7 days away, lose 3% of a level per day",
        DecayMode.Harsh => "After 1 day away, lose 10% of a level per day",
        _ => "After 3 days away, lose 5% of a level per day",
    };
}
