using Corruption.Core;

namespace Corruption.Tests;

public class BodyTagTests
{
    [Theory]
    [InlineData(BodyFlags.None, PregnancyStage.None, null)]
    [InlineData(BodyFlags.CanCarry | BodyFlags.CanBreed, PregnancyStage.Second, "a1b2c3d4")]
    [InlineData(BodyFlags.CanBreed | BodyFlags.Virile, PregnancyStage.Recovering, null)]
    public void RoundTrips(BodyFlags flags, PregnancyStage stage, string? father)
    {
        var tag = new BodyTag(flags, stage, father);
        Assert.Equal(tag, BodyTag.Parse(tag.Format()));
    }

    [Fact] public void EmptyFormat() => Assert.Equal("P1|0|0|-", BodyTag.Empty.Format());

    [Theory]
    [InlineData(null)]
    [InlineData("P1|0|0")]
    [InlineData("P2|0|0|-")]
    [InlineData("P1|1024|0|-")]
    [InlineData("P1|0|5|-")]
    [InlineData("P1|-1|0|-")]
    [InlineData("P1|0|0|XYZ")]
    [InlineData("E1|0|-")]
    public void Malformed(string? text) => Assert.Null(BodyTag.Parse(text));

    [Fact]
    public void PublishedWithPregnancyOffAndOnlyOnChange()
    {
        var r = new EngineRig { Playing = false };
        r.Run(20);
        Assert.Equal(["P1|0|0|-"], r.Host.BodyTags);
    }

    [Fact]
    public void AllThreeRemovedWhenHidden()
    {
        var r = new EngineRig { Playing = false };
        r.Run(1);
        r.Engine.SetHidden(true);
        r.Run(1);
        Assert.Null(r.Host.Tags[^1]);
        Assert.Null(r.Host.ExtraTags[^1]);
        Assert.Null(r.Host.BodyTags[^1]);
    }

    [Fact]
    public void NoneBeforeSetup()
    {
        var r = new EngineRig { Playing = false };
        r.Save.Setup = null;
        r.Run(3);
        Assert.Empty(r.Host.BodyTags);
    }

    [Fact]
    public void ReadIntoTheEngine()
    {
        var r = new EngineRig { BobBody = new BodyTag(BodyFlags.CanBreed, PregnancyStage.None, null) };
        r.Run(1);
        Assert.True(r.Engine.BodyOf(r.Bob)!.Value.CanBreed);
        r.BobBody = null;
        r.Run(1);
        Assert.Null(r.Engine.BodyOf(r.Bob));
    }
}
