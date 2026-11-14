using Corruption.Core;

namespace Corruption.Tests;

public class ChangelogTests
{
    [Fact]
    public void NewestFirstWithValidVersions()
    {
        var versions = Changelog.Entries.Select(e => Version.Parse(e.Version)).ToList();
        Assert.Equal(versions.OrderByDescending(v => v), versions);
        Assert.All(Changelog.Entries, e => Assert.NotEmpty(e.Notes));
    }

    [Fact]
    public void NothingSeenShowsOnlyTheNewest()
    {
        Assert.Equal([Changelog.Entries[0]], Changelog.Unseen(null));
        Assert.Equal([Changelog.Entries[0]], Changelog.Unseen("garbage"));
    }

    [Fact]
    public void LatestSeenShowsNothing() => Assert.Empty(Changelog.Unseen(Changelog.Latest));

    [Fact]
    public void OlderSeenShowsEverythingNewer()
        => Assert.Equal(Changelog.Entries, Changelog.Unseen("0.0.0.1"));
}
