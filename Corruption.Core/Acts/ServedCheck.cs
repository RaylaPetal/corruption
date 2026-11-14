namespace Corruption.Core;

/// Whether Penumbra is serving a mod's own animation file. It answers for temporary settings, inheritance and priority
/// at once, because it compares what Penumbra actually resolves.
public static class ServedCheck
{
    /// <param name="modRoot">Penumbra's mod folder.</param>
    /// <param name="redirects">Game path to the mod-relative files that option can point it at.</param>
    /// <param name="resolved">Game path to what Penumbra resolves it to for this character.</param>
    /// <param name="raceCode">The character's race code, as "c0201". Paths for other races are ignored when any path is
    /// for this race; an option with none relies on the game's fallback skeleton, so all its paths count.</param>
    public static bool IsServed(string modRoot, string modDirectory, IReadOnlyDictionary<string, IReadOnlyCollection<string>> redirects,
        IReadOnlyDictionary<string, string> resolved, string? raceCode)
    {
        var root = Normalize(modRoot).TrimEnd('/');
        foreach (var (gamePath, files) in Relevant(redirects, raceCode))
        {
            if (!resolved.TryGetValue(gamePath, out var actual)) continue;
            var normalized = Normalize(actual);
            if (files.Any(file => normalized == Normalize($"{root}/{modDirectory}/{file}"))) return true;
        }
        return false;
    }

    /// When a mod's animation isn't served, what Penumbra serves instead for its first path: another mod's folder, ""
    /// for the game's own file, the whole path for a file outside the mod folder, or null when nothing resolved.
    public static string? ServedInstead(string modRoot, IReadOnlyDictionary<string, IReadOnlyCollection<string>> redirects,
        IReadOnlyDictionary<string, string> resolved, string? raceCode)
    {
        var root = Normalize(modRoot).TrimEnd('/') + "/";
        foreach (var (gamePath, _) in Relevant(redirects, raceCode))
        {
            if (!resolved.TryGetValue(gamePath, out var actual)) continue;
            var normalized = Normalize(actual);
            // The game's own file comes back as the game path; anything else outside the mod folder is returned whole.
            if (!normalized.StartsWith(root, StringComparison.Ordinal))
                return normalized == Normalize(gamePath) ? "" : normalized;
            var rest = normalized[root.Length..];
            var slash = rest.IndexOf('/');
            return slash < 0 ? rest : rest[..slash];
        }
        return null;
    }

    /// The paths for this race when the option has any, else all of them, since the game falls back to another skeleton.
    private static List<KeyValuePair<string, IReadOnlyCollection<string>>> Relevant(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> redirects, string? raceCode)
    {
        var own = raceCode is null ? [] : redirects.Where(r => IsForRace(r.Key, raceCode)).ToList();
        return own.Count > 0 ? own : redirects.ToList();
    }

    private static bool IsForRace(string gamePath, string raceCode)
        => Normalize(gamePath).Contains($"/{raceCode}/", StringComparison.Ordinal);

    private static string Normalize(string path) => path.Replace('\\', '/').ToLowerInvariant();
}

/// Animation paths carry a race code, such as "c0201" for a female Midlander.
public static class RaceCodes
{
    /// The race code for the Customize bytes. Null for an unknown race.
    public static string? Of(byte race, byte tribe, byte gender)
    {
        int? first = race switch
        {
            1 => tribe == 2 ? 3 : 1,
            2 => 5,
            3 => 11,
            4 => 7,
            5 => 9,
            6 => 13,
            7 => 15,
            8 => 17,
            _ => null,
        };
        return first is { } code ? $"c{code + (gender == 0 ? 0 : 1):00}01" : null;
    }
}
