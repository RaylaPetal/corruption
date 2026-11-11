namespace Corruption.Core;

/// The numbers of aftercare: licking a partner clean, and being licked or kissed.
public static class AftercareRules
{
    /// Lick clean is offered this long after the local character's climax with a partner.
    public static readonly TimeSpan OfferFor = TimeSpan.FromMinutes(2);

    public const double LickXp = 20;
    public const double LickBondSeconds = 60;

    /// Where a lick goes when the bones can't be read.
    public const string FallbackPart = "st";

    /// Being licked or kissed clean by someone you had a scene with this recently gives arousal, at most this often.
    public static readonly TimeSpan RecentPartner = TimeSpan.FromMinutes(2);
    public const double CleanedArousal = 5;
    public static readonly TimeSpan CleanedGap = TimeSpan.FromSeconds(30);
}

public enum LickState { Offered, Waiting, Paid, Done }

/// Lick clean for one partner after the local character's climax: offered, waiting for Cummy's answer, paid, or done.
public sealed record LickOffer(PeerRecord Partner, DateTime Until, LickState State = LickState.Offered, bool Retried = false,
    string? Message = null)
{
    public bool IsOpen(DateTime now) => now < Until && State is LickState.Offered or LickState.Waiting;

    public TimeSpan Left(DateTime now) => Until > now ? Until - now : TimeSpan.Zero;
}
