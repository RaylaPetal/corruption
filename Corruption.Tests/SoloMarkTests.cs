using Corruption.Core;

namespace Corruption.Tests;

/// "Counts alone" on a picked mod.
public class SoloMarkTests
{
    private static ActsFile Picked()
    {
        var file = new ActsFile();
        file.Mods.Add(new ModPick { ModDirectory = "SoloPack", ModName = "Solo Pack" });
        return file;
    }

    [Fact]
    public void APickIsntSoloUntilMarked()
    {
        var file = Picked();
        Assert.False(file.Find("SoloPack")!.Solo);
        Assert.True(file.SetSolo("SoloPack", true));
        Assert.True(file.Find("solopack")!.Solo);
        Assert.False(file.SetSolo("SoloPack", true));
    }

    [Fact]
    public void AnUnpickedModCantBeMarked() => Assert.False(Picked().SetSolo("Elsewhere", true));

    [Fact]
    public void ActsCarryTheMark()
    {
        var file = Picked();
        file.SetSolo("SoloPack", true);
        Assert.True(file.Find("SoloPack")!.ActFor("g", "o", new PoseKey(50, 0)).Solo);
    }

    [Fact]
    public void UnpickingClearsIt()
    {
        var file = Picked();
        file.SetSolo("SoloPack", true);
        file.Mods.Remove(file.Find("SoloPack")!);
        file.Mods.Add(new ModPick { ModDirectory = "SoloPack", ModName = "Solo Pack" });
        Assert.False(file.Find("SoloPack")!.Solo);
    }

    [Fact]
    public void TheMarkSurvivesSavingAndAnOlderFileReadsAsNotSolo()
    {
        var dir = Directory.CreateTempSubdirectory("corruption-solo").FullName;
        try
        {
            var path = Path.Combine(dir, "acts.json");
            var file = Picked();
            file.Mods.Add(new ModPick { ModDirectory = "Duo", ModName = "Duo" });
            file.SetSolo("SoloPack", true);
            JsonStore.Save(path, file);
            var loaded = JsonStore.Load<ActsFile>(path, out _);
            Assert.True(loaded.Find("SoloPack")!.Solo);
            Assert.False(loaded.Find("Duo")!.Solo);

            File.WriteAllText(path, """{"Folders":[],"Mods":[{"ModDirectory":"Old","ModName":"Old"}]}""");
            Assert.False(JsonStore.Load<ActsFile>(path, out _).Find("Old")!.Solo);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
