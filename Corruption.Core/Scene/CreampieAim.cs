namespace Corruption.Core;

/// Whether a climax is a creampie, and in which hole: the giver's penis tip within reach of the receiver's vagina or
/// anus, read from both skeletons.
public static class CreampieAim
{
    /// Cummy's hole names.
    public const string Pussy = "pussy";
    public const string Ass = "ass";

    /// The holes a giver can creampie on this receiver. A giver needs a penis: Penis or Both, or Not set with penis bones.
    /// A receiver set to Penis has no pussy to aim at.
    public static IReadOnlyList<string> Holes(Anatomy receiver, Anatomy giver, bool giverHasPenisBones)
    {
        var givesInside = giver switch
        {
            Anatomy.Vagina => false,
            Anatomy.NotSet => giverHasPenisBones,
            _ => true,
        };
        if (!givesInside) return [];
        return receiver == Anatomy.Penis ? [Ass] : [Pussy, Ass];
    }

    /// The hole the giver's climax is inside, or null when it's outside or the bones can't tell.
    public static string? Inside(ShotAim.BoneLookup giverBones, ShotAim.BoneLookup receiverBones, Anatomy giver, Anatomy receiver)
    {
        var tip = ShotAim.PenisTipPoint(giverBones);
        var holes = Holes(receiver, giver, tip is not null);
        if (tip is not { } at || holes.Count == 0) return null;
        return ShotAim.InsideHole(at, ShotAim.HolePoints(receiverBones, holes));
    }

    /// Each hole's distance from the giver's tip, for the Debug readout. Empty when there's no tip.
    public static Dictionary<string, float> Distances(ShotAim.BoneLookup giverBones, ShotAim.BoneLookup receiverBones)
    {
        if (ShotAim.PenisTipPoint(giverBones) is not { } tip) return [];
        return ShotAim.HolePoints(receiverBones, [Pussy, Ass]).ToDictionary(h => h.Key, h => System.Numerics.Vector3.Distance(h.Value, tip));
    }

    /// The Cummy part a hole's creampie steps up, and a lick there ends it.
    public static string PartOf(string hole) => hole == Pussy ? CummyParts.Groin : CummyParts.Butt;
}
