using System.Numerics;
using Corruption.Core;

namespace Corruption.Tests;

public class ExclusionTests
{
    private static readonly PoseKey Pose = new(50, 0);
    private static readonly string Cuddle = ActsFile.ExclusionKeyOf("Cuddles", "Spoon", Pose);
    private static readonly string Other = ActsFile.ExclusionKeyOf("Sex", "Missionary", Pose);

    [Fact]
    public void ExcludeAndInclude()
    {
        var file = new ActsFile();
        Assert.True(file.SetExcluded("gl", [Cuddle], true));
        Assert.True(file.IsExcluded("GL", Cuddle));
        Assert.False(file.IsExcluded("gl", Other));
        Assert.False(file.SetExcluded("gl", [Cuddle], true));
        Assert.True(file.SetExcluded("gl", [Cuddle], false));
        Assert.False(file.IsExcluded("gl", Cuddle));
        Assert.Empty(file.Exclusions);
    }

    [Fact]
    public void SurvivesUnpickAndRepick()
    {
        var file = new ActsFile();
        file.Mods.Add(new ModPick { ModDirectory = "gl" });
        file.SetExcluded("gl", [Cuddle], true);
        file.Mods.Clear();
        file.Mods.Add(new ModPick { ModDirectory = "gl" });
        Assert.True(file.IsExcluded("gl", Cuddle));
    }

    [Fact]
    public void KeyMatchesTheAct()
        => Assert.Equal(Cuddle, new ModPick { ModDirectory = "gl" }.ActFor("Cuddles", "Spoon", Pose).ExclusionKey);

    [Fact]
    public void ExcludedComesWhereNoKnownActWould()
    {
        var input = new SceneInput(false, false, true, 1, Pose, Pose, Excluded: true);
        Assert.Equal(SceneFailure.ExcludedAct, SceneCheck.Evaluate(input, 3));
        Assert.Equal(SceneFailure.Paused, SceneCheck.Evaluate(input with { Paused = true }, 3));
        Assert.Equal(SceneFailure.NoKnownAct, SceneCheck.Evaluate(input with { Excluded = false }, 3));
    }

    [Fact]
    public void ExcludedAnimationEarnsNothingAndSaysWhy()
    {
        var r = new EngineRig();
        var excluded = r.Act;
        for (var i = 0; i < 30; i++)
        {
            r.Engine.Sample(r.Now, new SceneSample(null, excluded.Pose, Vector3.Zero,
                [new VisiblePlayer(r.Bob, 1, EngineRig.BobPose, r.BobTag)], Excluded: excluded));
            r.Now = r.Now.AddSeconds(1);
        }
        Assert.Equal(0, r.Save.Xp);
        Assert.Equal(SceneFailure.ExcludedAct, r.Engine.Status.Failure);
        Assert.Equal(SceneState.Waiting, r.Engine.Status.State);
        Assert.Same(excluded, r.Engine.Status.Excluded);
    }
}
