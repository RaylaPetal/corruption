namespace Corruption.Core;

/// Pregnancy as chosen in the welcome window.
public sealed record PregnancyChoice(bool On, bool Carry, bool Breed, int StartDay, double ChanceFactor);

/// The personality and traits as chosen in the welcome window.
public sealed record PersonaChoice(string? Personality, IReadOnlyList<string> Traits);

/// Everything the welcome window asks, applied at once when the player finishes it. Persona is null when the persona
/// step wasn't shown, because one is already chosen.
public sealed record WelcomeChoices(ProfileSetup? Setup, Anatomy Anatomy, PregnancyChoice Pregnancy, PersonaChoice? Persona = null);

/// The one-time welcome: shown to every character until they finish it, and again for one that resets its profile.
public static class Welcome
{
    /// Raised when the welcome gains steps worth showing to characters who already finished it. 2 added the persona.
    public const int CurrentVersion = 2;

    public static bool IsNeeded(CharacterSave save)
        => save.Setup is null || save.WelcomeVersion < CurrentVersion || NeedsPersona(save);

    /// A character set up before personalities existed, who hasn't chosen one yet.
    public static bool NeedsPersona(CharacterSave save) => save.Setup is null || save.Setup.Personality is null;
}
