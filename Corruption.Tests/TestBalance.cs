using Corruption.Core;

namespace Corruption.Tests;

/// The values the rule tests were written against, so they test mechanics rather than tuning. Tuning itself is tested
/// against the shipped defaults in PaceTests.
public static class TestBalance
{
    public static Balance Classic() => new Balance
    {
        ArousalPerTick = 3,
        ClimaxXp = 150,
        LayerXp = 75,
        FirstPartnerXp = 200,
        FirstActXp = 100,
    }.WithoutMoon();

    /// Moon events depend on the clock. Rule tests turn them off so the test's start time can't change their numbers.
    public static Balance WithoutMoon(this Balance b)
    {
        b.FullMoonArousalFactor = 1;
        b.NewMoonSceneSinsFactor = 1;
        return b;
    }
}
