using System.Text.RegularExpressions;

namespace Corruption.Core;

public enum PersonaKind { Personality, Trait }

/// <param name="Id">Stored in saves and sent in the persona tag: never rename one.</param>
/// <param name="Upside">What it gives, for the welcome and Profile. Empty for Easygoing.</param>
/// <param name="Downside">What it costs. Empty for Easygoing.</param>
/// <param name="Role">A pregnancy role it needs to do anything.</param>
public sealed record PersonaDef(string Id, string Name, PersonaKind Kind, string Upside, string Downside, Modifiers Effects,
    ItemRole Role = ItemRole.Anyone);

public static class PersonaIds
{
    public const string Easygoing = "easygoing";
    public const string Shy = "shy";
    public const string Reserved = "reserved";
    public const string Romantic = "romantic";
    public const string Loyal = "loyal";
    public const string Lewd = "lewd";
    public const string Exhibitionist = "exhibitionist";
    public const string Dominant = "dominant";
    public const string Submissive = "submissive";
    public const string Bratty = "bratty";
    public const string Playful = "playful";
    public const string Curious = "curious";
    public const string Needy = "needy";

    public const string Hypersexual = "hypersexual";
    public const string Sensitive = "sensitive";
    public const string Enduring = "enduring";
    public const string MultiOrgasmic = "multi_orgasmic";
    public const string Edger = "edger";
    public const string Messy = "messy";
    public const string AttentionSeeker = "attention_seeker";
    public const string Clingy = "clingy";
    public const string QuickLearner = "quick_learner";
    public const string Fertile = "fertile";
    public const string Virile = "virile";
    public const string Greedy = "greedy";
    public const string Thrifty = "thrifty";
    public const string Lightweight = "lightweight";
}

public static partial class PersonaCatalog
{
    public const int MaxTraits = 2;

    /// Chemistry pays this on ticks with a partner whose personality suits this one.
    public const double ChemistryXp = 1.05;

    /// Pent Up pays this on ticks until it wears off.
    public const double PentUpXp = 0.8;
    public static readonly TimeSpan PentUpAfter = TimeSpan.FromHours(24);
    public const double PentUpSeconds = 15 * 60;

    private static PersonaDef P(string id, string name, string up, string down, Modifiers effects)
        => new(id, name, PersonaKind.Personality, up, down, effects);

    private static PersonaDef T(string id, string name, string up, string down, Modifiers effects, ItemRole role = ItemRole.Anyone)
        => new(id, name, PersonaKind.Trait, up, down, effects, role);

    public static readonly IReadOnlyList<PersonaDef> Personalities =
    [
        P(PersonaIds.Easygoing, "Easygoing", "", "", Modifiers.None),
        P(PersonaIds.Shy, "Shy", "Scenes with nobody watching pay ×1.1 XP", "Each watcher counts half",
            new Modifiers { QuietTickXp = 1.1, WatcherCount = 0.5 }),
        P(PersonaIds.Reserved, "Reserved", "Fatigue starts 5 minutes later", "Arousal builds ×0.9 as fast",
            new Modifiers { FatigueGraceMinutes = 5, Arousal = 0.9 }),
        P(PersonaIds.Romantic, "Romantic", "Your bond pays +0.1 more", "Someone new pays half the first-scene XP",
            new Modifiers { BondedExtra = 0.1, FirstPartnerXp = 0.5 }),
        P(PersonaIds.Loyal, "Loyal", "Streak bonus goes up to ×1.4", "A missed day ends your streak",
            new Modifiers { StreakCap = 1.4, StreakEndsOnMiss = true }),
        P(PersonaIds.Lewd, "Lewd", "Arousal builds ×1.15 as fast", "Your climaxes pay ×0.85 XP",
            new Modifiers { Arousal = 1.15, ClimaxXp = 0.85 }),
        P(PersonaIds.Exhibitionist, "Exhibitionist", "Each watcher counts ×1.5", "Arousal builds ×0.85 as fast with nobody watching",
            new Modifiers { WatcherCount = 1.5, QuietArousal = 0.85 }),
        P(PersonaIds.Dominant, "Dominant", "A partner's climax pays you 75% of theirs", "Your climaxes pay ×0.8 XP",
            new Modifiers { PartnerClimaxShare = 0.75, ClimaxXp = 0.8 }),
        P(PersonaIds.Submissive, "Submissive", "Your climaxes pay ×1.25 XP", "/corr climax needs 10 more arousal",
            new Modifiers { ClimaxXp = 1.25, ManualClimaxShift = 10 }),
        P(PersonaIds.Bratty, "Bratty", "Your climaxes pay ×1.3 XP", "Arousal builds ×0.85 as fast",
            new Modifiers { ClimaxXp = 1.3, Arousal = 0.85 }),
        P(PersonaIds.Playful, "Playful", "A new animation pays ×2 XP", "Fatigue starts 3 minutes sooner",
            new Modifiers { FirstActXp = 2, FatigueGraceMinutes = -3 }),
        P(PersonaIds.Curious, "Curious", "Someone new or a new animation pays ×1.5 XP", "Time with your bond counts ×0.8",
            new Modifiers { FirstPartnerXp = 1.5, FirstActXp = 1.5, BondTime = 0.8 }),
        P(PersonaIds.Needy, "Needy", "Arousal builds ×1.1 as fast", "Arousal fades twice as fast out of a scene",
            new Modifiers { Arousal = 1.1, ArousalDecay = 2 }),
    ];

    public static readonly IReadOnlyList<PersonaDef> Traits =
    [
        T(PersonaIds.Hypersexual, "Hypersexual", "Arousal builds ×1.25 as fast and refractory ×0.75",
            "After a day without a scene you're Pent Up: ×0.8 XP for 15 minutes of scenes",
            new Modifiers { Arousal = 1.25, Refractory = 0.75, PentUp = true }),
        T(PersonaIds.Sensitive, "Sensitive", "Arousal builds ×1.2 as fast", "Your climaxes pay ×0.8 XP",
            new Modifiers { Arousal = 1.2, ClimaxXp = 0.8 }),
        T(PersonaIds.Enduring, "Enduring", "Refractory period halved", "Arousal builds ×0.85 as fast",
            new Modifiers { Refractory = 0.5, Arousal = 0.85 }),
        T(PersonaIds.MultiOrgasmic, "Multi-Orgasmic", "Arousal stays at 40 after a climax", "Refractory period ×1.5",
            new Modifiers { ArousalAfterClimax = 40, Refractory = 1.5 }),
        T(PersonaIds.Edger, "Edger", "Time at 100 arousal counts double for goals", "Arousal builds ×0.9 as fast",
            new Modifiers { EdgedCount = 2, Arousal = 0.9 }),
        T(PersonaIds.Messy, "Messy", "Each layer worn adds 4.5% instead of 3%", "Your climaxes pay ×0.9 XP",
            new Modifiers { MarkedPerLayer = 0.045, ClimaxXp = 0.9 }),
        T(PersonaIds.AttentionSeeker, "Attention Seeker", "Each watcher counts ×1.25", "Scenes with nobody watching pay ×0.9 XP",
            new Modifiers { WatcherCount = 1.25, QuietTickXp = 0.9 }),
        T(PersonaIds.Clingy, "Clingy", "Time with your bond counts ×1.5", "While bonded, scenes with others pay ×0.9 XP",
            new Modifiers { BondTime = 1.5, AwayFromBondTickXp = 0.9 }),
        T(PersonaIds.QuickLearner, "Quick Learner", "Someone new or a new animation pays ×2 XP", "Fatigue starts 3 minutes sooner",
            new Modifiers { FirstPartnerXp = 2, FirstActXp = 2, FatigueGraceMinutes = -3 }),
        T(PersonaIds.Fertile, "Fertile", "Your conception chance ×1.5", "Contraceptive and Morning-After Pills cost double",
            new Modifiers
            {
                ConceptionOwn = 1.5,
                ItemPriceFor = new Dictionary<string, double> { [ItemIds.Contraceptive] = 2, [ItemIds.MorningAfter] = 2 },
            }, ItemRole.Carry),
        T(PersonaIds.Virile, "Virile", "Partners' conception chance with you ×1.25", "Refractory period ×1.25",
            new Modifiers { ConceptionPartner = 1.25, Refractory = 1.25 }, ItemRole.Breed),
        T(PersonaIds.Greedy, "Greedy", "Scene time pays ×1.25 Sins", "Scene XP ×0.95",
            new Modifiers { SceneSins = 1.25, PersonaTickXp = 0.95 }),
        T(PersonaIds.Thrifty, "Thrifty", "Shop items cost 15% less", "Timed items last ×0.8",
            new Modifiers { ItemPrice = 0.85, ItemDuration = 0.8 }),
        T(PersonaIds.Lightweight, "Lightweight", "Timed items last ×1.5", "Shop items cost 15% more",
            new Modifiers { ItemDuration = 1.5, ItemPrice = 1.15 }),
    ];

    /// Personalities that suit each other. Order doesn't matter.
    public static readonly IReadOnlyList<(string A, string B)> Chemistry =
    [
        (PersonaIds.Dominant, PersonaIds.Submissive),
        (PersonaIds.Dominant, PersonaIds.Bratty),
        (PersonaIds.Romantic, PersonaIds.Romantic),
        (PersonaIds.Shy, PersonaIds.Exhibitionist),
        (PersonaIds.Playful, PersonaIds.Curious),
        (PersonaIds.Lewd, PersonaIds.Lewd),
    ];

    public static PersonaDef? FindPersonality(string? id) => id is null ? null : Personalities.FirstOrDefault(p => p.Id == id);

    public static PersonaDef? FindTrait(string? id) => id is null ? null : Traits.FirstOrDefault(t => t.Id == id);

    public static bool HaveChemistry(string? a, string? b)
        => a is not null && b is not null && Chemistry.Any(c => (c.A == a && c.B == b) || (c.A == b && c.B == a));

    [GeneratedRegex("^[a-z0-9_]{1,24}$")]
    private static partial Regex IdPattern();

    /// Ids are sent in a tag, so they're kept short and plain.
    public static bool IsValidId(string? id) => id is not null && IdPattern().IsMatch(id);
}

/// One character's persona: validating a choice, and what's active now.
public static class PersonaBook
{
    /// Why this choice can't be locked in, or null when it can.
    public static string? Refusal(string? personality, IReadOnlyCollection<string> traits)
        => PersonaCatalog.FindPersonality(personality) is null ? "Pick a personality."
            : traits.Count > PersonaCatalog.MaxTraits ? $"Pick at most {PersonaCatalog.MaxTraits} traits."
            : traits.Distinct().Count() != traits.Count ? "Each trait can only be picked once."
            : traits.Any(t => PersonaCatalog.FindTrait(t) is null) ? "That trait doesn't exist."
            : null;

    /// The personality, or Easygoing when none was chosen yet.
    public static PersonaDef Personality(CharacterSave save)
        => PersonaCatalog.FindPersonality(save.Setup?.Personality) ?? PersonaCatalog.Personalities[0];

    public static IEnumerable<PersonaDef> Traits(CharacterSave save)
        => (save.Setup?.Traits ?? []).Select(PersonaCatalog.FindTrait).OfType<PersonaDef>();

    /// A trait that needs a pregnancy role works only with it.
    public static bool IsActive(CharacterSave save, PersonaDef trait) => trait.Role switch
    {
        ItemRole.Carry => save.Pregnancy.Enabled && save.Pregnancy.CanCarry,
        ItemRole.Breed => save.Pregnancy.Enabled && save.Pregnancy.CanBreed,
        _ => true,
    };

    /// The personality's and active traits' effects together.
    public static Modifiers Effects(CharacterSave save)
        => Modifiers.Combine(Traits(save).Where(t => IsActive(save, t)).Select(t => t.Effects).Prepend(Personality(save).Effects));
}
