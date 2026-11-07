using Corruption.Core;

namespace Corruption.Tests;

/// The values the rule tests were written against, so they test mechanics rather than tuning. Tuning itself is tested
/// against the shipped defaults in PaceTests.
public static class TestBalance
{
    public static Balance Classic() => new()
    {
        ArousalPerTick = 3,
        ClimaxXp = 150,
        LayerXp = 75,
        FirstPartnerXp = 200,
        FirstActXp = 100,
    };
}
