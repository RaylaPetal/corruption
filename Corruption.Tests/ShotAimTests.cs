using System.Numerics;
using Corruption.Core;

namespace Corruption.Tests;

public class ShotAimTests
{
    /// A receiver standing at the origin facing +Z, with vanilla bones roughly where a body has them.
    private static readonly Dictionary<string, Vector3> Receiver = new()
    {
        ["j_f_ulip_01_l"] = new(0, 1.55f, 0.08f), ["j_kao"] = new(0, 1.6f, 0),
        ["j_mune_l"] = new(-0.08f, 1.3f, 0.08f), ["j_mune_r"] = new(0.08f, 1.3f, 0.08f),
        ["j_sebo_c"] = new(0, 1.3f, 0), ["j_sebo_b"] = new(0, 1.2f, 0), ["j_sebo_a"] = new(0, 1.1f, 0),
        ["j_kubi"] = new(0, 1.45f, 0), ["j_sako_l"] = new(-0.15f, 1.42f, 0), ["j_sako_r"] = new(0.15f, 1.42f, 0),
        ["j_kosi"] = new(0, 0.95f, 0), ["j_shiri_l"] = new(-0.08f, 0.9f, -0.08f), ["j_shiri_r"] = new(0.08f, 0.9f, -0.08f),
        ["j_ude_a_l"] = new(-0.25f, 1.3f, 0), ["j_ude_b_l"] = new(-0.3f, 1.1f, 0),
        ["j_ude_a_r"] = new(0.25f, 1.3f, 0), ["j_ude_b_r"] = new(0.3f, 1.1f, 0),
        ["j_te_l"] = new(-0.32f, 0.9f, 0), ["j_te_r"] = new(0.32f, 0.9f, 0),
        ["j_asi_a_l"] = new(-0.1f, 0.8f, 0), ["j_asi_b_l"] = new(-0.1f, 0.5f, 0),
        ["j_asi_a_r"] = new(0.1f, 0.8f, 0), ["j_asi_b_r"] = new(0.1f, 0.5f, 0),
        ["j_asi_d_l"] = new(-0.1f, 0.1f, 0), ["j_asi_e_l"] = new(-0.1f, 0.02f, 0.08f),
        ["j_asi_d_r"] = new(0.1f, 0.1f, 0), ["j_asi_e_r"] = new(0.1f, 0.02f, 0.08f),
    };

    private static readonly Vector3 Pelvis = new(0, 0.95f, 0);

    /// The fixture turned about its pelvis, as an animation would, while its model yaw stays facing +Z.
    public static Dictionary<string, Vector3> Turned(Dictionary<string, Vector3> bones, Quaternion turn)
        => bones.ToDictionary(b => b.Key, b => Pelvis + Vector3.Transform(b.Value - Pelvis, turn));

    /// Lying on its back: the chest faces up, the head towards -Z.
    public static readonly Quaternion OnBack = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2);

    /// Lying face down: the chest faces the floor, the head towards +Z.
    public static readonly Quaternion FaceDown = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);

    private static ShotAim.BoneLookup Lookup(Dictionary<string, Vector3> bones) => names =>
    {
        var found = names.Where(bones.ContainsKey).Select(n => bones[n]).ToList();
        return found.Count == 0 ? null : found.Aggregate(Vector3.Zero, (a, b) => a + b) / found.Count;
    };

    private static void Near(Vector3 expected, Vector3 actual)
        => Assert.True(Vector3.Distance(expected, actual) < 0.01f, $"expected {expected}, got {actual}");

    private static IReadOnlyList<string> AimAt(Vector3 shot, Dictionary<string, Vector3>? bones = null)
        => ShotAim.Order(shot, ShotAim.PartPoints(Lookup(bones ?? Receiver), ShotAim.Facing(0)), CummyParts.All);

    [Fact] public void InFrontOfTheHipsIsTheGroin() => Assert.Equal("gr", AimAt(new(0, 0.88f, 0.2f))[0]);
    [Fact] public void BehindTheHipsIsTheButt() => Assert.Equal("bt", AimAt(new(0, 0.9f, -0.25f))[0]);
    [Fact] public void FaceHeight() => Assert.Equal("mo", AimAt(new(0, 1.55f, 0.3f))[0]);
    [Fact] public void FrontOfTheChest() => Assert.Equal("ch", AimAt(new(0, 1.3f, 0.3f))[0]);
    [Fact] public void BehindTheChestIsTheBack() => Assert.Equal("bk", AimAt(new(0, 1.25f, -0.3f))[0]);
    [Fact] public void FrontOfTheLeftThigh() => Assert.Equal("ll", AimAt(new(-0.1f, 0.65f, 0.25f))[0]);
    [Fact] public void BackOfTheRightThigh() => Assert.Equal("br", AimAt(new(0.1f, 0.65f, -0.25f))[0]);

    [Fact]
    public void NearbyPartsComeNext()
    {
        var order = AimAt(new(0, 0.88f, 0.2f));
        Assert.Contains(order[1], new[] { "st", "ll", "lr", "bt" });
        Assert.Equal(CummyParts.All.Count, order.Count);
        Assert.Equal(CummyParts.All.Count, order.Distinct().Count());
    }

    [Fact]
    public void FacingTurnsTheBody()
    {
        // Turned around (facing -Z), a shot from +Z hits the butt instead of the groin. The bones turn with the body.
        var turned = Turned(Receiver, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI));
        var points = ShotAim.PartPoints(Lookup(turned), ShotAim.Facing(MathF.PI));
        Assert.Equal("bt", ShotAim.Order(new(0, 0.9f, 0.25f), points, CummyParts.All)[0]);
    }

    [Fact]
    public void YawAloneTurnsABodyWithoutAFrame()
    {
        // Without the bones a frame is read from, the model's facing still turns the parts.
        var bones = new Dictionary<string, Vector3>(Receiver);
        bones.Remove("j_kubi");
        var points = ShotAim.PartPoints(Lookup(bones), ShotAim.Facing(MathF.PI));
        Assert.Equal("bt", ShotAim.Order(new(0, 0.9f, 0.25f), points, CummyParts.All)[0]);
    }

    [Fact]
    public void IvcsBonesWinOverVanilla()
    {
        var giver = new Dictionary<string, Vector3> { ["j_kosi"] = new(0, 1, 0), ["iv_ochinko_f"] = new(0, 0.9f, 0.3f) };
        Assert.Equal(new Vector3(0, 0.9f, 0.3f), ShotAim.GiverPoint(Lookup(giver), ShotAim.Facing(0)));
        var vagina = new Dictionary<string, Vector3> { ["j_kosi"] = new(0, 1, 0), ["iv_omanko"] = new(0, 0.9f, 0.05f) };
        Assert.Equal(new Vector3(0, 0.9f, 0.05f), ShotAim.GiverPoint(Lookup(vagina), ShotAim.Facing(0)));
    }

    [Fact]
    public void VanillaGiverShootsFromInFrontOfTheHips()
    {
        var giver = new Dictionary<string, Vector3> { ["j_kosi"] = new(0, 1, 0) };
        var at = ShotAim.GiverPoint(Lookup(giver), ShotAim.Facing(0))!.Value;
        Assert.True(at.Z > 0.1f && at.Y < 1);
    }

    [Fact]
    public void NoBonesNoAim() => Assert.Null(ShotAim.GiverPoint(Lookup([]), ShotAim.Facing(0)));

    // ── Body frame ──────────────────────────────────────────────────────────

    [Fact]
    public void StandingFrameMatchesTheFacing()
    {
        var frame = ShotAim.BodyFrame(Lookup(Receiver), ShotAim.Facing(0));
        Assert.True(frame.FromBones);
        Near(Vector3.UnitZ, frame.Forward);
        Near(Vector3.UnitY, frame.Up);
    }

    [Fact]
    public void LyingOnTheBackFacesUp()
    {
        var frame = ShotAim.BodyFrame(Lookup(Turned(Receiver, OnBack)), ShotAim.Facing(0));
        Near(Vector3.UnitY, frame.Forward);
        Near(-Vector3.UnitZ, frame.Up);
    }

    [Fact]
    public void LyingFaceDownFacesTheFloor()
        => Near(-Vector3.UnitY, ShotAim.BodyFrame(Lookup(Turned(Receiver, FaceDown)), ShotAim.Facing(0)).Forward);

    [Fact]
    public void BentOverFacesDownAndForward()
    {
        var bent = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 3);
        Near(Vector3.Transform(Vector3.UnitZ, bent), ShotAim.BodyFrame(Lookup(Turned(Receiver, bent)), ShotAim.Facing(0)).Forward);
    }

    [Fact]
    public void WithoutTheNeckItsTheFacing()
    {
        var bones = new Dictionary<string, Vector3>(Receiver);
        bones.Remove("j_kubi");
        var frame = ShotAim.BodyFrame(Lookup(bones), ShotAim.Facing(MathF.PI / 2));
        Assert.False(frame.FromBones);
        Near(ShotAim.Facing(MathF.PI / 2), frame.Forward);
        Near(Vector3.UnitY, frame.Up);
    }

    [Fact]
    public void ShouldersAlongTheSpineGiveNoFrame()
    {
        var bones = new Dictionary<string, Vector3>(Receiver) { ["j_sako_l"] = new(0, 1.0f, 0.01f), ["j_sako_r"] = new(0, 1.4f, 0) };
        Assert.False(ShotAim.BodyFrame(Lookup(bones), ShotAim.Facing(0)).FromBones);
    }

    [Fact]
    public void SwappedSidesStillFaceOutOfTheChest()
    {
        var bones = new Dictionary<string, Vector3>(Receiver) { ["j_sako_l"] = Receiver["j_sako_r"], ["j_sako_r"] = Receiver["j_sako_l"] };
        Near(Vector3.UnitZ, ShotAim.BodyFrame(Lookup(bones), ShotAim.Facing(0)).Forward);
    }

    [Fact]
    public void WithoutAChestTheLipsSetTheFront()
    {
        var bones = Turned(Receiver, OnBack);
        bones.Remove("j_mune_l");
        bones.Remove("j_mune_r");
        Near(Vector3.UnitY, ShotAim.BodyFrame(Lookup(bones), ShotAim.Facing(0)).Forward);
    }

    // ── Offsets in the body frame ───────────────────────────────────────────

    private static IReadOnlyList<string> AimAtTurned(Vector3 shot, Quaternion turn)
        => ShotAim.Order(shot, ShotAim.PartPoints(Lookup(Turned(Receiver, turn)), ShotAim.Facing(0)), CummyParts.All);

    [Fact]
    public void AboveABodyOnItsBackIsTheChest() => Assert.Equal("ch", AimAtTurned(new(0, 1.4f, -0.35f), OnBack)[0]);

    [Fact]
    public void UnderABodyOnItsBackIsTheBack() => Assert.Equal("bk", AimAtTurned(new(0, 0.75f, -0.35f), OnBack)[0]);

    [Fact]
    public void AboveABodyFaceDownIsTheBack() => Assert.Equal("bk", AimAtTurned(new(0, 1.4f, 0.35f), FaceDown)[0]);

    [Fact]
    public void UnderABodyFaceDownIsTheChest() => Assert.Equal("ch", AimAtTurned(new(0, 0.5f, 0.35f), FaceDown)[0]);

    [Fact]
    public void AVanillaGiverOnTheirBackShootsUpFromTheHips()
    {
        var giver = Turned(new Dictionary<string, Vector3>(Receiver), OnBack);
        var at = ShotAim.GiverPoint(Lookup(giver), ShotAim.Facing(0))!.Value;
        Assert.True(at.Y > Pelvis.Y + 0.1f, $"expected above the pelvis, got {at}");
    }

    // ── Mouth first ─────────────────────────────────────────────────────────

    [Fact]
    public void SixtyNineLandsOnTheMouth()
    {
        // The receiver on their back, the giver's vagina just above their lips.
        var lips = Turned(Receiver, OnBack)["j_f_ulip_01_l"];
        Assert.Equal("mo", AimAtTurned(lips + new Vector3(0, 0.06f, 0), OnBack)[0]);
    }

    [Fact]
    public void CloseToTheMouthBeatsANearerPart()
    {
        var points = new Dictionary<string, Vector3> { ["mo"] = new(0, 0, 0.10f), ["bk"] = new(0, 0, 0.03f), ["ch"] = new(0, 0, 0.3f) };
        var order = ShotAim.Order(Vector3.Zero, points, CummyParts.All);
        Assert.Equal(["mo", "bk", "ch"], order.Take(3));
        Assert.Equal(CummyParts.All.Count, order.Distinct().Count());
    }

    [Fact]
    public void FarFromTheMouthItsByDistance()
    {
        var points = new Dictionary<string, Vector3> { ["mo"] = new(0, 0, 0.13f), ["bk"] = new(0, 0, 0.03f) };
        Assert.Equal("bk", ShotAim.Order(Vector3.Zero, points, CummyParts.All)[0]);
    }

    [Fact]
    public void PartsWithoutBonesGoLast()
    {
        var order = AimAt(new(0, 0.88f, 0.2f), new Dictionary<string, Vector3> { ["j_kosi"] = new(0, 0.95f, 0) });
        Assert.Equal("gr", order[0]);
        Assert.Equal(CummyParts.All.Count, order.Count);
    }
}

public class AimedLayerTests
{
    private static EngineRig Scene()
    {
        var r = new EngineRig();
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.Level = 50;
        r.Run(11);
        return r;
    }

    [Fact]
    public void AFullMouthMovesToTheNextNearest()
    {
        var r = Scene();
        r.Host.Aim = ["mo", "bk", "ch"];
        r.Host.Outcomes.Enqueue("full");
        r.BobClimaxes();
        Assert.Equal(["mo", "bk"], r.Host.Layers.Select(l => l.Part));
    }

    [Fact]
    public void TheNearestPartIsLayered()
    {
        var r = Scene();
        r.Host.Aim = ["ch", "st", "mo"];
        r.BobClimaxes();
        Assert.Equal("ch", Assert.Single(r.Host.Layers).Part);
    }

    [Fact]
    public void AFullOrBlockedPartMovesToTheNextNearest()
    {
        var r = Scene();
        r.Host.Aim = ["ch", "st", "mo"];
        r.Host.Outcomes.Enqueue("full");
        r.Host.Outcomes.Enqueue("blocked");
        r.BobClimaxes();
        Assert.Equal(["ch", "st", "mo"], r.Host.Layers.Select(l => l.Part));
    }

    [Fact]
    public void WithoutBonesItsRandom()
    {
        var r = Scene();
        r.Host.Aim = null;
        r.BobClimaxes();
        Assert.Contains(Assert.Single(r.Host.Layers).Part, CummyParts.All);
    }
}

public class AnatomyTests
{
    /// An IVCS skeleton with both sets of genital bones, as some bodies carry whatever they show.
    private static readonly Dictionary<string, System.Numerics.Vector3> BothBones = new()
    {
        ["j_kosi"] = new(0, 1, 0),
        ["iv_ochinko_f"] = new(0, 0.9f, 0.3f),
        ["iv_omanko"] = new(0, 0.88f, 0.05f),
    };

    private static ShotAim.BoneLookup Lookup(Dictionary<string, System.Numerics.Vector3> bones) => names =>
        names.FirstOrDefault(bones.ContainsKey) is { } n ? bones[n] : null;

    [Theory]
    [InlineData(Anatomy.Vagina, 0.05f)]
    [InlineData(Anatomy.Penis, 0.3f)]
    [InlineData(Anatomy.Both, 0.3f)]
    [InlineData(Anatomy.NotSet, 0.3f)]
    public void TheGiversChoicePicksTheBone(Anatomy anatomy, float z)
        => Assert.Equal(z, ShotAim.GiverPoint(Lookup(BothBones), ShotAim.Facing(0), anatomy)!.Value.Z, 3);

    private static Dictionary<string, System.Numerics.Vector3> With(params (string Bone, System.Numerics.Vector3 At)[] extra)
    {
        var bones = new Dictionary<string, System.Numerics.Vector3>(BothBones);
        foreach (var (bone, at) in extra) bones[bone] = at;
        return bones;
    }

    [Fact]
    public void NotSetWithACollapsedPenisUsesTheVagina()
    {
        var bones = With(("iv_ochinko_a", new(0, 0.9f, 0.29f)));
        Assert.Equal(0.05f, ShotAim.GiverPoint(Lookup(bones), ShotAim.Facing(0), Anatomy.NotSet)!.Value.Z, 3);
    }

    [Fact]
    public void NotSetWithAFarPenisUsesTheVagina()
    {
        var bones = With(("iv_ochinko_f", new(0, 0.2f, 0.3f)));
        Assert.Equal(0.05f, ShotAim.GiverPoint(Lookup(bones), ShotAim.Facing(0), Anatomy.NotSet)!.Value.Z, 3);
    }

    [Fact]
    public void NotSetWithAShownPenisUsesTheTip()
    {
        var bones = With(("iv_ochinko_a", new(0, 0.92f, 0.08f)));
        Assert.Equal(0.3f, ShotAim.GiverPoint(Lookup(bones), ShotAim.Facing(0), Anatomy.NotSet)!.Value.Z, 3);
    }

    [Fact]
    public void NotSetWithAHiddenPenisAndNoVaginaKeepsTheTip()
    {
        var bones = With(("iv_ochinko_a", new(0, 0.9f, 0.29f)));
        bones.Remove("iv_omanko");
        Assert.Equal(0.3f, ShotAim.GiverPoint(Lookup(bones), ShotAim.Facing(0), Anatomy.NotSet)!.Value.Z, 3);
    }

    [Theory]
    [InlineData(Anatomy.Penis, 0.3f)]
    [InlineData(Anatomy.Both, 0.3f)]
    [InlineData(Anatomy.Vagina, 0.05f)]
    public void OnlyNotSetChecksForAHiddenPenis(Anatomy anatomy, float z)
    {
        var bones = With(("iv_ochinko_a", new(0, 0.9f, 0.29f)));
        Assert.Equal(z, ShotAim.GiverPoint(Lookup(bones), ShotAim.Facing(0), anatomy)!.Value.Z, 3);
    }

    [Fact]
    public void VaginaWithoutIvcsUsesTheHips()
    {
        var at = ShotAim.GiverPoint(Lookup(new() { ["j_kosi"] = new(0, 1, 0), ["iv_ochinko_f"] = new(0, 0.9f, 0.3f) }),
            ShotAim.Facing(0), Anatomy.Vagina)!.Value;
        Assert.Equal(0.12f, at.Z, 3);
    }

    [Theory]
    [InlineData(Anatomy.NotSet)]
    [InlineData(Anatomy.Penis)]
    [InlineData(Anatomy.Vagina)]
    [InlineData(Anatomy.Both)]
    public void TravelsInTheBodyTag(Anatomy anatomy)
    {
        var tag = new BodyTag(Anatomies.Flags(anatomy) | BodyFlags.CanCarry, PregnancyStage.None, null);
        Assert.Equal(anatomy, BodyTag.Parse(tag.Format())!.Value.Anatomy);
    }

    [Fact]
    public void PublishedEvenWithPregnancyOff()
    {
        var r = new EngineRig { Playing = false };
        r.Save.Anatomy = Anatomy.Vagina;
        r.Run(1);
        Assert.Equal("P1|16|0|-", r.Host.BodyTags[^1]);
        r.Engine.SetAnatomy(Anatomy.Both);
        r.Run(1);
        Assert.Equal("P1|24|0|-", r.Host.BodyTags[^1]);
    }

    [Fact]
    public void ThePartnersAnatomyIsUsedToAim()
    {
        var r = new EngineRig();
        r.Save.Level = 50;
        r.BobBody = new BodyTag(BodyFlags.Vagina, PregnancyStage.None, null);
        r.Run(11);
        r.BobClimaxes();
        Assert.Equal(Anatomy.Vagina, r.Host.AimedFrom);
    }

    [Fact]
    public void ResetKeepsAnatomy()
    {
        var save = new CharacterSave { Anatomy = Anatomy.Penis };
        save.ResetProgress(EngineRig.T0);
        Assert.Equal(Anatomy.Penis, save.Anatomy);
    }
}
