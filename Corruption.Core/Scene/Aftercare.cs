namespace Corruption.Core;

/// The numbers of aftercare: licking a partner clean, and being licked or kissed.
public static class AftercareRules
{
    /// Lick clean is offered this long after the local character's climax with a partner.
    public static readonly TimeSpan OfferFor = TimeSpan.FromMinutes(2);

    public const double LickXp = 20;

    /// Lick clean pays this instead when it licks up the creampie this character's climax left.
    public const double CreampieLickXp = 30;
    public const double LickBondSeconds = 60;

    /// Where a lick goes when the bones can't be read.
    public const string FallbackPart = "st";

    /// Being licked or kissed clean by someone you had a scene with this recently gives arousal, at most this often.
    public static readonly TimeSpan RecentPartner = TimeSpan.FromMinutes(2);
    public const double CleanedArousal = 5;

    /// Being licked out (a lick ending a creampie) gives this instead.
    public const double LickedOutArousal = 10;
    public static readonly TimeSpan CleanedGap = TimeSpan.FromSeconds(30);
}

/// Creampies on a partner's credited climax.
public static class CreampieRules
{
    public const double Xp = 50;

    /// Each dripping hole counts as this many layer stages in the marked factor.
    public const double HoleStages = 3;
}

public enum LickState { Offered, Waiting, Paid, Done }

/// Lick clean for one partner after the local character's climax: offered, waiting for Cummy's answer, paid, or done.
/// Hole is where this character's climax went inside the partner, by bones; null when it landed outside. AtHole is
/// set when the lick on its way went to that hole's part, so it pays as licking up a creampie.
public sealed record LickOffer(PeerRecord Partner, DateTime Until, LickState State = LickState.Offered, bool Retried = false,
    string? Message = null, string? Hole = null, bool AtHole = false)
{
    public bool IsOpen(DateTime now) => now < Until && State is LickState.Offered or LickState.Waiting;

    public TimeSpan Left(DateTime now) => Until > now ? Until - now : TimeSpan.Zero;
}
