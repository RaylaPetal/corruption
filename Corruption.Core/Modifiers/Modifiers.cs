namespace Corruption.Core;

/// Everything a title, personality or trait can change. Every field is neutral by default, so a call site can always
/// apply it: a factor of 1, an added value of 0, or null to keep the base value. Sources combine with
/// <see cref="Combine"/>, so each value is applied at one place whatever its sources.
public sealed record Modifiers
{
    public static readonly Modifiers None = new();

    // ── Tick XP ────────────────────────────────────────────────────────────────

    /// Tick XP from a title. Personalities and traits use <see cref="PersonaTickXp"/>, so the two show apart.
    public double TickXp { get; init; } = 1;

    /// Tick XP from a title, only during a Full Moon.
    public double FullMoonTickXp { get; init; } = 1;

    /// Tick XP from a personality or trait.
    public double PersonaTickXp { get; init; } = 1;

    /// Tick XP when nobody is watching.
    public double QuietTickXp { get; init; } = 1;

    /// Tick XP with someone other than the formed bond, while bonded.
    public double AwayFromBondTickXp { get; init; } = 1;

    /// Added to the bonded factor.
    public double BondedExtra { get; init; }

    // ── Arousal and climax ─────────────────────────────────────────────────────

    public double Arousal { get; init; } = 1;

    /// Arousal gain from <see cref="TitleRules.HighArousal"/> up.
    public double HighArousal { get; init; } = 1;

    /// Arousal gain when nobody is watching.
    public double QuietArousal { get; init; } = 1;

    /// How fast arousal decays out of a scene.
    public double ArousalDecay { get; init; } = 1;

    public double Refractory { get; init; } = 1;

    /// The climaxing player's own climax XP.
    public double ClimaxXp { get; init; } = 1;

    /// Arousal right after a climax, instead of the balance value.
    public double? ArousalAfterClimax { get; init; }

    /// A lower `/corr climax` threshold, applied before <see cref="ManualClimaxShift"/>.
    public double? ManualClimaxMinimum { get; init; }

    /// Added to every `/corr climax` threshold, the Hair Trigger's included.
    public double ManualClimaxShift { get; init; }

    // ── Watchers, streak, marks and fatigue ────────────────────────────────────

    public double? WatcherCap { get; init; }
    public double? StreakCap { get; init; }
    public double? MarkedCap { get; init; }

    /// What each layer worn adds to the marked factor, instead of the balance value.
    public double? MarkedPerLayer { get; init; }

    /// How much each watcher counts.
    public double WatcherCount { get; init; } = 1;

    /// A missed day ends the streak rather than halving it.
    public bool StreakEndsOnMiss { get; init; }

    /// Minutes added to the fatigue grace period. Can be negative; the grace never goes below 0.
    public double FatigueGraceMinutes { get; init; }

    /// How much each edged second counts toward goals and achievements.
    public double EdgedCount { get; init; } = 1;

    // ── Partners and bonds ─────────────────────────────────────────────────────

    public double FirstPartnerXp { get; init; } = 1;
    public double FirstActXp { get; init; } = 1;
    public double? PartnerClimaxShare { get; init; }
    public double BondTime { get; init; } = 1;

    /// XP and Sins for climaxing together.
    public double TogetherPay { get; init; } = 1;
    public double? TogetherSeconds { get; init; }

    // ── Pregnancy ──────────────────────────────────────────────────────────────

    public double ConceptionOwn { get; init; } = 1;

    /// Partners' conception chance with the wearer. Partners read it from the wearer's tags.
    public double ConceptionPartner { get; init; } = 1;

    public double BirthSins { get; init; } = 1;
    public double CravingSins { get; init; } = 1;

    // ── Sins and items ─────────────────────────────────────────────────────────

    public double SceneSins { get; init; } = 1;
    public double ItemPrice { get; init; } = 1;

    /// Price factors for single items, by id.
    public IReadOnlyDictionary<string, double> ItemPriceFor { get; init; } = new Dictionary<string, double>();

    /// How long timed items last.
    public double ItemDuration { get; init; } = 1;

    public int CheckInBonus { get; init; }

    /// Gets Pent Up after a day without a scene.
    public bool PentUp { get; init; }

    public double PriceFactorFor(string itemId) => ItemPrice * ItemPriceFor.GetValueOrDefault(itemId, 1);

    /// Several sources as one: factors multiply, added values add, caps and set values take the highest, and the manual
    /// climax threshold takes the lowest.
    public static Modifiers Combine(params IEnumerable<Modifiers> sources)
    {
        var all = sources.ToList();
        if (all.Count == 0) return None;
        if (all.Count == 1) return all[0];

        double Product(Func<Modifiers, double> f) => all.Aggregate(1.0, (p, m) => p * f(m));
        double Sum(Func<Modifiers, double> f) => all.Sum(f);
        double? Highest(Func<Modifiers, double?> f) => all.Select(f).Where(v => v is not null).Max();
        double? Lowest(Func<Modifiers, double?> f) => all.Select(f).Where(v => v is not null).Min();

        var prices = new Dictionary<string, double>();
        foreach (var (id, factor) in all.SelectMany(m => m.ItemPriceFor))
            prices[id] = prices.GetValueOrDefault(id, 1) * factor;

        return new Modifiers
        {
            TickXp = Product(m => m.TickXp),
            FullMoonTickXp = Product(m => m.FullMoonTickXp),
            PersonaTickXp = Product(m => m.PersonaTickXp),
            QuietTickXp = Product(m => m.QuietTickXp),
            AwayFromBondTickXp = Product(m => m.AwayFromBondTickXp),
            BondedExtra = Sum(m => m.BondedExtra),
            Arousal = Product(m => m.Arousal),
            HighArousal = Product(m => m.HighArousal),
            QuietArousal = Product(m => m.QuietArousal),
            ArousalDecay = Product(m => m.ArousalDecay),
            Refractory = Product(m => m.Refractory),
            ClimaxXp = Product(m => m.ClimaxXp),
            ArousalAfterClimax = Highest(m => m.ArousalAfterClimax),
            ManualClimaxMinimum = Lowest(m => m.ManualClimaxMinimum),
            ManualClimaxShift = Sum(m => m.ManualClimaxShift),
            WatcherCap = Highest(m => m.WatcherCap),
            StreakCap = Highest(m => m.StreakCap),
            MarkedCap = Highest(m => m.MarkedCap),
            MarkedPerLayer = Highest(m => m.MarkedPerLayer),
            WatcherCount = Product(m => m.WatcherCount),
            StreakEndsOnMiss = all.Any(m => m.StreakEndsOnMiss),
            FatigueGraceMinutes = Sum(m => m.FatigueGraceMinutes),
            EdgedCount = Product(m => m.EdgedCount),
            FirstPartnerXp = Product(m => m.FirstPartnerXp),
            FirstActXp = Product(m => m.FirstActXp),
            PartnerClimaxShare = Highest(m => m.PartnerClimaxShare),
            BondTime = Product(m => m.BondTime),
            TogetherPay = Product(m => m.TogetherPay),
            TogetherSeconds = Highest(m => m.TogetherSeconds),
            ConceptionOwn = Product(m => m.ConceptionOwn),
            ConceptionPartner = Product(m => m.ConceptionPartner),
            BirthSins = Product(m => m.BirthSins),
            CravingSins = Product(m => m.CravingSins),
            SceneSins = Product(m => m.SceneSins),
            ItemPrice = Product(m => m.ItemPrice),
            ItemPriceFor = prices,
            ItemDuration = Product(m => m.ItemDuration),
            CheckInBonus = all.Sum(m => m.CheckInBonus),
            PentUp = all.Any(m => m.PentUp),
        };
    }
}

/// The modifiers that apply to a character right now, from every source.
public static class ModifierBook
{
    /// The equipped title combined with the personality and active traits.
    public static Modifiers For(CharacterSave save) => Combine(save, TitleBook.Bonus(save));

    private static Modifiers Combine(CharacterSave save, Modifiers title) => Modifiers.Combine(title, PersonaBook.Effects(save));
}
