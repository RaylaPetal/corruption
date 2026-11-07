namespace Corruption.Core;

/// Requests, forms and ends this character's bond from what nearby extra tags say. The save holds the request or bond;
/// who wants to bond with this character is only known while they're nearby.
public sealed class BondBook(CharacterSave save)
{
    private readonly HashSet<string> wantsToBond = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> namesMe = new(StringComparer.OrdinalIgnoreCase);

    /// Nearby players whose tag names this character while this character hasn't asked them.
    public IReadOnlySet<string> WantsToBond => wantsToBond;

    public bool IsBondedWith(string playerKey)
        => save.BondFormed && string.Equals(save.BondWith, playerKey, StringComparison.OrdinalIgnoreCase);

    public bool IsRequesting(string playerKey)
        => !save.BondFormed && string.Equals(save.BondWith, playerKey, StringComparison.OrdinalIgnoreCase);

    /// The bonded factor applies: the bond is formed with them, and their tag, seen this second, still names this player.
    public bool PaysWith(string playerKey) => IsBondedWith(playerKey) && namesMe.Contains(playerKey);

    /// The bond's level from time together, 0 with no formed bond.
    public int Level => save.BondFormed ? BondLevels.LevelOf(save.BondSeconds) : 0;

    /// The bond's level when ticking with this player, or 0 when the bond doesn't pay with them right now.
    public int LevelWith(string playerKey) => PaysWith(playerKey) ? Level : 0;

    /// The hash this character's extra tag carries.
    public string? OwnTagHash => save.BondWith is { } key ? BondHashes.Of(key) : null;

    /// Asks a player to bond, or bonds back. A waiting request to someone else is withdrawn. A formed bond has to be ended
    /// first. Null on success, otherwise why not.
    public string? Request(string playerKey)
    {
        if (save.BondFormed && save.BondWith is { } bonded && !string.Equals(bonded, playerKey, StringComparison.OrdinalIgnoreCase))
            return "You're already bonded. Unbond first.";
        if (save.BondFormed) return null;
        save.BondWith = playerKey;
        save.BondSeconds = 0;
        return null;
    }

    /// Ends the bond or withdraws the request.
    public void Unbond()
    {
        save.BondWith = null;
        save.BondFormed = false;
        save.BondSeconds = 0;
    }

    /// Reads nearby extra tags. Returns a sentence to tell the player when the bond formed or ended, else null. Players
    /// without an extra tag, such as clients without bond support, change nothing.
    public string? Observe(string? ownKey, IEnumerable<(PeerRecord Player, ExtraTag? Extra)> nearby)
    {
        wantsToBond.Clear();
        namesMe.Clear();
        if (ownKey is null) return null;
        var myHash = BondHashes.Of(ownKey);
        string? message = null;

        foreach (var (player, extra) in nearby)
        {
            if (extra is not { } tag) continue;
            var theyNameMe = tag.BondHash == myHash;
            if (theyNameMe) namesMe.Add(player.Key);

            if (!string.Equals(save.BondWith, player.Key, StringComparison.OrdinalIgnoreCase))
            {
                if (theyNameMe) wantsToBond.Add(player.Key);
                continue;
            }

            if (!save.BondFormed && theyNameMe)
            {
                save.BondFormed = true;
                save.BondSeconds = 0;
                message = $"You're bonded with {player.Name}.";
            }
            else if (save.BondFormed && !theyNameMe)
            {
                Unbond();
                message = $"{player.Name} ended your bond.";
            }
        }
        return message;
    }
}
