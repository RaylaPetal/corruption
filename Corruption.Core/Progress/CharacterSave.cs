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

    public Wallet Wallet { get; set; } = new();

    /// The welcome window version this character finished, 0 before it.
    public int WelcomeVersion { get; set; }

    /// What this character has, for partners aiming a climax. A choice, not progress, so a reset keeps it.
    public Anatomy Anatomy { get; set; }

    /// The longest verified scene, start to last passing check.
    public double LongestSceneSeconds { get; set; }

    /// The most verified seconds in one scene, gaps and pauses left out. For Marathon.
    public double LongestVerifiedSceneSeconds { get; set; }

    /// The most watchers at once during a verified scene. For Crowd Pleaser.
    public int PeakWatchers { get; set; }

    /// The UTC day solo time is counted for, the verified solo seconds spent that day, and extra seconds from Do Not
    /// Disturb Signs that day.
    public DateTime SoloDayUtc { get; set; }
    public double SoloSecondsToday { get; set; }
    public double SoloBonusSecondsToday { get; set; }

    /// Solo climaxes with 3 or more watching. For Caught in the Act.
    public int CaughtInTheAct { get; set; }

    /// Cummy version 5 has been connected for this character, so lick goals can be met.
    public bool HadCummyV5 { get; set; }

    /// Times Cummy showed every unblocked part layered, and the face at its top stage. For Head to Toe and Glazed.
    public int HeadToToe { get; set; }
    public int Glazed { get; set; }

    /// Verified seconds of scenes left before a Hypersexual character stops being Pent Up. 0 when they aren't.
    public double PentUpSecondsLeft { get; set; }

    /// Different Full Moons with a verified tick, and the cycle of the last one counted. For Moon-Touched.
    public int FullMoonsSeen { get; set; }
    public long LastFullMoonCycle { get; set; } = long.MinValue;

    /// Items owned, by id, and items working now.
    public Dictionary<string, int> Inventory { get; set; } = [];
    public Dictionary<string, ActiveEffect> Effects { get; set; } = [];

    public PregnancyState Pregnancy { get; set; } = new();

    /// The last scene's summary, for the Profile page. Null before the first.
    public SceneSummary? LastScene { get; set; }

    /// Each partner's history, keyed `Name@World`.
    public Dictionary<string, PartnerRecord> Partners { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// True once older per-partner scene counts were turned into records.
    public bool PartnersSeeded { get; set; }

    /// Unlocked titles and the equipped one. Prestige keeps them; a reset clears them, bought ones too.
    public TitleSave Titles { get; set; } = new();

    /// Verified scene time with the bonded partner, which sets the bond level.
    public double BondSeconds { get; set; }

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
        LongestVerifiedSceneSeconds = 0;
        PeakWatchers = 0;
        FullMoonsSeen = 0;
        PentUpSecondsLeft = 0;
        SoloDayUtc = default;
        SoloSecondsToday = 0;
        SoloBonusSecondsToday = 0;
        CaughtInTheAct = 0;
        HeadToToe = 0;
        Glazed = 0;
        LastFullMoonCycle = long.MinValue;
        Goals = new GoalSave();
        SeenPartners.Clear();
        SeenActs.Clear();
        Prestige = 0;
        BondWith = null;
        BondFormed = false;
        BondSeconds = 0;
        Wallet = new Wallet();
        Inventory.Clear();
        Effects.Clear();
        Pregnancy = new PregnancyState();
        LastScene = null;
        Partners.Clear();
        PartnersSeeded = true;
        // Nothing is earned after a reset, so there's nothing to unlock silently either.
        Titles = new TitleSave { Initialized = true };
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
