namespace Corruption.Core;

public enum ProfileStatus { NeedsSetup, Earning, Waiting, Idle, Paused, Hidden, Fading }

/// What the Profile page shows about time. Null times mean "doesn't apply".
/// <param name="SinceLastScene">Null when there's never been a verified scene.</param>
/// <param name="UntilDecay">Time until fading starts. Null when decay is Off, never played, or already fading.</param>
/// <param name="UntilNextFade">While fading, time until the next day's fade.</param>
public readonly record struct ProfileTimes(
    bool NeverPlayed,
    bool DecayOff,
    bool Fading,
    TimeSpan? SinceLastScene,
    TimeSpan? UntilDecay,
    TimeSpan? UntilNextFade);

public static class ProfileTimers
{
    /// Uses the same grace and day logic as <see cref="Progression.ApplyFading"/>.
    public static ProfileTimes Compute(CharacterSave save, DateTime now)
    {
        if (save.Setup is not { } setup) return new ProfileTimes(true, false, false, null, null, null);

        var neverPlayed = !save.Goals.Stats.TryGetValue(nameof(GoalKind.SceneSeconds), out var seconds) || seconds <= 0;
        var decayOff = setup.Decay == DecayMode.Off;
        TimeSpan? since = neverPlayed || save.LastVerifiedUtc == default ? null : Max(TimeSpan.Zero, now - save.LastVerifiedUtc);
        if (decayOff || save.LastVerifiedUtc == default) return new ProfileTimes(neverPlayed, decayOff, false, since, null, null);

        var graceEnd = save.LastVerifiedUtc.AddHours(setup.DecayRule.GraceHours);
        if (now < graceEnd) return new ProfileTimes(neverPlayed, false, false, since, graceEnd - now, null);

        var start = save.FadedThroughUtc > graceEnd ? save.FadedThroughUtc : graceEnd;
        var days = Math.Max(0, Math.Floor((now - start).TotalDays));
        var next = start.AddDays(days + 1);
        return new ProfileTimes(neverPlayed, false, true, since, null, next - now);
    }

    public static ProfileStatus StatusOf(CharacterSave save, SceneState scene, DateTime now)
    {
        if (save.Setup is null) return ProfileStatus.NeedsSetup;
        if (save.Hidden) return ProfileStatus.Hidden;
        if (save.Paused) return ProfileStatus.Paused;
        if (scene == SceneState.Earning) return ProfileStatus.Earning;
        if (scene == SceneState.Waiting) return ProfileStatus.Waiting;
        return Progression.IsFading(save, now) ? ProfileStatus.Fading : ProfileStatus.Idle;
    }

    /// Like 2d 4h, 3h 12m or 45s.
    public static string Describe(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d {span.Hours}h";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m";
        if (span.TotalMinutes >= 1) return $"{span.Minutes}m {span.Seconds}s";
        return $"{span.Seconds}s";
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
