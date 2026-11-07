namespace Corruption.Core;

public enum VisualKind { Glamourer, Penumbra }

/// A look chosen for a tier: a Glamourer design, or a Penumbra mod with one option per group.
public sealed class TierVisual
{
    public VisualKind Kind { get; set; }

    public Guid DesignId { get; set; }

    public string ModDirectory { get; set; } = "";

    /// What to show for the choice.
    public string Name { get; set; } = "";

    /// Group name to option name.
    public Dictionary<string, string> Options { get; set; } = [];

    /// Two choices that apply the same look.
    public bool SameAs(TierVisual? other)
        => other is not null && other.Kind == Kind && other.DesignId == DesignId
        && string.Equals(other.ModDirectory, ModDirectory, StringComparison.Ordinal)
        && other.Options.Count == Options.Count && other.Options.All(kv => Options.TryGetValue(kv.Key, out var v) && v == kv.Value);
}

public static class TierVisualPlan
{
    /// The visual that should be on the character now, or null for none: the current tier's choice, unless there's no
    /// setup, the tier is Pure, or the player is paused with removal on.
    public static TierVisual? Wanted(CharacterSave save, bool removeWhilePaused)
    {
        if (save.Setup is null || (save.Paused && removeWhilePaused)) return null;
        var tier = Progression.TierOf(save.Level);
        if (tier == Tier.Pure) return null;
        return save.TierVisuals.TryGetValue(tier.ToString(), out var visual) ? visual : null;
    }
}

public static class PregnancyVisualPlan
{
    /// The trimesters a visual can be chosen for, as stored.
    public static readonly IReadOnlyList<string> Keys = ["First", "Second", "Third"];

    /// The current trimester's choice while pregnant, in the same cases tier visuals apply, or null.
    public static TierVisual? Wanted(CharacterSave save, DateTime now, bool removeWhilePaused)
    {
        if (save.Setup is null || (save.Paused && removeWhilePaused)) return null;
        var stage = PregnancyBook.StageOf(save.Pregnancy, now);
        if (stage is not (PregnancyStage.First or PregnancyStage.Second or PregnancyStage.Third)) return null;
        return save.Pregnancy.Visuals.TryGetValue(Keys[(int)stage - 1], out var visual) ? visual : null;
    }
}
