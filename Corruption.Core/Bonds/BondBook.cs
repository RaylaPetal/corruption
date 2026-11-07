namespace Corruption.Core;

/// Requests, forms and ends this character's bond from what nearby extra tags say. The save holds the request or bond;
/// who wants to bond with this character is only known while they're nearby.
public sealed class BondBook(CharacterSave save)
{
    public const Tier MinimumTier = Tier.Tainted;

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

    /// The hash this character's extra tag carries.
    public string? OwnTagHash => save.BondWith is { } key ? BondHashes.Of(key) : null;

    /// Why the Bond button isn't offered for this player, or null when it is.
    public string? WhyNot(int ownLevel, int theirLevel)
    {
        if (Progression.TierOf(ownLevel) < MinimumTier) return $"Bonding opens at the {MinimumTier} tier.";
        if (Progression.TierOf(theirLevel) < MinimumTier) return $"They need to reach the {MinimumTier} tier.";
        return null;
    }

    /// Asks a player to bond, or bonds back. A waiting request to someone else is withdrawn. A formed bond has to be ended
    /// first. Null on success, otherwise why not.
    public string? Request(string playerKey, int ownLevel, int theirLevel)
    {
        if (WhyNot(ownLevel, theirLevel) is { } reason) return reason;
        if (save.BondFormed && save.BondWith is { } bonded && !string.Equals(bonded, playerKey, StringComparison.OrdinalIgnoreCase))
            return "You're already bonded. Unbond first.";
        if (save.BondFormed) return null;
        save.BondWith = playerKey;
        return null;
    }

    /// Ends the bond or withdraws the request.
    public void Unbond()
    {
        save.BondWith = null;
        save.BondFormed = false;
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
