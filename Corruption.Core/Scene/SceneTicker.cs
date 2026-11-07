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

/// AFK: no change of emote or position for the configured time.
public sealed class AfkTracker
{
    private const float MoveThreshold = 0.05f;

    private PoseKey? lastPose;
    private Vector3 lastPosition;
    private DateTime lastChange;
    private bool started;

    public void Update(DateTime now, PoseKey? pose, Vector3 position)
    {
        if (!started || pose != lastPose || Vector3.Distance(position, lastPosition) > MoveThreshold)
        {
            started = true;
            lastPose = pose;
            lastPosition = position;
            lastChange = now;
        }
    }

    public bool IsAfk(DateTime now, double minutes) => started && (now - lastChange).TotalMinutes >= minutes;
}
