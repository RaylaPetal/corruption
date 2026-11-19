using System.Numerics;

namespace Corruption.Core;

/// Where a climax lands: the giver's genitals, and the receiver's Cummy part nearest to them. Bones come from the
/// game as world positions by name; IVCS bones (iv_*) are used when the skeleton has them, vanilla ones (j_*) otherwise,
/// as PoseKit does.
public static class ShotAim
{
    /// Finds the average world position of the named bones that exist, or null when none do.
    public delegate Vector3? BoneLookup(IReadOnlyList<string> names);

    /// A part's point on the body: the first bone group that exists, then pushed along the body's own front and up, so front
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
    private static readonly string[] PenisBase = ["iv_ochinko_a"];
    private static readonly string[] PenisShaft = ["iv_ochinko_e"];
    private static readonly string[] Vagina = ["iv_omanko"];
    private static readonly string[] Hips = ["j_kosi"];
    private static readonly string[] Neck = ["j_kubi"];
    private static readonly string[] ShoulderL = ["j_sako_l"];
    private static readonly string[] ShoulderR = ["j_sako_r"];
    private static readonly string[] Chest = ["j_mune_l", "j_mune_r"];
    private static readonly string[] UpperSpine = ["j_sebo_c"];
    private static readonly string[] Lips = ["j_f_ulip_01_l", "j_f_ulip_01_r", "j_f_dlip_01_l", "j_f_dlip_01_r"];
    private static readonly string[] Head = ["j_kao"];

    /// IVCS hole bones, for creampies. `iv_koumon` is the anus, as PoseKit and Cummy's own drip attach points use it.
    /// Neither has a vanilla stand-in: a creampie needs an IVCS skeleton, as Cummy's drip does.
    private static readonly Dictionary<string, string[]> HoleBones = new()
    {
        [CreampieAim.Pussy] = ["iv_omanko"],
        [CreampieAim.Ass] = ["iv_koumon"],
    };

    /// How close the penis tip has to be to a hole for a climax to count as inside, in game units (about metres).
    public const float InsideRadius = 0.07f;

    /// The giver's penis tip, the only point a creampie is measured from; null without the bone. Never the shaft, the
    /// vagina or the hips, so a grind against the body isn't "inside".
    public static Vector3? PenisTipPoint(BoneLookup bones) => bones(PenisTip);

    /// The receiver's hole points among <paramref name="holes"/> that its bones allow.
    public static Dictionary<string, Vector3> HolePoints(BoneLookup bones, IEnumerable<string> holes)
    {
        var points = new Dictionary<string, Vector3>();
        foreach (var hole in holes)
            if (HoleBones.TryGetValue(hole, out var names) && bones(names) is { } at) points[hole] = at;
        return points;
    }

    /// The nearest hole within <paramref name="radius"/> of the tip, or null when none is.
    public static string? InsideHole(Vector3 tip, IReadOnlyDictionary<string, Vector3> holes, float radius = InsideRadius)
        => holes.Where(h => Vector3.Distance(h.Value, tip) <= radius)
            .OrderBy(h => Vector3.DistanceSquared(h.Value, tip))
            .Select(h => h.Key)
            .FirstOrDefault();

    /// Which way a body's front and top point in the world. Read from the torso's bones when it can be, so a body an
    /// animation lays down or bends over still has its front out of the chest; otherwise the model's facing and world up.
    public readonly record struct Frame(Vector3 Forward, Vector3 Up, bool FromBones)
    {
        public static Frame Upright(Vector3 facing) => new(facing, Vector3.UnitY, false);
    }

    /// How far the shoulder line has to be from parallel with the spine (the sine of the angle between them) for the
    /// torso's bones to give a frame.
    public const float MinFrameSpread = 0.3f;

    /// The body's frame: up along the spine from hips to neck, side along the shoulders, front across both. The front is
    /// set by the chest sitting in front of the spine, or the lips in front of the head, so it doesn't depend on which
    /// way the axes or the left and right bones run. Falls back to <paramref name="facing"/> and world up when the bones
    /// are missing or line up.
    public static Frame BodyFrame(BoneLookup bones, Vector3 facing)
    {
        var fallback = Frame.Upright(facing);
        if (bones(Neck) is not { } neck || bones(Hips) is not { } hips
            || bones(ShoulderL) is not { } left || bones(ShoulderR) is not { } right) return fallback;
        var spine = neck - hips;
        var across = right - left;
        if (spine.LengthSquared() < 1e-6f || across.LengthSquared() < 1e-6f) return fallback;

        var up = Vector3.Normalize(spine);
        var side = Vector3.Normalize(across);
        var flat = side - up * Vector3.Dot(side, up);
        if (flat.Length() < MinFrameSpread) return fallback;

        var forward = Vector3.Cross(up, Vector3.Normalize(flat));
        if (FrontHint(bones, hips, forward) is { } sign && sign < 0) forward = -forward;
        return new Frame(forward, up, true);
    }

    /// Which side of the torso <paramref name="forward"/> is on, by the chest in front of the spine, then the lips in
    /// front of the head. Null when neither tells.
    private static float? FrontHint(BoneLookup bones, Vector3 hips, Vector3 forward)
    {
        if (bones(Chest) is { } chest)
        {
            var dot = Vector3.Dot(forward, chest - (bones(UpperSpine) ?? hips));
            if (MathF.Abs(dot) > 1e-4f) return dot;
        }
        if (bones(Lips) is { } lips && bones(Head) is { } head)
        {
            var dot = Vector3.Dot(forward, lips - head);
            if (MathF.Abs(dot) > 1e-4f) return dot;
        }
        return null;
    }

    /// A penis tip closer than this to the penis base is a collapsed, hidden penis.
    public const float MinShaft = 0.03f;

    /// A penis tip farther than this from the hips isn't part of the shown body.
    public const float MaxTipFromHips = 0.40f;

    /// The tip-to-base and tip-to-hips distances the hidden-penis check reads, for the Debug readout. Each is null
    /// without its bones.
    public static (float? Shaft, float? FromHips) PenisReadings(BoneLookup bones)
    {
        if (bones(PenisTip) is not { } tip) return (null, null);
        float? shaft = bones(PenisBase) is { } at ? Vector3.Distance(tip, at) : null;
        float? fromHips = bones(Hips) is { } hips ? Vector3.Distance(tip, hips) : null;
        return (shaft, fromHips);
    }

    /// Whether the penis bones look like part of the shown body: not collapsed, and at the hips. A missing reference bone
    /// skips its check.
    public static bool PenisLooksShown(BoneLookup bones)
    {
        var (shaft, fromHips) = PenisReadings(bones);
        return shaft is not < MinShaft && fromHips is not > MaxTipFromHips;
    }

    /// Where a shot comes from, tried in order. IVCS skeletons may carry both sets of genital bones whatever the body
    /// shows, so the giver's own anatomy choice picks which. Not set tries the penis first, then the vagina, unless the
    /// penis looks hidden, when the vagina goes first. Both shoots from the penis. A skeleton without either falls back to
    /// a point in front of the hips.
    private static (string Name, string[] Bones)[] SourceOrder(Anatomy anatomy, BoneLookup bones) => anatomy switch
    {
        Anatomy.Vagina => [("vagina", Vagina), ("hips", Hips)],
        Anatomy.NotSet when !PenisLooksShown(bones) => [("vagina", Vagina), ("penis", PenisTip), ("shaft", PenisShaft), ("hips", Hips)],
        _ => [("penis", PenisTip), ("shaft", PenisShaft), ("vagina", Vagina), ("hips", Hips)],
    };

    /// The facing direction from a model yaw, as the game gives it.
    public static Vector3 Facing(float yaw) => new(MathF.Sin(yaw), 0, MathF.Cos(yaw));

    public static Vector3? GiverPoint(BoneLookup bones, Vector3 facing, Anatomy anatomy = Anatomy.NotSet)
        => GiverPoint(bones, BodyFrame(bones, facing), anatomy);

    public static Vector3? GiverPoint(BoneLookup bones, Frame frame, Anatomy anatomy = Anatomy.NotSet)
        => GiverAim(bones, frame, anatomy)?.Point;

    /// The shot's point and the name of what it came from (penis, shaft, vagina or hips), or null without any of them.
    public static (Vector3 Point, string Source)? GiverAim(BoneLookup bones, Frame frame, Anatomy anatomy = Anatomy.NotSet)
    {
        foreach (var (name, group) in SourceOrder(anatomy, bones))
        {
            if (bones(group) is not { } at) continue;
            // Only the vanilla hip bone needs moving forward to where genitals would be, in the giver's own frame.
            return (group == Hips ? at + frame.Forward * 0.12f + frame.Up * -0.08f : at, name);
        }
        return null;
    }

    /// Each part's point on the receiver that its bones allow.
    public static Dictionary<string, Vector3> PartPoints(BoneLookup bones, Vector3 facing)
        => PartPoints(bones, BodyFrame(bones, facing));

    /// Each part's point, its offsets along the body's own front and up.
    public static Dictionary<string, Vector3> PartPoints(BoneLookup bones, Frame frame)
    {
        var points = new Dictionary<string, Vector3>();
        foreach (var (part, anchor) in Anchors)
        {
            foreach (var group in anchor.Groups)
            {
                if (bones(group) is not { } at) continue;
                points[part] = at + frame.Forward * anchor.Forward + frame.Up * anchor.Up;
                break;
            }
        }
        return points;
    }

    /// How close a shot has to be to the mouth's point for the mouth to go first, in game units.
    public const float MouthRadius = 0.12f;

    private const string Mouth = "mo";

    /// Parts from nearest to farthest from the shot, except that a shot within <see cref="MouthRadius"/> of the mouth
    /// tries the mouth first. Parts with no point go last, in the given order.
    public static IReadOnlyList<string> Order(Vector3 shot, IReadOnlyDictionary<string, Vector3> points, IEnumerable<string> allParts)
    {
        var all = allParts.ToList();
        var order = all.Where(points.ContainsKey).OrderBy(p => Vector3.DistanceSquared(points[p], shot))
            .Concat(all.Where(p => !points.ContainsKey(p)))
            .ToList();
        if (points.TryGetValue(Mouth, out var mouth) && Vector3.Distance(mouth, shot) <= MouthRadius && order.Remove(Mouth))
            order.Insert(0, Mouth);
        return order;
    }
}
