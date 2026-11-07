using System.Numerics;

namespace Corruption.Core;

/// Turns once-a-second samples into 10-second ticks. A failed sample voids the tick in progress and nothing else, and
/// a change of partner or act starts a new one.
public sealed class SceneTicker
{
    private DateTime? windowStart;
    private string? partner;
    private string? act;

    public void Reset()
    {
        windowStart = null;
        partner = null;
        act = null;
    }

    /// True when this sample completes a verified tick.
    public bool Sample(DateTime now, bool passed, string? partnerKey, string? actKey, double tickSeconds)
    {
        if (!passed)
        {
            Reset();
            return false;
        }
        if (windowStart is null || partnerKey != partner || actKey != act)
        {
            windowStart = now;
            partner = partnerKey;
            act = actKey;
            return false;
        }
        if ((now - windowStart.Value).TotalSeconds < tickSeconds) return false;
        windowStart = now;
        return true;
    }
}
