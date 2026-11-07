namespace Corruption.Core;

public enum ItemKind { Timed, Counted, Instant }

/// Who can be offered an item. Carry and Breed need pregnancy on with that role.
public enum ItemRole { Anyone, Carry, Breed }

public sealed record ItemDef(string Id, string Name, int Price, ItemKind Kind, ItemRole Role, string Effect,
    TimeSpan? Duration = null, int Uses = 0, string? Opposite = null);

/// An item working now: until a time, or for a number of uses.
public sealed class ActiveEffect
{
    public DateTime? UntilUtc { get; set; }
    public int UsesLeft { get; set; }
}

public static class ItemIds
{
    public const string Aphrodisiac = "aphrodisiac";
    public const string NumbingBalm = "numbing_balm";
    public const string LustIncense = "lust_incense";
    public const string Perfume = "perfume";
    public const string FatigueSalve = "fatigue_salve";
    public const string AfterglowWine = "afterglow_wine";
    public const string HairTrigger = "hair_trigger";
    public const string CumMagnet = "cum_magnet";
    public const string StreakCharm = "streak_charm";
    public const string FertilityCharm = "fertility_charm";
    public const string VirilityTonic = "virility_tonic";
    public const string Contraceptive = "contraceptive";
    public const string MorningAfter = "morning_after";
    public const string HormoneTea = "hormone_tea";
    public const string SwiftLabour = "swift_labour";
    public const string CravingTreat = "craving_treat";
}

public static class ItemCatalog
{
    private static TimeSpan Minutes(int m) => TimeSpan.FromMinutes(m);

    public static readonly IReadOnlyList<ItemDef> All =
    [
        new(ItemIds.Aphrodisiac, "Aphrodisiac", 40, ItemKind.Timed, ItemRole.Anyone, "Arousal builds 1.5× as fast", Minutes(30), Opposite: ItemIds.NumbingBalm),
        new(ItemIds.NumbingBalm, "Numbing Balm", 40, ItemKind.Timed, ItemRole.Anyone, "Arousal builds 0.6× as fast, for longer scenes", Minutes(30), Opposite: ItemIds.Aphrodisiac),
        new(ItemIds.LustIncense, "Lust Incense", 80, ItemKind.Timed, ItemRole.Anyone, "Scene XP ×1.25", Minutes(60)),
        new(ItemIds.Perfume, "Exhibitionist's Perfume", 60, ItemKind.Timed, ItemRole.Anyone, "Every watcher counts twice", Minutes(60)),
        new(ItemIds.FatigueSalve, "Fatigue Salve", 50, ItemKind.Timed, ItemRole.Anyone, "No fatigue from playing one animation long", Minutes(30)),
        new(ItemIds.AfterglowWine, "Afterglow Wine", 50, ItemKind.Timed, ItemRole.Anyone, "Refractory period halved", Minutes(60)),
        new(ItemIds.HairTrigger, "Hair Trigger", 70, ItemKind.Counted, ItemRole.Anyone, "Your next /corr climax works from 40 arousal", Uses: 1),
        new(ItemIds.CumMagnet, "Cum Magnet", 60, ItemKind.Counted, ItemRole.Anyone, "A partner's climax layers two parts instead of one", Uses: 3),
        new(ItemIds.StreakCharm, "Streak Charm", 100, ItemKind.Counted, ItemRole.Anyone, "A missed day doesn't halve your streak", Uses: 1),
        new(ItemIds.FertilityCharm, "Fertility Charm", 60, ItemKind.Timed, ItemRole.Carry, "Your conception chance ×2", TimeSpan.FromHours(24), Opposite: ItemIds.Contraceptive),
        new(ItemIds.VirilityTonic, "Virility Tonic", 60, ItemKind.Timed, ItemRole.Breed, "Partners' conception chance with you ×1.5", TimeSpan.FromHours(24)),
        new(ItemIds.Contraceptive, "Contraceptive Pill", 30, ItemKind.Timed, ItemRole.Carry, "You can't conceive", TimeSpan.FromHours(24), Opposite: ItemIds.FertilityCharm),
        new(ItemIds.MorningAfter, "Morning-After Pill", 80, ItemKind.Instant, ItemRole.Carry, "Ends a pregnancy conceived in the last 24 hours"),
        new(ItemIds.HormoneTea, "Hormone Tea", 60, ItemKind.Instant, ItemRole.Carry, "Moves your cycle to the start of its fertile window"),
        new(ItemIds.SwiftLabour, "Swift Labour", 120, ItemKind.Instant, ItemRole.Carry, "Takes 2 days off the rest of your pregnancy"),
        new(ItemIds.CravingTreat, "Craving Treat", 25, ItemKind.Instant, ItemRole.Carry, "Satisfies today's craving"),
    ];

    public static ItemDef? Find(string id) => All.FirstOrDefault(i => i.Id == id);

    /// Most a timed item can be stacked to, from now: four times its duration.
    public const int MaxStack = 4;
}

/// Buying, using and the effects of items, for one character.
public static class ItemBook
{
    public static bool IsOffered(CharacterSave save, ItemDef item) => item.Role switch
    {
        ItemRole.Carry => save.Pregnancy.Enabled && save.Pregnancy.CanCarry,
        ItemRole.Breed => save.Pregnancy.Enabled && save.Pregnancy.CanBreed,
        _ => true,
    };

    public static IEnumerable<ItemDef> Offered(CharacterSave save) => ItemCatalog.All.Where(i => IsOffered(save, i));

    public static int Owned(CharacterSave save, string id) => save.Inventory.GetValueOrDefault(id);

    /// Null when bought, otherwise why not.
    public static string? Buy(CharacterSave save, string id)
    {
        if (save.Setup is null) return "Finish your profile setup first.";
        if (ItemCatalog.Find(id) is not { } item) return "That item doesn't exist.";
        if (!IsOffered(save, item)) return $"{item.Name} needs pregnancy turned on with the right role.";
        if (!Sins.TrySpend(save.Wallet, item.Price)) return $"You need {item.Price - save.Wallet.Sins} more Sins for {item.Name}.";
        save.Inventory[id] = Owned(save, id) + 1;
        return null;
    }

    /// Uses one from the inventory. Instant items run <paramref name="instant"/>, which returns why it can't apply; the
    /// item is only used up when it does. Null on success, otherwise why not.
    public static string? Use(CharacterSave save, string id, DateTime now, Func<ItemDef, string?>? instant = null)
    {
        if (ItemCatalog.Find(id) is not { } item) return "That item doesn't exist.";
        if (Owned(save, id) < 1) return $"You don't have a {item.Name}.";
        Expire(save, now);

        switch (item.Kind)
        {
            case ItemKind.Instant:
                if (instant is null) return $"{item.Name} can't be used right now.";
                if (instant(item) is { } refusal) return refusal;
                break;
            case ItemKind.Counted:
                var counted = save.Effects.TryGetValue(id, out var c) ? c : save.Effects[id] = new ActiveEffect();
                counted.UsesLeft += item.Uses;
                break;
            default:
                var duration = item.Duration ?? TimeSpan.Zero;
                if (item.Opposite is { } opposite) save.Effects.Remove(opposite);
                var until = save.Effects.TryGetValue(id, out var e) && e.UntilUtc is { } current && current > now ? current + duration : now + duration;
                var cap = now + duration * ItemCatalog.MaxStack;
                save.Effects[id] = new ActiveEffect { UntilUtc = until > cap ? cap : until };
                break;
        }

        if (--save.Inventory[id] <= 0) save.Inventory.Remove(id);
        return null;
    }

    /// Drops effects whose time or uses have run out, including while logged out.
    public static void Expire(CharacterSave save, DateTime now)
    {
        foreach (var (id, effect) in save.Effects.ToList())
        {
            var timedOut = effect.UntilUtc is { } until && until <= now;
            var usedUp = effect.UntilUtc is null && effect.UsesLeft <= 0;
            if (timedOut || usedUp) save.Effects.Remove(id);
        }
    }

    public static bool IsActive(CharacterSave save, string id, DateTime now)
        => save.Effects.TryGetValue(id, out var e) && (e.UntilUtc is { } until ? until > now : e.UsesLeft > 0);

    /// Spends one use of a counted effect. False when there's none.
    public static bool TrySpendUse(CharacterSave save, string id)
    {
        if (!save.Effects.TryGetValue(id, out var e) || e.UntilUtc is not null || e.UsesLeft <= 0) return false;
        if (--e.UsesLeft <= 0) save.Effects.Remove(id);
        return true;
    }

    // ── What effects do ───────────────────────────────────────────────────────

    public static double ArousalFactor(CharacterSave save, DateTime now)
        => (IsActive(save, ItemIds.Aphrodisiac, now) ? 1.5 : 1) * (IsActive(save, ItemIds.NumbingBalm, now) ? 0.6 : 1);

    public static double TickXpFactor(CharacterSave save, DateTime now) => IsActive(save, ItemIds.LustIncense, now) ? 1.25 : 1;

    public static int WatcherMultiplier(CharacterSave save, DateTime now) => IsActive(save, ItemIds.Perfume, now) ? 2 : 1;

    public static bool NoFatigue(CharacterSave save, DateTime now) => IsActive(save, ItemIds.FatigueSalve, now);

    public static double RefractoryFactor(CharacterSave save, DateTime now) => IsActive(save, ItemIds.AfterglowWine, now) ? 0.5 : 1;

    public const double HairTriggerMinimum = 40;

    public static double ConceptionFactor(CharacterSave save, DateTime now) => IsActive(save, ItemIds.FertilityCharm, now) ? 2 : 1;

    public static bool PreventsConception(CharacterSave save, DateTime now) => IsActive(save, ItemIds.Contraceptive, now);

    public static bool Virile(CharacterSave save, DateTime now) => IsActive(save, ItemIds.VirilityTonic, now);
}
