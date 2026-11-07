using Corruption.Core;

namespace Corruption.Tests;

public class TitleTagTests
{
    [Theory]
    [InlineData("insatiable")]
    [InlineData(null)]
    [InlineData("from_a_newer_version")]
    public void RoundTrips(string? id)
    {
        var tag = new TitleTag(id);
        Assert.Equal(tag, TitleTag.Parse(tag.Format()));
    }

    [Fact]
    public void LooksLikeTheOtherTags()
    {
        Assert.Equal("T1|insatiable", new TitleTag("insatiable").Format());
        Assert.Equal("T1|-", new TitleTag(null).Format());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("T1")]
    [InlineData("T2|insatiable")]
    [InlineData("T1|insatiable|extra")]
    [InlineData("T1|Not Valid")]
    [InlineData("T1|")]
    [InlineData("C1|50.0|12|70|3|0")]
    public void RejectsMalformedTags(string? text) => Assert.Null(TitleTag.Parse(text));

    private static EngineRig Rig()
    {
        var r = new EngineRig();
        r.Save.Titles.Unlocked.UnionWith(["insatiable", "devout"]);
        r.Save.Titles.Initialized = true;
        r.Playing = false;
        return r;
    }

    [Fact]
    public void IsPublishedOnceAndAgainOnlyOnAChange()
    {
        var r = Rig();
        r.Run(3);
        Assert.Equal(["T1|-"], r.Host.TitleTags);

        Assert.Null(r.Engine.TryEquipTitle("insatiable", r.Now));
        r.Run(3);
        Assert.Equal(["T1|-", "T1|insatiable"], r.Host.TitleTags);
    }

    [Fact]
    public void EquippingDoesntRewriteTheOtherTags()
    {
        var r = Rig();
        r.Run(3);
        var tags = r.Host.Tags.Count;
        var extras = r.Host.ExtraTags.Count;
        var bodies = r.Host.BodyTags.Count;
        r.Engine.TryEquipTitle("devout", r.Now);
        r.Run(3);
        Assert.Equal((tags, extras, bodies), (r.Host.Tags.Count, r.Host.ExtraTags.Count, r.Host.BodyTags.Count));
    }

    [Fact]
    public void HidingRemovesAllFourTags()
    {
        var r = Rig();
        r.Run(2);
        r.Engine.SetHidden(true);
        r.Run(2);
        Assert.Null(r.Host.Tags[^1]);
        Assert.Null(r.Host.ExtraTags[^1]);
        Assert.Null(r.Host.BodyTags[^1]);
        Assert.Null(r.Host.TitleTags[^1]);
    }

    [Fact]
    public void NoTagBeforeSetup()
    {
        var r = Rig();
        r.Save.Setup = null;
        r.Run(3);
        Assert.Empty(r.Host.TitleTags);
    }

    [Fact]
    public void IsWrittenAgainWhenAskedToRepublish()
    {
        var r = Rig();
        r.Run(2);
        r.Engine.RepublishTag();
        r.Run(1);
        Assert.Equal(["T1|-", "T1|-"], r.Host.TitleTags);
    }

    [Fact]
    public void ANearbyPlayersTitleIsRead()
    {
        var r = Rig();
        r.BobTitle = "sire";
        r.Run(1);
        Assert.Equal("sire", r.Engine.TitleOf(r.Bob));
        r.BobTitle = null;
        r.Run(1);
        Assert.Null(r.Engine.TitleOf(r.Bob));
    }
}
