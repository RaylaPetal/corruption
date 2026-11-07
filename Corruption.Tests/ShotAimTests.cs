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
        ["j_kosi"] = new(0, 0.95f, 0), ["j_shiri_l"] = new(-0.08f, 0.9f, -0.08f), ["j_shiri_r"] = new(0.08f, 0.9f, -0.08f),
        ["j_ude_a_l"] = new(-0.25f, 1.3f, 0), ["j_ude_b_l"] = new(-0.3f, 1.1f, 0),
        ["j_ude_a_r"] = new(0.25f, 1.3f, 0), ["j_ude_b_r"] = new(0.3f, 1.1f, 0),
        ["j_te_l"] = new(-0.32f, 0.9f, 0), ["j_te_r"] = new(0.32f, 0.9f, 0),
        ["j_asi_a_l"] = new(-0.1f, 0.8f, 0), ["j_asi_b_l"] = new(-0.1f, 0.5f, 0),
        ["j_asi_a_r"] = new(0.1f, 0.8f, 0), ["j_asi_b_r"] = new(0.1f, 0.5f, 0),
        ["j_asi_d_l"] = new(-0.1f, 0.1f, 0), ["j_asi_e_l"] = new(-0.1f, 0.02f, 0.08f),
        ["j_asi_d_r"] = new(0.1f, 0.1f, 0), ["j_asi_e_r"] = new(0.1f, 0.02f, 0.08f),
    };

    private static ShotAim.BoneLookup Lookup(Dictionary<string, Vector3> bones) => names =>
    {
        var found = names.Where(bones.ContainsKey).Select(n => bones[n]).ToList();
        return found.Count == 0 ? null : found.Aggregate(Vector3.Zero, (a, b) => a + b) / found.Count;
    };

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
        // Turned around (facing -Z), a shot from +Z hits the butt instead of the groin.
        var points = ShotAim.PartPoints(Lookup(Receiver), ShotAim.Facing(MathF.PI));
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
