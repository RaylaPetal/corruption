namespace Corruption.Core;

public enum MoodleKind { Aroused, Marked, Watched, Bonded, Refractory, Fading, Fertile, Pregnant, Craving, Unsatisfied, Recovering }

/// What Corruption's Moodles statuses should be right now.
/// <param name="Visible">False when hidden or before setup: no statuses, since they'd show the plugin.</param>
/// <param name="InScene">A verified scene is running.</param>
/// <param name="WithBondedPartner">The scene's partner is the formed bond.</param>
/// <param name="Trimester">1 to 3 while pregnant, else 0.</param>
public readonly record struct MoodlesInput(bool Visible, double Arousal, int LayersWorn, bool InScene, int Watchers,
    bool WithBondedPartner, bool Refractory, bool Fading,
    bool Fertile = false, int Trimester = 0, bool Craving = false, bool Unsatisfied = false, bool Recovering = false);

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
        if (input.Fertile) plan[MoodleKind.Fertile] = 1;
        if (input.Trimester is >= 1 and <= 3) plan[MoodleKind.Pregnant] = input.Trimester;
        if (input.Craving) plan[MoodleKind.Craving] = 1;
        if (input.Unsatisfied) plan[MoodleKind.Unsatisfied] = 1;
        if (input.Recovering) plan[MoodleKind.Recovering] = 1;
        return plan;
    }

    /// ceil(arousal / 20), so 0 has none and 100 is step 5.
    public static int ArousedStep(double arousal)
        => (int)Math.Clamp(Math.Ceiling(PlateComposer.ArousalNumber(arousal) / 20.0), 0, ArousedSteps);
}
