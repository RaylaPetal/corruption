using Corruption.Core;

namespace Corruption.Tests;

public class CumStatusTextTests
{
    [Theory]
    [InlineData(1, "Marked", "A fresh, sticky load clings to their skin.")]
    [InlineData(2, "Glazed", "Glazed with 2 warm, sticky loads, still glistening.")]
    [InlineData(4, "Glazed", "Glazed with 4 warm, sticky loads, still glistening.")]
    [InlineData(5, "Cum-Painted", "Painted in 5 thick loads. They've clearly been used.")]
    [InlineData(7, "Cum-Painted", "Painted in 7 thick loads. They've clearly been used.")]
    [InlineData(9, "Cum-Painted", "Painted in 9 thick loads. They've clearly been used.")]
    [InlineData(10, "Cum-Soaked", "Soaked in 10 loads head to toe, a dripping, well-used mess.")]
    [InlineData(32, "Cum-Soaked", "Soaked in 32 loads head to toe, a dripping, well-used mess.")]
    public void LayersByTier(int stages, string title, string description)
        => Assert.Equal((title, description), CumStatusText.Layers(stages));

    [Theory]
    [InlineData(MoodlesPlan.DrippingPussy, "Creampied")]
    [InlineData(MoodlesPlan.DrippingAss, "Ass Creampied")]
    [InlineData(MoodlesPlan.DrippingPussy | MoodlesPlan.DrippingAss, "Double Creampied")]
    public void CreampieByHoles(int holes, string title) => Assert.Equal(title, CumStatusText.Creampie(holes).Title);

    [Fact]
    public void EveryTextFitsMoodles()
    {
        var all = Enumerable.Range(1, 99).Select(CumStatusText.Layers)
            .Concat(new[] { 1, 2, 3 }.Select(CumStatusText.Creampie));
        Assert.All(all, t => Assert.InRange(t.Description.Length, 1, 80));
    }
}
