namespace Corruption.Core;

public enum MoodleKind { Aroused, Marked, Watched, Bonded, Refractory, Fading }

/// What Corruption's Moodles statuses should be right now.
/// <param name="Visible">False when hidden or before setup: no statuses, since they'd show the plugin.</param>
/// <param name="InScene">A verified scene is running.</param>
/// <param name="WithBondedPartner">The scene's partner is the formed bond.</param>
public readonly record struct MoodlesInput(bool Visible, double Arousal, int LayersWorn, bool InScene, int Watchers,
    bool WithBondedPartner, bool Refractory, bool Fading);

public static class MoodlesPlan
{
    public const int ArousedSteps = 5;

    /// Each status that applies, with its level: the Aroused step from 1 to 5, the Marked layer count, else 1.
    public static IReadOnlyDictionary<MoodleKind, int> Plan(in MoodlesInput input)
    {
        var plan = new Dictionary<MoodleKind, int>();
        if (!input.Visible) return plan;

        var step = ArousedStep(input.Arousal);
        if (step > 0) plan[MoodleKind.Aroused] = step;
        if (input.LayersWorn > 0) plan[MoodleKind.Marked] = input.LayersWorn;
        if (input.InScene && input.Watchers > 0) plan[MoodleKind.Watched] = 1;
        if (input.InScene && input.WithBondedPartner) plan[MoodleKind.Bonded] = 1;
        if (input.Refractory) plan[MoodleKind.Refractory] = 1;
        if (input.Fading) plan[MoodleKind.Fading] = 1;
        return plan;
    }

    /// ceil(arousal / 20), so 0 has none and 100 is step 5.
    public static int ArousedStep(double arousal)
        => (int)Math.Clamp(Math.Ceiling(PlateComposer.ArousalNumber(arousal) / 20.0), 0, ArousedSteps);
}
