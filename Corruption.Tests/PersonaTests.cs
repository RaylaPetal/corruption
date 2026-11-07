using Corruption.Core;

namespace Corruption.Tests;

/// The persona catalogue, choosing one, and what it adds up to.
public class PersonaTests
{
    private static IEnumerable<PersonaDef> All => PersonaCatalog.Personalities.Concat(PersonaCatalog.Traits);

    [Fact]
    public void ThereAreThirteenPersonalitiesAndFourteenTraits()
    {
        Assert.Equal(13, PersonaCatalog.Personalities.Count);
        Assert.Equal(14, PersonaCatalog.Traits.Count);
        Assert.All(PersonaCatalog.Personalities, p => Assert.Equal(PersonaKind.Personality, p.Kind));
        Assert.All(PersonaCatalog.Traits, t => Assert.Equal(PersonaKind.Trait, t.Kind));
    }

    [Fact]
    public void IdsAreUniqueAndSafeToSend()
    {
        var ids = All.Select(p => p.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.True(PersonaCatalog.IsValidId(id), id));
    }

    [Fact]
    public void EasygoingDoesNothing()
    {
        var easygoing = PersonaCatalog.FindPersonality(PersonaIds.Easygoing)!;
        Assert.Same(Modifiers.None, easygoing.Effects);
        Assert.Equal("", easygoing.Upside);
        Assert.Equal("", easygoing.Downside);
    }

    /// Every factor and value a persona can set, and whether it's a cost at this value.
    private static bool HasACost(Modifiers m)
    {
        double[] goodWhenHigh =
        [
            m.PersonaTickXp, m.QuietTickXp, m.AwayFromBondTickXp, m.Arousal, m.QuietArousal, m.ClimaxXp, m.WatcherCount,
            m.FirstPartnerXp, m.FirstActXp, m.BondTime, m.ItemDuration, m.EdgedCount, m.SceneSins,
        ];
        double[] goodWhenLow = [m.Refractory, m.ArousalDecay, m.ItemPrice, .. m.ItemPriceFor.Values];
        return goodWhenHigh.Any(v => v < 1) || goodWhenLow.Any(v => v > 1) || m.ManualClimaxShift > 0 || m.FatigueGraceMinutes < 0
               || m.StreakEndsOnMiss || m.PentUp;
    }

    [Fact]
    public void EveryChoiceButEasygoingCostsSomething()
    {
        foreach (var persona in All.Where(p => p.Id != PersonaIds.Easygoing))
        {
            Assert.False(string.IsNullOrWhiteSpace(persona.Upside), persona.Id);
            Assert.False(string.IsNullOrWhiteSpace(persona.Downside), persona.Id);
            Assert.True(HasACost(persona.Effects), $"{persona.Id} has no downside in its effects");
        }
    }

    [Fact]
    public void ChemistryWorksBothWays()
    {
        foreach (var (a, b) in PersonaCatalog.Chemistry)
        {
            Assert.True(PersonaCatalog.HaveChemistry(a, b));
            Assert.True(PersonaCatalog.HaveChemistry(b, a));
            Assert.NotNull(PersonaCatalog.FindPersonality(a));
            Assert.NotNull(PersonaCatalog.FindPersonality(b));
        }
        Assert.False(PersonaCatalog.HaveChemistry(PersonaIds.Shy, PersonaIds.Shy));
        Assert.False(PersonaCatalog.HaveChemistry(PersonaIds.Dominant, null));
    }

    // ── Choosing ────────────────────────────────────────────────────────────

    [Fact]
    public void AChoiceNeedsAPersonality()
        => Assert.Equal("Pick a personality.", PersonaBook.Refusal(null, []));

    [Fact]
    public void AtMostTwoTraits()
        => Assert.NotNull(PersonaBook.Refusal(PersonaIds.Shy, [PersonaIds.Sensitive, PersonaIds.Edger, PersonaIds.Greedy]));

    [Fact]
    public void NoTraitTwice()
        => Assert.NotNull(PersonaBook.Refusal(PersonaIds.Shy, [PersonaIds.Sensitive, PersonaIds.Sensitive]));

    [Fact]
    public void NoUnknownTraits()
        => Assert.NotNull(PersonaBook.Refusal(PersonaIds.Shy, ["unheard_of"]));

    [Fact]
    public void AGoodChoiceIsAccepted()
    {
        Assert.Null(PersonaBook.Refusal(PersonaIds.Shy, [PersonaIds.Hypersexual, PersonaIds.Sensitive]));
        Assert.Null(PersonaBook.Refusal(PersonaIds.Easygoing, []));
    }

    [Fact]
    public void AResetClearsThePersonaAndPentUp()
    {
        var save = new CharacterSave { Setup = new ProfileSetup { Personality = PersonaIds.Shy, Traits = [PersonaIds.Hypersexual] }, PentUpSecondsLeft = 300 };
        save.ResetProgress(EngineRig.T0);
        Assert.Null(save.Setup);
        Assert.Equal(0, save.PentUpSecondsLeft);
        Assert.Equal(PersonaIds.Easygoing, PersonaBook.Personality(save).Id);
    }

    // ── What it adds up to ──────────────────────────────────────────────────

    private static CharacterSave With(string personality, params string[] traits)
        => new() { Setup = new ProfileSetup { Personality = personality, Traits = [.. traits] } };

    [Fact]
    public void LewdAndSensitiveStack()
        => Assert.Equal(1.15 * 1.2, ModifierBook.For(With(PersonaIds.Lewd, PersonaIds.Sensitive)).Arousal, 6);

    [Fact]
    public void AnUnsetPersonaIsEasygoing()
    {
        var save = new CharacterSave { Setup = new ProfileSetup() };
        Assert.Equal(PersonaIds.Easygoing, PersonaBook.Personality(save).Id);
        Assert.Equal(1, ModifierBook.For(save).Arousal);
    }

    [Fact]
    public void FertileNeedsTheCarryRole()
    {
        var save = With(PersonaIds.Easygoing, PersonaIds.Fertile);
        Assert.Equal(1, ModifierBook.For(save).ConceptionOwn);
        PregnancyBook.Enable(save.Pregnancy, true, false, 1, EngineRig.T0);
        Assert.Equal(1.5, ModifierBook.For(save).ConceptionOwn);
    }

    [Fact]
    public void VirileNeedsTheBreedRole()
    {
        var save = With(PersonaIds.Easygoing, PersonaIds.Virile);
        Assert.Equal(1, ModifierBook.For(save).Refractory);
        PregnancyBook.Enable(save.Pregnancy, false, true, 1, EngineRig.T0);
        Assert.Equal(1.25, ModifierBook.For(save).Refractory);
    }

    [Fact]
    public void TheTitleAndPersonaCombine()
    {
        var save = With(PersonaIds.Loyal);
        save.Titles.Unlocked.Add("habit");
        save.Titles.Equipped = "habit";
        Assert.Equal(1.4, ModifierBook.For(save).StreakCap);
    }
}
