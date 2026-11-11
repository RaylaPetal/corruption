using System.Numerics;
using Corruption.Core;

namespace Corruption.Tests;

public class CreampieAimTests
{
    private static readonly Vector3 PussyAt = new(0, 0.88f, 0.05f);
    private static readonly Vector3 AssAt = new(0, 0.9f, -0.06f);

    private static ShotAim.BoneLookup Lookup(Dictionary<string, Vector3> bones) => names =>
    {
        var found = names.Where(bones.ContainsKey).Select(n => bones[n]).ToList();
        return found.Count == 0 ? null : found.Aggregate(Vector3.Zero, (a, b) => a + b) / found.Count;
    };

    private static readonly ShotAim.BoneLookup Receiver = Lookup(new() { ["iv_omanko"] = PussyAt, ["iv_koumon"] = AssAt, ["j_kosi"] = new(0, 0.95f, 0) });

    private static ShotAim.BoneLookup Tip(Vector3 at) => Lookup(new() { ["iv_ochinko_f"] = at, ["iv_ochinko_a"] = at + new Vector3(0, 0, 0.15f) });

    private static string? Inside(Vector3 tip, Anatomy giver = Anatomy.Penis, Anatomy receiver = Anatomy.Vagina, ShotAim.BoneLookup? to = null)
        => CreampieAim.Inside(Tip(tip), to ?? Receiver, giver, receiver);

    [Fact] public void TipAtThePussyIsInside() => Assert.Equal(CreampieAim.Pussy, Inside(PussyAt + new Vector3(0, 0, 0.03f)));

    [Fact] public void AssNearerThanPussy() => Assert.Equal(CreampieAim.Ass, Inside(AssAt + new Vector3(0, 0, -0.04f)));

    [Fact] public void BothOutOfReachIsOutside() => Assert.Null(Inside(PussyAt + new Vector3(0, 0, 0.2f)));

    [Fact] public void JustOutsideTheRadius() => Assert.Null(Inside(PussyAt + new Vector3(0, 0, ShotAim.InsideRadius + 0.01f)));

    [Fact] public void VaginaGiverNeverCreampies() => Assert.Null(Inside(PussyAt, giver: Anatomy.Vagina));

    [Fact] public void PenisReceiverOnlyHasTheAss()
    {
        Assert.Null(Inside(PussyAt, receiver: Anatomy.Penis));
        Assert.Equal(CreampieAim.Ass, Inside(AssAt, receiver: Anatomy.Penis));
    }

    [Fact] public void NotSetGiverWithTipBones() => Assert.Equal(CreampieAim.Pussy, Inside(PussyAt, giver: Anatomy.NotSet));

    [Fact]
    public void NotSetGiverWithoutTipBones()
    {
        var hipsOnly = Lookup(new() { ["j_kosi"] = PussyAt });
        Assert.Null(CreampieAim.Inside(hipsOnly, Receiver, Anatomy.NotSet, Anatomy.Vagina));
        Assert.Empty(CreampieAim.Holes(Anatomy.Vagina, Anatomy.NotSet, giverHasPenisBones: false));
    }

    [Fact] public void ShaftAloneIsNeverInside()
    {
        var shaftOnly = Lookup(new() { ["iv_ochinko_e"] = PussyAt });
        Assert.Null(CreampieAim.Inside(shaftOnly, Receiver, Anatomy.Penis, Anatomy.Vagina));
    }

    [Fact]
    public void MissingHoleBonesIsOutside()
    {
        var vanilla = Lookup(new() { ["j_kosi"] = PussyAt });
        Assert.Null(Inside(PussyAt, to: vanilla));
    }

    [Fact] public void HoleParts()
    {
        Assert.Equal("gr", CreampieAim.PartOf(CreampieAim.Pussy));
        Assert.Equal("bt", CreampieAim.PartOf(CreampieAim.Ass));
    }
}
