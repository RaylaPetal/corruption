namespace Corruption.Core;

/// Turns the scene into a vibration intensity from 0 to 1: arousal times the maximum during a verified scene, the
/// maximum for a few seconds after a climax, and 0 otherwise. Only says when to send.
public sealed class ToyController
{
    public const double BurstSeconds = 3;
    public const double MinChange = 0.02;
    public const double MinGapSeconds = 1;

    private double sent;
    private DateTime lastSent = DateTime.MinValue;

    /// What the devices were last told.
    public double Current => sent;

    public static double Target(DateTime now, bool verifiedScene, double arousal, double maximum, DateTime? lastClimaxUtc)
    {
        maximum = Math.Clamp(maximum, 0, 1);
        if (lastClimaxUtc is { } climax && now >= climax && (now - climax).TotalSeconds < BurstSeconds) return maximum;
        return verifiedScene ? Math.Clamp(arousal / 100, 0, 1) * maximum : 0;
    }

    /// The intensity to send now, if any. Stopping and the start of a burst always go at once; anything else waits for a
    /// change of at least 2 points, at most once a second.
    public double? Update(DateTime now, bool verifiedScene, double arousal, double maximum, DateTime? lastClimaxUtc)
    {
        var target = Target(now, verifiedScene, arousal, maximum, lastClimaxUtc);
        if (target == sent) return null;

        var immediate = target == 0 || target >= Math.Clamp(maximum, 0, 1) && target > sent;
        if (!immediate)
        {
            if (Math.Abs(target - sent) < MinChange) return null;
            if ((now - lastSent).TotalSeconds < MinGapSeconds) return null;
        }
        sent = target;
        lastSent = now;
        return target;
    }

    /// The devices were stopped directly, such as by the safeword.
    public void Stopped() => sent = 0;
}
