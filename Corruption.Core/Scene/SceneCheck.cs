namespace Corruption.Core;

/// Why a scene isn't earning right now. None means it is.
public enum SceneFailure
{
    None, NeedsSetup, Paused, Hidden, NoKnownAct, NoPartner, PartnerTooFar, NoPartnerDeclaration, PartnerEmoteMismatch, ExcludedAct,
    SoloBudgetSpent,
}

/// What the check needs, all as plain values. Declared and Observed belong to the nearest loaded partner.
public readonly record struct SceneInput(
    bool Paused,
    bool HasKnownAct,
    bool PartnerLoaded,
    double Distance,
    PoseKey? Declared,
    PoseKey? Observed,
    bool NeedsSetup = false,
    bool Hidden = false,
    bool Excluded = false,
    bool SoloAct = false,
    bool SoloBudgetLeft = true);

/// The whole per-client rule. It never compares the partners' acts: each only needs one known on their own plugin.
public static class SceneCheck
{
    /// When true, sit and doze variants must match too. If in-game testing shows other players' variants read
    /// unreliably, set this false to compare the emote alone.
    public const bool CompareVariant = true;

    public static SceneFailure Evaluate(in SceneInput input, double rangeYalms) => Check(input, rangeYalms).Failure;

    /// The scene's outcome: why it isn't earning, or None; and whether it earns as a solo scene. A duo scene always
    /// wins. Only when a partner condition fails does a solo act earn alone, and only with solo time left today.
    public static (SceneFailure Failure, bool Solo) Check(in SceneInput input, double rangeYalms)
    {
        var duo = Duo(input, rangeYalms);
        if (duo is not (SceneFailure.NoPartner or SceneFailure.PartnerTooFar or SceneFailure.NoPartnerDeclaration
            or SceneFailure.PartnerEmoteMismatch) || !input.SoloAct) return (duo, false);
        return input.SoloBudgetLeft ? (SceneFailure.None, true) : (SceneFailure.SoloBudgetSpent, false);
    }

    private static SceneFailure Duo(in SceneInput input, double rangeYalms)
    {
        if (input.NeedsSetup) return SceneFailure.NeedsSetup;
        if (input.Paused) return SceneFailure.Paused;
        if (input.Hidden) return SceneFailure.Hidden;
        if (!input.HasKnownAct) return input.Excluded ? SceneFailure.ExcludedAct : SceneFailure.NoKnownAct;
        if (!input.PartnerLoaded) return SceneFailure.NoPartner;
        if (input.Distance > rangeYalms) return SceneFailure.PartnerTooFar;
        if (input.Declared is not { } declared) return SceneFailure.NoPartnerDeclaration;
        if (input.Observed is not { } observed || !SamePose(declared, observed)) return SceneFailure.PartnerEmoteMismatch;
        return SceneFailure.None;
    }

    private static bool SamePose(PoseKey declared, PoseKey observed)
#pragma warning disable CS0162 // The other branch is the documented fallback for CompareVariant.
        => CompareVariant ? declared == observed : declared.EmoteMode == observed.EmoteMode;
#pragma warning restore CS0162

    public static string Describe(SceneFailure failure) => failure switch
    {
        SceneFailure.None => "Earning",
        SceneFailure.NeedsSetup => "Finish your profile setup in /corr",
        SceneFailure.Paused => "Paused",
        SceneFailure.Hidden => "You're hidden, so scenes can't earn",
        SceneFailure.NoKnownAct => "No known act playing",
        SceneFailure.NoPartner => "Nobody nearby with Corruption",
        SceneFailure.PartnerTooFar => "Partner is too far away",
        SceneFailure.NoPartnerDeclaration => "Your partner isn't playing an act",
        SceneFailure.PartnerEmoteMismatch => "Partner isn't playing what they declared",
        SceneFailure.ExcludedAct => "This animation is excluded, so it doesn't count",
        SceneFailure.SoloBudgetSpent => "Your solo time for today is used up",
        _ => failure.ToString(),
    };
}
