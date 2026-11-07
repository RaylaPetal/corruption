using Corruption.Core;

namespace Corruption.Tests;

public class PresenceTagTests
{
    [Theory]
    [InlineData(50u, 0, 12, 70, 3, false)]
    [InlineData(2u, 3, 100, 0, 0, true)]
    public void RoundTripsWithAnAct(uint mode, byte variant, int level, int arousal, int climaxes, bool paused)
    {
        var tag = new PresenceTag(new PoseKey(mode, variant), level, arousal, climaxes, paused);
        Assert.Equal(tag, PresenceTag.Parse(tag.Format()));
    }

    [Fact]
    public void RoundTripsWithNoAct()
    {
        var tag = new PresenceTag(null, 5, 10, 0, false);
        Assert.Equal(tag, PresenceTag.Parse(tag.Format()));
    }

    [Fact]
    public void IsShort() => Assert.True(new PresenceTag(new PoseKey(65535, 6), 100, 100, 9999, true).Format().Length < 40);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("C2|50.0|12|70|3|0")]
    [InlineData("C1|50.0|12|70|3")]
    [InlineData("C1|50.0|12|70|3|0|x")]
    [InlineData("C1|50|12|70|3|0")]
    [InlineData("C1|x.0|12|70|3|0")]
    [InlineData("C1|70000.0|12|70|3|0")]
    [InlineData("C1|50.0|1000|70|3|0")]
    [InlineData("C1|50.0|12|101|3|0")]
    [InlineData("C1|50.0|12|-1|3|0")]
    [InlineData("C1|50.0|12|70|3|2")]
    public void AnythingElseIsNotATag(string? text) => Assert.Null(PresenceTag.Parse(text));
}

public class TagPublisherTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly PoseKey PoseA = new(50, 0);
    private static readonly PoseKey PoseB = new(51, 0);

    private static DateTime At(double seconds) => T0.AddSeconds(seconds);

    [Fact]
    public void TheFirstUpdatePublishesSoOthersCanFindThisCharacter()
    {
        var tag = new TagPublisher().Update(T0, null, 5, 0, 0, false);
        Assert.NotNull(tag);
        Assert.Null(tag!.Value.Pose);
    }

    [Fact]
    public void NothingChangingPublishesNothing()
    {
        var p = new TagPublisher();
        p.Update(T0, null, 5, 0, 0, false);
        for (var s = 1; s < 600; s++) Assert.Null(p.Update(At(s), null, 5, 0, 0, false));
    }

    [Fact]
    public void AnActGoesOutOnceStableForThreeSeconds()
    {
        var p = new TagPublisher();
        p.Update(T0, null, 5, 0, 0, false);
        Assert.Null(p.Update(At(5), PoseA, 5, 0, 0, false));
        Assert.Null(p.Update(At(7), PoseA, 5, 0, 0, false));
        Assert.Equal(PoseA, p.Update(At(8), PoseA, 5, 0, 0, false)!.Value.Pose);
        Assert.Null(p.Update(At(9), PoseA, 5, 0, 0, false));
    }

    [Fact]
    public void QuickCyclingPublishesOnlyWhatSettles()
    {
        var p = new TagPublisher();
        p.Update(T0, null, 5, 0, 0, false);
        for (var s = 5; s < 20; s++) Assert.Null(p.Update(At(s), s % 2 == 0 ? PoseA : PoseB, 5, 0, 0, false));
        Assert.Null(p.Update(At(20), PoseB, 5, 0, 0, false));
        Assert.Null(p.Update(At(21), PoseB, 5, 0, 0, false));
        Assert.Equal(PoseB, p.Update(At(22), PoseB, 5, 0, 0, false)!.Value.Pose);
    }

    [Fact]
    public void ABriefGapBetweenEmotesPublishesNothing()
    {
        var p = new TagPublisher();
        p.Update(T0, PoseA, 5, 0, 0, false);
        p.Update(At(3), PoseA, 5, 0, 0, false);
        Assert.Null(p.Update(At(10), null, 5, 0, 0, false));
        Assert.Null(p.Update(At(11), PoseA, 5, 0, 0, false));
        Assert.Null(p.Update(At(20), PoseA, 5, 0, 0, false));
    }

    [Fact]
    public void AnActEndingGoesOutAfterTwoSeconds()
    {
        var p = new TagPublisher();
        p.Update(T0, PoseA, 5, 0, 0, false);
        p.Update(At(3), PoseA, 5, 0, 0, false);
        Assert.Null(p.Update(At(10), null, 5, 0, 0, false));
        Assert.Null(p.Update(At(11), null, 5, 0, 0, false));
        var tag = p.Update(At(12), null, 5, 0, 0, false);
        Assert.NotNull(tag);
        Assert.Null(tag!.Value.Pose);
    }

    [Fact]
    public void SmallArousalChangesPublishNothing()
    {
        var p = new TagPublisher();
        p.Update(T0, null, 5, 51, 0, false);
        Assert.Null(p.Update(At(600), null, 5, 59, 0, false));
    }

    [Fact]
    public void ANewArousalStepWaitsForTheGap()
    {
        var p = new TagPublisher();
        p.Update(T0, null, 5, 51, 0, false);
        Assert.Null(p.Update(At(29), null, 5, 61, 0, false));
        Assert.Equal(61, p.Update(At(30), null, 5, 61, 0, false)!.Value.Arousal);
    }

    [Fact]
    public void ReachingZeroArousalGoesOut()
    {
        var p = new TagPublisher();
        p.Update(T0, null, 5, 9, 0, false);
        Assert.Null(p.Update(At(29), null, 5, 0, 0, false));
        Assert.Equal(0, p.Update(At(30), null, 5, 0, 0, false)!.Value.Arousal);
    }

    [Fact]
    public void LeavingZeroArousalGoesOut()
    {
        var p = new TagPublisher();
        p.Update(T0, null, 5, 0, 0, false);
        Assert.Equal(3, p.Update(At(30), null, 5, 3, 0, false)!.Value.Arousal);
    }

    [Fact]
    public void ALevelChangeGoesOut()
    {
        var p = new TagPublisher();
        p.Update(T0, null, 5, 0, 0, false);
        Assert.Equal(6, p.Update(At(2), null, 6, 0, 0, false)!.Value.Level);
    }

    [Fact]
    public void AClimaxGoesOutAtOnceWithItsArousal()
    {
        var p = new TagPublisher();
        p.Update(T0, null, 5, 99, 0, false);
        var tag = p.Update(At(0.5), null, 5, 20, 1, false);
        Assert.Equal((1, 20), (tag!.Value.ClimaxCount, tag.Value.Arousal));
    }

    [Fact]
    public void PausingGoesOutAtOnceAndHidesTheAct()
    {
        var p = new TagPublisher();
        p.Update(T0, PoseA, 5, 0, 0, false);
        p.Update(At(3), PoseA, 5, 0, 0, false);
        var tag = p.Update(At(3.5), PoseA, 5, 0, 0, true);
        Assert.True(tag!.Value.Paused);
        Assert.Null(tag.Value.Pose);
    }

    [Fact]
    public void ForgettingPublishesAgain()
    {
        var p = new TagPublisher();
        p.Update(T0, null, 5, 0, 0, false);
        p.Forget();
        Assert.NotNull(p.Update(At(1), null, 5, 0, 0, false));
    }

    [Fact]
    public void ALongSceneWritesFarLessThanAHeartbeat()
    {
        var p = new TagPublisher();
        var writes = 0;
        var arousal = 0.0;
        for (var s = 0; s < 3600; s++)
        {
            if (s % 10 == 0) arousal = (arousal + 3) % 100;
            if (p.Update(At(s), PoseA, 5, arousal, 0, false) is not null) writes++;
        }
        Assert.InRange(writes, 2, 120);
    }
}
