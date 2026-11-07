using System.Security.Cryptography;
using System.Text;

namespace Corruption.Core;

/// Bonds name the other player by a short hash, so a third synced player can't read who it is.
public static class BondHashes
{
    /// The first 8 hex digits of SHA-256 of the lowercased `Name@World` key.
    public static string Of(string playerKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(playerKey.ToLowerInvariant()));
        return Convert.ToHexStringLower(bytes, 0, 4);
    }

    public static bool IsHash(string text)
        => text.Length == 8 && text.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
