using System.Text.Json;

namespace Corruption.Core;

/// Every tunable constant. Ships as balance.json; these defaults apply when that file is missing or invalid.
public sealed class Balance
{
    public double TickXp { get; set; } = 6;
    public double ArousalPerTick { get; set; } = 1.5;

    public double XpPerOwnTier { get; set; } = 0.05;
    public double XpPerPartnerTierAbove { get; set; } = 0.1;
    public double PartnerFactorCap { get; set; } = 1.3;
    public double XpPerWatcher { get; set; } = 0.1;
    public double WatcherFactorCap { get; set; } = 1.5;
    public double XpPerStreakDay { get; set; } = 0.05;
    public double StreakFactorCap { get; set; } = 1.3;
    public double BondedFactor { get; set; } = 1.25;
    public double DevotedBondFactor { get; set; } = 1.35;
    public double InTuneArousalFactor { get; set; } = 1.1;
    public double TogetherSeconds { get; set; } = 30;
    public double TogetherXp { get; set; } = 60;
    public double MinimumRefractorySeconds { get; set; } = 15;
    public double XpPerLayerWorn { get; set; } = 0.03;
    public double MarkedFactorCap { get; set; } = 1.3;
    public double XpPerStar { get; set; } = 0.05;
    public int StarsWithBonus { get; set; } = 10;

    public double WatchRadiusYalms { get; set; } = 15;
    public double WatchCloseYalms { get; set; } = 8;
    public double WatchFacingDegrees { get; set; } = 60;

    public double FatigueGraceMinutes { get; set; } = 10;
    public double FatigueStep { get; set; } = 0.1;
    public double FatigueFloor { get; set; } = 0.2;
    public double FatigueForgetMinutes { get; set; } = 5;

    public double CurveBase { get; set; } = 100;
    public double CurveFactor { get; set; } = 10;
    public double CurveExponent { get; set; } = 1.5;
    public int LevelCap { get; set; } = 100;

    public double FirstPartnerXp { get; set; } = 100;
    public double FirstActXp { get; set; } = 25;
    public double ClimaxXp { get; set; } = 60;
    public double PartnerClimaxShare { get; set; } = 0.5;
    public double LayerXp { get; set; } = 30;

    public double ArousalPerCorruptionTier { get; set; } = 0.05;
    public double ArousalDecayStep { get; set; } = 1;
    public double ArousalDecaySeconds { get; set; } = 30;
    public double ManualClimaxMinimum { get; set; } = 70;
    public double ArousalAfterClimax { get; set; } = 20;
    public double RefractorySeconds { get; set; } = 60;
    public double RefractoryGainFactor { get; set; } = 0.5;

    public double TickSeconds { get; set; } = 10;
    public double SceneRangeYalms { get; set; } = 3;
    public double SceneEndSeconds { get; set; } = 30;
    public double PartnerClimaxRecencySeconds { get; set; } = 30;
    public double PartnerClimaxLimitSeconds { get; set; } = 60;
    public double SaveIntervalSeconds { get; set; } = 60;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// The shipped constants, or the defaults and a warning when they can't be used.
    public static Balance Load(string path, out string? warning)
    {
        warning = null;
        try
        {
            var loaded = JsonSerializer.Deserialize<Balance>(File.ReadAllText(path), Options);
            if (loaded is not null && loaded.IsValid()) return loaded;
            warning = $"{path} has invalid values; using built-in defaults.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            warning = $"Couldn't read {path} ({ex.Message}); using built-in defaults.";
        }
        return new Balance();
    }

    private bool IsValid()
        => TickXp > 0 && ArousalPerTick > 0
        && TickSeconds > 0 && ArousalDecaySeconds > 0 && LevelCap >= 2 && SaveIntervalSeconds > 0;
}
