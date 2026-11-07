namespace Corruption.Core;

/// One character's Sins and what's owed toward them. Stored in the save.
public sealed class Wallet
{
    public long Sins { get; set; }

    /// The UTC day of the last check-in, and how many days in a row it's been.
    public DateTime CheckInDayUtc { get; set; }
    public int CheckInStreak { get; set; }

    /// Every Sin ever earned, for the stats.
    public long EarnedTotal { get; set; }

    /// Everything spent in the shop, for the Big Spender achievement.
    public long SpentTotal { get; set; }

    /// Verified scene seconds not yet paid out as a whole minute.
    public double SceneSecondsUnpaid { get; set; }
}

/// What pays Sins, and how much. Sins aren't scaled by difficulty.
public static class Sins
{
    public const int CheckInBase = 20;
    public const int CheckInPerDay = 5;
    public const int CheckInCap = 50;
    public const int PerSceneMinute = 1;
    public const int PerLevelPerTier = 5;
    public const int DailyGoal = 15;
    public const int WeeklyGoal = 60;
    public const int Achievement = 100;
    public const int Craving = 10;
    public const int Birth = 150;
    public const int Together = 30;

    /// The day's check-in, once per UTC day. 0 when it's already been paid today.
    public static int CheckIn(Wallet wallet, DateTime now)
    {
        var today = now.Date;
        if (wallet.CheckInDayUtc == today) return 0;
        wallet.CheckInStreak = wallet.CheckInDayUtc == today.AddDays(-1) ? wallet.CheckInStreak + 1 : 1;
        wallet.CheckInDayUtc = today;
        var pay = Math.Min(CheckInCap, CheckInBase + CheckInPerDay * (wallet.CheckInStreak - 1));
        wallet.Sins += pay;
        wallet.EarnedTotal += pay;
        return pay;
    }

    /// Scene time, paid a Sin per whole minute. Returns what was paid.
    public static int ForScene(Wallet wallet, double seconds)
    {
        wallet.SceneSecondsUnpaid += seconds;
        var minutes = (int)Math.Floor(wallet.SceneSecondsUnpaid / 60);
        wallet.SceneSecondsUnpaid -= minutes * 60;
        wallet.Sins += minutes * PerSceneMinute;
        wallet.EarnedTotal += minutes * PerSceneMinute;
        return minutes * PerSceneMinute;
    }

    /// Reaching <paramref name="level"/>: 5 times (tier number + 1).
    public static int ForLevel(int level) => PerLevelPerTier * ((int)Progression.TierOf(level) + 1);

    public static int ForReward(RewardKind kind) => kind switch
    {
        RewardKind.Daily => DailyGoal,
        RewardKind.Weekly => WeeklyGoal,
        _ => Achievement,
    };

    public static void Earn(Wallet wallet, long amount)
    {
        wallet.Sins += Math.Max(0, amount);
        wallet.EarnedTotal += Math.Max(0, amount);
    }

    /// Takes the price if there's enough. False, with nothing taken, otherwise.
    public static bool TrySpend(Wallet wallet, long price)
    {
        if (price < 0 || wallet.Sins < price) return false;
        wallet.Sins -= price;
        wallet.SpentTotal += price;
        return true;
    }
}
