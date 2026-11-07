using Corruption.Core;

namespace Corruption.Tests;

public class PlateComposerTests
{
    private static readonly PlateValues Curious = new(12, 72);

    [Fact]
    public void DefaultTexts()
    {
        Assert.Equal("Curious ♥ Lv. 12", PlateComposer.Render(PlateComposer.DefaultTemplate, Curious));
        Assert.Equal("♥ 72%", PlateComposer.Render(PlateComposer.DefaultActTemplate, Curious));
    }

    [Theory]
    [InlineData("{tier} ♥{arousal}")]
    [InlineData("{tier}")]
    [InlineData("{tier} Lv {level}")]
    [InlineData("{tier} ♥ {level}")]
    public void OldDefaultsUpgrade(string old) => Assert.Equal(PlateComposer.DefaultTemplate, PlateComposer.UpgradeTemplate(old));

    [Fact]
    public void EditedTextIsLeftAlone() => Assert.Equal("{tier}!", PlateComposer.UpgradeTemplate("{tier}!"));

    [Fact]
    public void AllTokens() => Assert.Equal("Curious 12 72", PlateComposer.Render("{tier} {level} {arousal}", Curious));

    [Theory]
    [InlineData(0, "♡♡♡♡♡")]
    [InlineData(19, "♡♡♡♡♡")]
    [InlineData(20, "♥♡♡♡♡")]
    [InlineData(60, "♥♥♥♡♡")]
    [InlineData(100, "♥♥♥♥♥")]
    public void ArousalBar(double arousal, string bar) => Assert.Equal(bar, PlateComposer.ArousalBar(arousal));

    [Fact]
    public void UnknownTokenRendersAsWrittenAndIsFlagged()
    {
        Assert.Equal("Curious {mood}", PlateComposer.Render("{tier} {mood}", Curious));
        Assert.Equal(["mood"], PlateComposer.UnknownTokens("{tier} {mood}"));
        Assert.Empty(PlateComposer.UnknownTokens(PlateComposer.DefaultTemplate));
    }

    [Fact]
    public void GlowStartsAtEighty()
    {
        Assert.Null(PlateComposer.Compose("x", Curious with { Arousal = 79.9 }).Glow);
        Assert.NotNull(PlateComposer.Compose("x", Curious with { Arousal = 80 }).Glow);
    }

    [Fact]
    public void ColourShiftsTowardRed()
    {
        var low = PlateComposer.Compose("x", Curious with { Arousal = 0 }).Color;
        var high = PlateComposer.Compose("x", Curious with { Arousal = 100 }).Color;
        Assert.Equal(PlateComposer.TierColor(Tier.Curious), low);
        Assert.True(high.X > high.Z && high.Y < low.Y);
    }
}

public class PlateLayoutTests
{
    [Fact]
    public void BottomAlignedTextTallerThanItsBoxGrowsAboveTheBox()
        // A 20-tall box at y=100 holding 40 of text: the text starts at 80, not at the box's top.
        => Assert.Equal(80f, PlateLayout.DrawnTop(100, 20, 7, 40, 2, 10));

    [Fact]
    public void BottomAlignedShortTextStartsInsideTheBox()
        => Assert.Equal(180f, PlateLayout.DrawnTop(100, 100, 7, 20, 1, 10));

    [Fact]
    public void TopAlignedTextStartsAtTheBoxTop()
        => Assert.Equal(100f, PlateLayout.DrawnTop(100, 100, 1, 40, 2, 10));

    [Fact]
    public void MiddleAlignedTextIsCentred()
        => Assert.Equal(130f, PlateLayout.DrawnTop(100, 100, 4, 40, 2, 10));

    [Fact]
    public void AMeasureThatMissesALineStillClearsBothLines()
        => Assert.True(PlateLayout.DrawnTop(100, 20, 7, 20, 2, 20) <= 74f);

    [Fact]
    public void CountsLines()
    {
        Assert.Equal(1, PlateLayout.CountLines("Rayla Velvet"u8));
        Assert.Equal(2, PlateLayout.CountLines("Rayla Velvet\r«Kitty»"u8));
        Assert.Equal(2, PlateLayout.CountLines([0x41, 0x02, 0x10, 0x01, 0x03, 0x42]));
        Assert.Equal(1, PlateLayout.CountLines([]));
    }
}

public class PlateCycleTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly PlateTexts Texts = new("steady", "act", "climax", 3, 15);
    private static readonly PlateValues Values = new(12, 40);
    private static readonly PlateValues NoArousal = new(12, 0);

    private static string At(DateTime now, PlateScene scene) => PlateComposer.ComposeNow(now, Texts, Values, scene).Text;

    [Fact]
    public void SteadyWithNoActAndNoArousal()
    {
        for (var s = 0; s < 30; s++)
            Assert.Equal("steady", PlateComposer.ComposeNow(T0.AddSeconds(s), Texts, NoArousal, new PlateScene(false, null)).Text);
    }

    [Fact]
    public void LeftoverArousalCyclesOutsideAnAct()
    {
        var seen = Enumerable.Range(0, 12).Select(s => At(T0.AddSeconds(s), new PlateScene(false, null))).ToList();
        Assert.Contains("act", seen);
        Assert.Contains("steady", seen);
    }

    [Fact]
    public void AnActCyclesEvenAtZeroArousal()
    {
        var seen = Enumerable.Range(0, 12).Select(s => PlateComposer.ComposeNow(T0.AddSeconds(s), Texts, NoArousal, new PlateScene(true, null)).Text).ToList();
        Assert.Contains("act", seen);
    }

    [Fact]
    public void ArousalBelowOneCountsAsNone()
        => Assert.Equal("steady", PlateComposer.ComposeNow(T0, Texts, new PlateValues(12, 0.9), new PlateScene(false, null)).Text);

    [Fact]
    public void DuringAnActItCyclesEveryFewSeconds()
    {
        var seen = Enumerable.Range(0, 12).Select(s => At(T0.AddSeconds(s), new PlateScene(true, null))).ToList();
        Assert.Contains("act", seen);
        Assert.Contains("steady", seen);
        for (var s = 0; s + 3 < seen.Count; s += 3) Assert.NotEqual(seen[s], seen[s + 3]);
        Assert.Equal(seen[0], seen[2]);
    }

    [Fact]
    public void ClimaxShowsFirstThenCyclesWithArousal()
    {
        var scene = new PlateScene(true, T0);
        Assert.Equal("climax", At(T0, scene));
        Assert.Equal("climax", At(T0.AddSeconds(2), scene));
        Assert.Equal("act", At(T0.AddSeconds(3), scene));
        Assert.Equal("climax", At(T0.AddSeconds(6), scene));
    }

    [Fact]
    public void ClimaxTextGlowsWhateverTheArousal()
        => Assert.NotNull(PlateComposer.ComposeNow(T0, Texts, Values, new PlateScene(true, T0)).Glow);

    [Fact]
    public void ClimaxEndsAfterItsTime()
        => Assert.Equal("steady", PlateComposer.ComposeNow(T0.AddSeconds(16), Texts, NoArousal, new PlateScene(false, T0)).Text);

    [Fact]
    public void ClimaxShowsEvenIfTheActJustEnded()
        => Assert.Equal("climax", At(T0.AddSeconds(1), new PlateScene(false, T0)));
}
