using System.Globalization;

namespace Corruption.Core;

[Flags]
public enum BodyFlags { None = 0, CanCarry = 1, CanBreed = 2, Virile = 4, Penis = 8, Vagina = 16 }

/// What a player says their character has, so partners' clients aim a climax from the right place. NotSet leaves it to
/// the bones the skeleton has.
public enum Anatomy { NotSet, Penis, Vagina, Both }

public static class Anatomies
{
    public static BodyFlags Flags(Anatomy anatomy) => anatomy switch
    {
        Anatomy.Penis => BodyFlags.Penis,
        Anatomy.Vagina => BodyFlags.Vagina,
        Anatomy.Both => BodyFlags.Penis | BodyFlags.Vagina,
        _ => BodyFlags.None,
    };

    public static Anatomy Of(BodyFlags flags) => (flags.HasFlag(BodyFlags.Penis), flags.HasFlag(BodyFlags.Vagina)) switch
    {
        (true, true) => Anatomy.Both,
        (true, false) => Anatomy.Penis,
        (false, true) => Anatomy.Vagina,
        _ => Anatomy.NotSet,
    };
}

/// 0 none, 1-3 a trimester, 4 recovering after a birth.
public enum PregnancyStage { None = 0, First = 1, Second = 2, Third = 3, Recovering = 4 }

/// The third SimpleHeels tag: pregnancy roles and stage, and the father as a hash. Every client with this version
/// publishes it after setup, even with pregnancy off, because having it also says "this client layers itself for a
/// partner's climax and sends no layer tells".
public readonly record struct BodyTag(BodyFlags Flags, PregnancyStage Stage, string? FatherHash)
{
    public const string Key = "Corruption.Body";

    private const string Prefix = "P1";
    private const string NoFather = "-";

    public static readonly BodyTag Empty = new(BodyFlags.None, PregnancyStage.None, null);

    public bool CanBreed => Flags.HasFlag(BodyFlags.CanBreed);
    public bool Virile => Flags.HasFlag(BodyFlags.Virile);
    public bool IsPregnant => Stage is PregnancyStage.First or PregnancyStage.Second or PregnancyStage.Third;
    public Anatomy Anatomy => Anatomies.Of(Flags);

    /// Like `P1|3|2|a1b2c3d4`, or `P1|0|0|-` with pregnancy off and anatomy not set.
    public string Format()
        => string.Create(CultureInfo.InvariantCulture, $"{Prefix}|{(int)Flags}|{(int)Stage}|{FatherHash ?? NoFather}");

    /// Null for anything that isn't a well-formed tag of this version.
    public static BodyTag? Parse(string? text)
    {
        var parts = text?.Split('|');
        if (parts is not { Length: 4 } || parts[0] != Prefix) return null;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var flags) || flags > 31) return null;
        if (!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var stage) || stage > 4) return null;
        string? father = null;
        if (parts[3] != NoFather)
        {
            if (!BondHashes.IsHash(parts[3])) return null;
            father = parts[3];
        }
        return new BodyTag((BodyFlags)flags, (PregnancyStage)stage, father);
    }
}

/// Writes the body tag only when it changes.
public sealed class BodyTagPublisher
{
    private BodyTag? published;

    public void Forget() => published = null;

    public BodyTag? Update(BodyTag wanted)
    {
        if (published == wanted) return null;
        published = wanted;
        return wanted;
    }
}
