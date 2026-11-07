using System.Numerics;
using Corruption.Core;

namespace Corruption.Tests;

public class PlateTitleTests
{
    [Theory]
    [InlineData("insatiable", "Insatiable")]
    [InlineData(null, "")]
    [InlineData("from_a_newer_version", "")]
    public void TheTitleToken(string? id, string shown)
        => Assert.Equal($"[{shown}]", PlateComposer.Render("[{title}]", new PlateValues(30, 0, 0, id)));

    [Fact]
    public void TheTitleTokenIsKnown() => Assert.Empty(PlateComposer.UnknownTokens("{title}"));

    [Fact]
    public void APlateCarriesItsTitleLine()
    {
        var plate = PlateComposer.Compose("{tier}", new PlateValues(30, 0, 0, "insatiable"));
        Assert.Equal(new PlateTitle("Insatiable", PlateComposer.TitleColor(TitleCatalog.Find("insatiable")!)), plate.Title);
        Assert.Null(PlateComposer.Compose("{tier}", new PlateValues(30, 0)).Title);
        Assert.Null(PlateComposer.Compose("{tier}", new PlateValues(30, 0, 0, "from_a_newer_version")).Title);
    }

    [Fact]
    public void TheTitleDoesntGlowOrRedden()
    {
        var calm = PlateComposer.Compose("{tier}", new PlateValues(30, 0, 0, "shop_harlot"));
        var hot = PlateComposer.Compose("{tier}", new PlateValues(30, 90, 0, "shop_harlot"));
        Assert.NotNull(hot.Glow);
        Assert.Equal(calm.Title, hot.Title);
    }

    [Fact]
    public void TitleOnlyHasNoText()
    {
        var plate = PlateComposer.TitleOnly(new PlateValues(30, 0, 0, "devout"))!;
        Assert.Equal("", plate.Text);
        Assert.Equal("Devout", plate.Title!.Text);
        Assert.Null(PlateComposer.TitleOnly(new PlateValues(30, 0)));
    }

    [Fact]
    public void ColoursFollowTheSource()
    {
        Vector3 Of(string id) => PlateComposer.TitleColor(TitleCatalog.Find(id)!);
        Assert.Equal(PlateComposer.TierColor(Progression.TierOf(50)), Of("lv_50"));
        Assert.Equal(Of("insatiable"), Of("devout"));
        Assert.Equal(Of("marathon"), Of("moon_touched"));
        Assert.Equal(Of("shop_tease"), Of("shop_harlot"));
        var distinct = new[] { Of("star_1"), Of("insatiable"), Of("marathon"), Of("shop_tease"), Of(TitleCatalog.Unholy) };
        Assert.Equal(distinct.Length, distinct.Distinct().Count());
    }

    [Fact]
    public void OwnAndOthersPlatesCarryTheirTitles()
    {
        var r = new EngineRig { Playing = false, BobTitle = "sire" };
        r.Save.Titles.Unlocked.Add("devout");
        r.Save.Titles.Equipped = "devout";
        r.Run(1);
        Assert.Equal("devout", r.Engine.OwnPlate.TitleId);
        Assert.Equal("sire", r.Engine.PlateOf(r.Bob)!.Value.TitleId);
    }

    [Theory]
    [InlineData(2, 100, 2)]
    [InlineData(1, 30, 1)]
    public void DrawnBottomIsTheTopPlusTheText(int alignment, float boxHeight, int lines)
    {
        var top = PlateLayout.DrawnTop(100, boxHeight, alignment, 20, lines, 18);
        var bottom = PlateLayout.DrawnBottom(100, boxHeight, alignment, 20, lines, 18);
        Assert.Equal(MathF.Max(20, lines * 18 * 1.15f), bottom - top, 3);
    }

    [Fact]
    public void BottomAlignedTextEndsAtTheBoxBottom()
        => Assert.Equal(130, PlateLayout.DrawnBottom(100, 30, 6, 20, 1, 18), 3);
}

