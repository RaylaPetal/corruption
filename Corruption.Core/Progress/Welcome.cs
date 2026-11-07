namespace Corruption.Core;

/// Pregnancy as chosen in the welcome window.
public sealed record PregnancyChoice(bool On, bool Carry, bool Breed, int StartDay, double ChanceFactor);

/// Everything the welcome window asks, applied at once when the player finishes it.
public sealed record WelcomeChoices(ProfileSetup? Setup, Anatomy Anatomy, PregnancyChoice Pregnancy);

/// The one-time welcome: shown to every character until they finish it, and again for one that resets its profile.
public static class Welcome
{
    /// Raised when the welcome gains steps worth showing to characters who already finished it.
    public const int CurrentVersion = 1;

    public static bool IsNeeded(CharacterSave save) => save.Setup is null || save.WelcomeVersion < CurrentVersion;
}
