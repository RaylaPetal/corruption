namespace Corruption.Core;

/// Starting over at level 100 for a star and a permanent bonus.
public static class PrestigeRules
{
    /// Why prestige isn't possible right now, or null when it is.
    public static string? Refusal(CharacterSave save, Balance b)
        => save.Setup is null ? "Finish your profile setup first."
            : save.Level < b.LevelCap ? $"Prestige needs level {b.LevelCap} (you're level {save.Level})."
            : null;

    /// Back to level 1 with no XP or arousal, plus a star. Everything else stays. False when refused.
    public static bool Apply(CharacterSave save, DateTime now, Balance b)
    {
        if (Refusal(save, b) is not null) return false;
        save.Level = 1;
        save.Xp = 0;
        save.Arousal = 0;
        save.ArousalAnchorUtc = now;
        save.Prestige++;
        return true;
    }

    /// 5% per star, up to the stars that pay a bonus. Stars beyond that are the cosmetic Eternal rank.
    public static double Factor(int stars, Balance b) => 1 + b.XpPerStar * Math.Clamp(stars, 0, b.StarsWithBonus);

    /// Empty at 0, ★3 up to 10, then ★ Eternal 2 for 12.
    public static string Label(int stars, int starsWithBonus = 10)
        => stars <= 0 ? ""
            : stars <= starsWithBonus ? $"★{stars}"
            : $"★ Eternal {stars - starsWithBonus}";
}
