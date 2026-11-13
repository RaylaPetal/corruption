namespace Corruption.Core;

/// The title and description of the two Cummy statuses Corruption shows through Moodles: layers worn, and creampies.
/// They get lewder as the character gets messier, and never name a partner, since Moodles show to everyone synced.
public static class CumStatusText
{
    /// By the number of layer stages worn: Marked (1), Glazed (2-4), Cum-Painted (5-9), Cum-Soaked (10 or more).
    public static (string Title, string Description) Layers(int stages) => Math.Max(1, stages) switch
    {
        1 => ("Marked", "A fresh, sticky load clings to their skin."),
        var n and <= 4 => ("Glazed", $"Glazed with {n} warm, sticky loads, still glistening."),
        var n and <= 9 => ("Cum-Painted", $"Painted in {n} thick loads. They've clearly been used."),
        var n => ("Cum-Soaked", $"Soaked in {n} loads head to toe, a dripping, well-used mess."),
    };

    /// By the dripping holes, as MoodlesPlan's mask: pussy, ass, or both.
    public static (string Title, string Description) Creampie(int holes) => holes switch
    {
        MoodlesPlan.DrippingPussy => ("Creampied", "Their pussy was just filled, and a thick, warm load keeps seeping out."),
        MoodlesPlan.DrippingAss => ("Ass Creampied", "Their ass was just filled, and cum keeps trickling out of it."),
        _ => ("Double Creampied", "Stuffed in both holes, with cum leaking from their pussy and their ass."),
    };
}
