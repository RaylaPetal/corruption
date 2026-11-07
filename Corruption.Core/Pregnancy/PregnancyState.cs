namespace Corruption.Core;

/// A birth, kept for the Profile's family record.
public sealed class BirthRecord
{
    public DateTime BornUtc { get; set; }
    public DateTime ConceivedUtc { get; set; }

    /// The father as `Name@World`.
    public string FatherKey { get; set; } = "";
}

/// One character's opt-in pregnancy: roles, cycle and any pregnancy, all as timestamps so offline time counts.
/// Stored in the save.
public sealed class PregnancyState
{
    public bool Enabled { get; set; }
    public bool CanCarry { get; set; }
    public bool CanBreed { get; set; }

    /// The player's own conception chance multiplier from Settings, 0.1 to 5. 1 is the normal chance.
    public double ChanceFactor { get; set; } = 1;

    /// Day 1 of the current cycle.
    public DateTime CycleStartUtc { get; set; }

    /// Set while pregnant.
    public DateTime? ConceivedUtc { get; set; }
    public DateTime? DueUtc { get; set; }

    /// The father as `Name@World`, while pregnant.
    public string? FatherKey { get; set; }

    /// After a birth, until this time. Default when not recovering.
    public DateTime RecoveryUntilUtc { get; set; }

    public int Conceptions { get; set; }
    public int Births { get; set; }

    /// Every birth, oldest first.
    public List<BirthRecord> BirthLog { get; set; } = [];

    /// Times this player was told they fathered a child.
    public int Fathered { get; set; }

    /// Today's craving while pregnant.
    public string CravingDay { get; set; } = "";
    public CravingKind? Craving { get; set; }
    public bool CravingMet { get; set; }
    public bool Unsatisfied { get; set; }

    /// Scene time toward a "ten minutes" craving today.
    public double CravingSeconds { get; set; }

    /// Carriers this player has been told they fathered, while those carriers stay pregnant.
    public HashSet<string> ToldFather { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// The chosen look per trimester, keyed First, Second and Third.
    public Dictionary<string, TierVisual> Visuals { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
