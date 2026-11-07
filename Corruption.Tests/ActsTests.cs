using Corruption.Core;

namespace Corruption.Tests;

public class ServedCheckTests
{
    private const string Root = @"C:\Mods";
    private const string GamePath = "chara/human/c0201/animation/a0001/bt_common/emote/sweep.pap";
    private const string OtherRacePath = "chara/human/c0101/animation/a0001/bt_common/emote/sweep.pap";

    private static Dictionary<string, IReadOnlyCollection<string>> Redirects(params string[] paths)
        => paths.ToDictionary(p => p, _ => (IReadOnlyCollection<string>)[@"anims\sweep.pap"]);

    [Fact]
    public void ServedWhenPenumbraResolvesToTheModsFile()
    {
        var resolved = new Dictionary<string, string> { [GamePath] = @"C:\Mods\MyMod\anims\sweep.pap" };
        Assert.True(ServedCheck.IsServed(Root, "MyMod", Redirects(GamePath), resolved, "c0201"));
    }

    [Fact]
    public void NotServedWhenAnotherModWins()
    {
        var resolved = new Dictionary<string, string> { [GamePath] = @"C:\Mods\OtherMod\anims\sweep.pap" };
        Assert.False(ServedCheck.IsServed(Root, "MyMod", Redirects(GamePath), resolved, "c0201"));
    }

    [Fact]
    public void NotServedWhenThePathResolvesToTheGameItself()
    {
        var resolved = new Dictionary<string, string> { [GamePath] = GamePath };
        Assert.False(ServedCheck.IsServed(Root, "MyMod", Redirects(GamePath), resolved, "c0201"));
    }

    [Fact]
    public void NotServedWhenUnresolved()
        => Assert.False(ServedCheck.IsServed(Root, "MyMod", Redirects(GamePath), new Dictionary<string, string>(), "c0201"));

    [Fact]
    public void OwnRacePathDecidesWhenTheOptionHasOne()
    {
        var resolved = new Dictionary<string, string>
        {
            [GamePath] = GamePath,
            [OtherRacePath] = @"C:\Mods\MyMod\anims\sweep.pap",
        };
        Assert.False(ServedCheck.IsServed(Root, "MyMod", Redirects(GamePath, OtherRacePath), resolved, "c0201"));
    }

    [Fact]
    public void AnyPathCountsWhenTheOptionHasNoneForThisRace()
    {
        var resolved = new Dictionary<string, string> { [OtherRacePath] = @"C:\Mods\MyMod\anims\sweep.pap" };
        Assert.True(ServedCheck.IsServed(Root, "MyMod", Redirects(OtherRacePath), resolved, "c0801"));
    }
}

public class RaceCodesTests
{
    [Theory]
    [InlineData(1, 1, 0, "c0101")]
    [InlineData(1, 1, 1, "c0201")]
    [InlineData(1, 2, 0, "c0301")]
    [InlineData(1, 2, 1, "c0401")]
    [InlineData(2, 3, 1, "c0601")]
    [InlineData(4, 7, 1, "c0801")]
    [InlineData(5, 9, 0, "c0901")]
    [InlineData(6, 12, 1, "c1401")]
    [InlineData(7, 14, 0, "c1501")]
    [InlineData(8, 16, 1, "c1801")]
    public void Codes(byte race, byte tribe, byte gender, string code) => Assert.Equal(code, RaceCodes.Of(race, tribe, gender));

    [Fact]
    public void UnknownRaceHasNoCode() => Assert.Null(RaceCodes.Of(0, 0, 0));
}

public class ModPickTests
{
    private static readonly PoseKey Pose = new(50, 0);

    [Fact]
    public void AnimationsTakeTheModsIdentity()
    {
        var act = new ModPick { ModDirectory = "m", ModName = "Mod" }.ActFor("g", "o", Pose);
        Assert.Equal(("o", "m", "Mod"), (act.Option, act.ModDirectory, act.ModName));
        Assert.Equal(Pose, act.Pose);
    }

    [Fact]
    public void AnimationsInAModAreDistinctActs()
    {
        var pick = new ModPick { ModDirectory = "m" };
        Assert.NotEqual(pick.ActFor("g", "one", Pose).Key, pick.ActFor("g", "two", Pose).Key);
    }
}

public class ActsFileMigrationTests
{
    [Fact]
    public void AModWithAPickedAnimationStaysPicked()
    {
        var file = new ActsFile
        {
            Acts =
            [
                new ActEntry { ModDirectory = "m", ModName = "Mod", Option = "a" },
                new ActEntry { ModDirectory = "m", ModName = "Mod", Option = "b" },
                new ActEntry { ModDirectory = "n", ModName = "Other" },
            ],
        };
        Assert.True(file.Migrate());
        Assert.Equal(2, file.Mods.Count);
        Assert.NotNull(file.Find("n"));
        Assert.Null(file.Acts);
    }

    [Fact]
    public void SeenAnimationsMeanTheModWasSeen()
    {
        var file = new ActsFile { KnownKeys = ["m|g|o|50|0"] };
        file.Migrate();
        Assert.Contains("m", file.KnownMods);
    }

    [Fact]
    public void NothingToMigrate() => Assert.False(new ActsFile().Migrate());
}

public class ActsFileTests
{
    [Fact]
    public void TickedFolderIncludesSubfoldersOnly()
    {
        var file = new ActsFile { Folders = ["Animations/Couples"] };
        Assert.True(file.IsInTickedFolder("Animations/Couples"));
        Assert.True(file.IsInTickedFolder("animations/couples/Extra"));
        Assert.False(file.IsInTickedFolder("Animations/CouplesOther"));
        Assert.False(file.IsInTickedFolder(""));
    }

    [Fact]
    public void NoFolderCanBeTickedWithoutIncludingEverything()
    {
        var file = new ActsFile { Folders = [""] };
        Assert.True(file.IsInTickedFolder(""));
        Assert.False(file.IsInTickedFolder("Animations"));
    }

    [Fact]
    public void FolderOfModPath()
    {
        Assert.Equal("Animations/Couples", ActsFile.FolderOf("Animations/Couples/Mod"));
        Assert.Equal("", ActsFile.FolderOf("Mod"));
    }

    [Fact]
    public void VariantsAreSeparateActs()
    {
        var a = new ActEntry { ModDirectory = "m", Option = "o", EmoteMode = 2, Variant = 1 };
        var b = new ActEntry { ModDirectory = "m", Option = "o", EmoteMode = 2, Variant = 2 };
        Assert.NotEqual(a.Key, b.Key);
    }

    [Fact]
    public void OnlySitEmotesKeepTheirVariant()
    {
        Assert.Equal(new PoseKey(2, 3), PoseKey.Normalized(2, 3));
        Assert.Equal(new PoseKey(50, 0), PoseKey.Normalized(50, 3));
    }
}

public class JsonStoreTests : IDisposable
{
    private readonly string dir = Directory.CreateTempSubdirectory("corruption-tests").FullName;

    public void Dispose() => Directory.Delete(dir, recursive: true);

    [Fact]
    public void RoundTrips()
    {
        var path = Path.Combine(dir, "acts.json");
        var file = new ActsFile { Folders = ["A"], Mods = [new ModPick { ModDirectory = "m", ModName = "Mod" }] };
        JsonStore.Save(path, file);
        var loaded = JsonStore.Load<ActsFile>(path, out var error);
        Assert.Null(error);
        Assert.Equal("Mod", loaded.Mods[0].ModName);
        Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
    }

    [Fact]
    public void MissingFileGivesDefaults()
    {
        Assert.Empty(JsonStore.Load<ActsFile>(Path.Combine(dir, "none.json"), out var error).Mods);
        Assert.Null(error);
    }

    [Fact]
    public void UnreadableFileIsKeptAsABackup()
    {
        var path = Path.Combine(dir, "progress.json");
        File.WriteAllText(path, "{ not json");
        var loaded = JsonStore.Load<ProgressFile>(path, out var error);
        Assert.Empty(loaded.Characters);
        Assert.NotNull(error);
        Assert.False(File.Exists(path));
        var backup = Assert.Single(Directory.GetFiles(dir, "progress.json.bak-*"));
        Assert.Equal("{ not json", File.ReadAllText(backup));
    }

    [Fact]
    public void OldPicksMigrateAndAreNotWrittenAgain()
    {
        var path = Path.Combine(dir, "acts.json");
        File.WriteAllText(path, """{ "Folders": ["A"], "Acts": [{ "ModDirectory": "m", "ModName": "Mod", "Tier": 3, "CumZone": "mo" }], "KnownKeys": ["n|g|o|1|0"] }""");
        var loaded = JsonStore.Load<ActsFile>(path, out _);
        Assert.True(loaded.Migrate());
        JsonStore.Save(path, loaded);
        var text = File.ReadAllText(path);
        Assert.DoesNotContain("KnownKeys", text);
        Assert.DoesNotContain("\"Acts\"", text);
        var again = JsonStore.Load<ActsFile>(path, out _);
        Assert.False(again.Migrate());
        Assert.Equal("Mod", again.Find("m")!.ModName);
    }

    [Fact]
    public void ProgressRoundTripsPerCharacter()
    {
        var path = Path.Combine(dir, "progress.json");
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var file = new ProgressFile();
        file.ForCharacter(1, now).Level = 12;
        file.ForCharacter(1, now).SeenActs.Add("a");
        file.ForCharacter(2, now).Level = 40;
        JsonStore.Save(path, file);
        var loaded = JsonStore.Load<ProgressFile>(path, out _);
        Assert.Equal(12, loaded.ForCharacter(1, now).Level);
        Assert.Contains("a", loaded.ForCharacter(1, now).SeenActs);
        Assert.Equal(40, loaded.ForCharacter(2, now).Level);
    }
}
