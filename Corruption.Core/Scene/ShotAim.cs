using System.Numerics;

namespace Corruption.Core;

/// Where a climax lands: the giver's genitals, and the receiver's Cummy part nearest to them. Bones come from the
/// game as world positions by name; IVCS bones (iv_*) are used when the skeleton has them, vanilla ones (j_*) otherwise,
/// as PoseKit does.
public static class ShotAim
{
    /// Finds the average world position of the named bones that exist, or null when none do.
    public delegate Vector3? BoneLookup(IReadOnlyList<string> names);

    /// A part's point on the body: the first bone group that exists, then pushed along the body's facing and up, so front
    /// and back parts around the same bones come apart.
    private sealed record Anchor(string[][] Groups, float Forward = 0, float Up = 0);

    /// Cummy part ids, as in CummyParts.
    private static readonly Dictionary<string, Anchor> Anchors = new()
    {
        ["mo"] = new([["j_f_ulip_01_l", "j_f_ulip_01_r", "j_f_dlip_01_l", "j_f_dlip_01_r"], ["j_kao"]], Forward: 0.08f),
        ["ch"] = new([["j_mune_l", "j_mune_r"], ["j_sebo_c"]], Forward: 0.10f),
        ["bk"] = new([["j_sebo_c", "j_sebo_b"]], Forward: -0.14f),
        ["st"] = new([["j_sebo_a"]], Forward: 0.12f),
        ["gr"] = new([["iv_omanko"], ["iv_ochinko_a"], ["j_kosi"]], Forward: 0.06f, Up: -0.08f),
        ["bt"] = new([["iv_shiri_l", "iv_shiri_r"], ["j_shiri_l", "j_shiri_r"], ["j_kosi"]], Forward: -0.10f),
        ["al"] = new([["j_ude_a_l", "j_ude_b_l"]]),
        ["ar"] = new([["j_ude_a_r", "j_ude_b_r"]]),
        ["hl"] = new([["j_te_l"]]),
        ["hr"] = new([["j_te_r"]]),
        ["ll"] = new([["j_asi_a_l", "j_asi_b_l"]], Forward: 0.07f),
        ["lr"] = new([["j_asi_a_r", "j_asi_b_r"]], Forward: 0.07f),
        ["bl"] = new([["j_asi_a_l", "j_asi_b_l"]], Forward: -0.07f),
        ["br"] = new([["j_asi_a_r", "j_asi_b_r"]], Forward: -0.07f),
        ["fl"] = new([["j_asi_d_l", "j_asi_e_l"]]),
        ["fr"] = new([["j_asi_d_r", "j_asi_e_r"]]),
    };

    private static readonly string[] PenisTip = ["iv_ochinko_f"];
    private static readonly string[] PenisShaft = ["iv_ochinko_e"];
    private static readonly string[] Vagina = ["iv_omanko"];
    private static readonly string[] Hips = ["j_kosi"];

    /// Where a shot comes from, tried in order. IVCS skeletons may carry both sets of genital bones whatever the body
    /// shows, so the giver's own anatomy choice picks which. Not set tries the penis first, then the vagina. Both shoots
    /// from the penis. A skeleton without either falls back to a point in front of the hips.
    private static string[][] SourceOrder(Anatomy anatomy) => anatomy switch
    {
        Anatomy.Vagina => [Vagina, Hips],
        _ => [PenisTip, PenisShaft, Vagina, Hips],
    };

    /// The facing direction from a model yaw, as the game gives it.
    public static Vector3 Facing(float yaw) => new(MathF.Sin(yaw), 0, MathF.Cos(yaw));

    public static Vector3? GiverPoint(BoneLookup bones, Vector3 facing, Anatomy anatomy = Anatomy.NotSet)
    {
        foreach (var group in SourceOrder(anatomy))
        {
            if (bones(group) is not { } at) continue;
            // Only the vanilla hip bone needs moving forward to where genitals would be.
            return group[0] == "j_kosi" ? at + facing * 0.12f + Vector3.UnitY * -0.08f : at;
        }
        return null;
    }

    /// Each part's point on the receiver that its bones allow.
    public static Dictionary<string, Vector3> PartPoints(BoneLookup bones, Vector3 facing)
    {
        var points = new Dictionary<string, Vector3>();
        foreach (var (part, anchor) in Anchors)
        {
            foreach (var group in anchor.Groups)
            {
                if (bones(group) is not { } at) continue;
                points[part] = at + facing * anchor.Forward + Vector3.UnitY * anchor.Up;
                break;
            }
        }
        return points;
    }

    /// Parts from nearest to farthest from the shot. Parts with no point go last, in the given order.
    public static IReadOnlyList<string> Order(Vector3 shot, IReadOnlyDictionary<string, Vector3> points, IEnumerable<string> allParts)
    {
        var all = allParts.ToList();
        return all.Where(points.ContainsKey).OrderBy(p => Vector3.DistanceSquared(points[p], shot))
            .Concat(all.Where(p => !points.ContainsKey(p)))
            .ToList();
    }
}
