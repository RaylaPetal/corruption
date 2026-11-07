using Corruption.Core;

namespace Corruption.Tests;

public class PersonaTagTests
{
    [Fact]
    public void LooksLikeTheOtherTags()
    {
        Assert.Equal("R1|shy|hypersexual,sensitive", new PersonaTag("shy", ["hypersexual", "sensitive"]).Format());
        Assert.Equal("R1|easygoing|-", new PersonaTag("easygoing", []).Format());
    }

    [Theory]
    [InlineData("R1|shy|hypersexual,sensitive")]
    [InlineData("R1|easygoing|-")]
    [InlineData("R1|from_the_future|new_trait")]
    public void RoundTrips(string text) => Assert.Equal(text, PersonaTag.Parse(text)!.Value.Format());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("R2|shy|-")]
    [InlineData("R1|shy")]
    [InlineData("R1|Shy|-")]
    [InlineData("R1|shy|a,b,c")]
    [InlineData("R1|shy|a b")]
    [InlineData("R1||-")]
    public void RejectsMalformedTags(string? text) => Assert.Null(PersonaTag.Parse(text));

    [Fact]
    public void UnknownIdsAreKeptButNotShown()
    {
        var tag = PersonaTag.Parse("R1|shy|new_trait,sensitive")!.Value;
        Assert.Equal(["new_trait", "sensitive"], tag.Traits);
        Assert.Equal(["sensitive"], tag.KnownTraits.Select(t => t.Id));
        Assert.Null(PersonaTag.Parse("R1|from_the_future|-")!.Value.KnownPersonality);
    }

    [Fact]
    public void TagsCompareByWhatTheySay()
        => Assert.Equal(new PersonaTag("shy", ["edger"]), new PersonaTag("shy", new List<string> { "edger" }));

    private static EngineRig Rig(string personality = PersonaIds.Easygoing, params string[] traits)
    {
        var r = new EngineRig { Playing = false };
        r.Save.Setup = new ProfileSetup { Decay = DecayMode.Off, Personality = personality, Traits = [.. traits] };
        return r;
    }

    [Fact]
    public void AnHourOfPlayWritesItOnce()
    {
        var r = Rig(PersonaIds.Shy, PersonaIds.Edger);
        r.Playing = true;
        r.Run(3600);
        Assert.Equal(["R1|shy|edger"], r.Host.PersonaTags);
    }

    [Fact]
    public void AnUnsetPersonaIsSentAsEasygoing()
    {
        var r = new EngineRig { Playing = false };
        r.Run(1);
        Assert.Equal(["R1|easygoing|-"], r.Host.PersonaTags);
    }

    [Fact]
    public void HidingRemovesAllFiveTags()
    {
        var r = Rig();
        r.Run(2);
        r.Engine.SetHidden(true);
        r.Run(2);
        Assert.Null(r.Host.Tags[^1]);
        Assert.Null(r.Host.ExtraTags[^1]);
        Assert.Null(r.Host.BodyTags[^1]);
        Assert.Null(r.Host.TitleTags[^1]);
        Assert.Null(r.Host.PersonaTags[^1]);
    }

    [Fact]
    public void NoTagBeforeSetup()
    {
        var r = Rig();
        r.Save.Setup = null;
        r.Run(2);
        Assert.Empty(r.Host.PersonaTags);
    }

    [Fact]
    public void IsWrittenAgainWhenAskedToRepublish()
    {
        var r = Rig();
        r.Run(1);
        r.Engine.RepublishTag();
        r.Run(1);
        Assert.Equal(2, r.Host.PersonaTags.Count);
    }

    // ── Chemistry ───────────────────────────────────────────────────────────

    private static EngineRig Pair(string own, string? theirs)
    {
        var r = new EngineRig(balance: new Balance().WithoutMoon());
        r.Save.Setup = new ProfileSetup { Decay = DecayMode.Off, Personality = own };
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        r.Save.SeenActs.Add(r.Act.Key);
        r.Save.SeenPartners.Add(r.Bob.Key);
        r.BobPersona = theirs is null ? null : new PersonaTag(theirs, []);
        r.Watchers = 1;
        r.Run(11);
        return r;
    }

    [Fact]
    public void DominantAndSubmissiveHaveChemistry()
    {
        var r = Pair(PersonaIds.Dominant, PersonaIds.Submissive);
        Assert.Equal(1.05, r.Engine.Status.Factors.Persona, 6);
        Assert.True(r.Engine.HasChemistryWith(r.Bob));
    }

    [Fact]
    public void EveryPairWorksFromBothSides()
    {
        foreach (var (a, b) in PersonaCatalog.Chemistry)
        {
            Assert.Equal(1.05, Pair(a, b).Engine.Status.Factors.Persona, 6);
            Assert.Equal(1.05, Pair(b, a).Engine.Status.Factors.Persona, 6);
        }
    }

    [Fact]
    public void NoChemistryWithAnOlderPartner()
    {
        var r = Pair(PersonaIds.Dominant, null);
        Assert.Equal(1, r.Engine.Status.Factors.Persona, 6);
        Assert.False(r.Engine.HasChemistryWith(r.Bob));
    }

    [Fact]
    public void NoChemistryForAPairThatDoesntSuit()
        => Assert.Equal(1, Pair(PersonaIds.Dominant, PersonaIds.Dominant).Engine.Status.Factors.Persona, 6);

    // ── A Virile partner ────────────────────────────────────────────────────

    private static bool Conceives(string? partnerTrait, double roll)
    {
        var r = new EngineRig(new FixedRandom(roll));
        r.Save.Level = 50;
        r.Save.Goals.Achievements.UnionWith(GoalCatalog.Achievements.Select(a => a.Id));
        PregnancyBook.Enable(r.Save.Pregnancy, true, false, 4, r.Now);
        r.BobBody = new BodyTag(BodyFlags.CanBreed, PregnancyStage.None, null);
        r.BobPersona = new PersonaTag(PersonaIds.Easygoing, partnerTrait is null ? [] : [partnerTrait]);
        r.Host.Aim = ["gr", "st", "ll", "lr", "bt"];
        r.Run(11);
        r.BobClimaxes();
        return PregnancyBook.IsPregnant(r.Save.Pregnancy);
    }

    [Fact]
    public void AVirilePartnerRaisesTheOdds()
    {
        // Day 4 is 25%; Virile makes it 31.25%, so a roll of 0.3 only conceives with it.
        Assert.True(Conceives(PersonaIds.Virile, 0.3));
        Assert.False(Conceives(null, 0.3));
    }
}
