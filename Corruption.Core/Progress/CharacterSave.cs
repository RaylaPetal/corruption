namespace Corruption.Core;

/// Everything stored for one local character.
public sealed class CharacterSave
{
    /// Null until the player has done the first-time setup. Nothing earns before that.
    public ProfileSetup? Setup { get; set; }

    public int Level { get; set; } = 1;
    public double Xp { get; set; }

    public double Arousal { get; set; }

    /// When arousal was last accounted for; decay runs from here, which also covers time the plugin wasn't running.
    public DateTime ArousalAnchorUtc { get; set; }
    public DateTime RefractoryUntilUtc { get; set; }

    public HashSet<string> SeenPartners { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> SeenActs { get; set; } = [];

    /// The last verified tick, and how far fading has been applied; both drive corruption fading.
    public DateTime LastVerifiedUtc { get; set; }
    public DateTime FadedThroughUtc { get; set; }

    public int StreakDays { get; set; }

    /// The UTC day of the last verified tick.
    public DateTime LastSceneDayUtc { get; set; }

    public GoalSave Goals { get; set; } = new();

    public bool Paused { get; set; }

    /// Publishes no tag, so other clients see no plugin here. Scenes can't earn while hidden, since nobody can earn back.
    public bool Hidden { get; set; }

    /// Stars from prestiging. Fading never takes them; only a profile reset does.
    public int Prestige { get; set; }

    /// The bond request or bond, as a `Name@World` key. Null when there's neither.
    public string? BondWith { get; set; }

    /// True once both tags named each other.
    public bool BondFormed { get; set; }

    /// The chosen look per tier, keyed by tier name. Kept through a reset, since it's a choice rather than progress.
    public Dictionary<string, TierVisual> TierVisuals { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// Back to the start, with setup to do again. The pause and hiding stay.
    public void ResetProgress(DateTime now)
    {
        Setup = null;
        LastVerifiedUtc = default;
        FadedThroughUtc = default;
        Level = 1;
        Xp = 0;
        Arousal = 0;
        ArousalAnchorUtc = now;
        RefractoryUntilUtc = default;
        StreakDays = 0;
        LastSceneDayUtc = default;
        Goals = new GoalSave();
        SeenPartners.Clear();
        SeenActs.Clear();
        Prestige = 0;
        BondWith = null;
        BondFormed = false;
    }
}

/// progress.json: one save per character, keyed by ContentId.
public sealed class ProgressFile
{
    public Dictionary<string, CharacterSave> Characters { get; set; } = [];

    public CharacterSave ForCharacter(ulong contentId, DateTime now)
    {
        var key = contentId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!Characters.TryGetValue(key, out var save))
            Characters[key] = save = new CharacterSave { ArousalAnchorUtc = now };
        return save;
    }
}
