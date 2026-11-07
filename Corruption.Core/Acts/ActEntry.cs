using System.Text.Json.Serialization;

namespace Corruption.Core;

/// One animation being played: a pose of an option in a picked mod. Built from the mod's settings, not stored.
public sealed class ActEntry
{
    public string ModDirectory { get; set; } = "";
    public string ModName { get; set; } = "";
    public string Group { get; set; } = "";
    public string Option { get; set; } = "";
    public uint EmoteMode { get; set; }
    public byte Variant { get; set; }

    public PoseKey Pose => new(EmoteMode, Variant);

    public string Key => KeyOf(ModDirectory, Group, Option, Pose);

    public string DisplayName => Option is "" or "Default" ? ModName : Option;

    /// Identifies this animation within its mod, for exclusions.
    public string ExclusionKey => ActsFile.ExclusionKeyOf(Group, Option, Pose);

    public static string KeyOf(string modDirectory, string group, string option, PoseKey pose)
        => $"{modDirectory}|{group}|{option}|{pose.EmoteMode}|{pose.Variant}";
}

/// Where a climax can land, by Cummy's part wire ids (Cummy/BodyPart.cs). Full Body isn't here: a climax picks one part.
public static class CummyParts
{
    public static readonly IReadOnlyList<string> All =
        ["mo", "ch", "bk", "st", "bt", "gr", "al", "ar", "hl", "hr", "ll", "lr", "bl", "br", "fl", "fr"];
}

/// A mod the user picked: every looping animation in it counts.
public sealed class ModPick
{
    public string ModDirectory { get; set; } = "";
    public string ModName { get; set; } = "";

    public ActEntry ActFor(string group, string option, PoseKey pose) => new()
    {
        ModDirectory = ModDirectory,
        ModName = ModName,
        Group = group,
        Option = option,
        EmoteMode = pose.EmoteMode,
        Variant = pose.Variant,
    };
}

/// The user's folder ticks and picked mods: acts.json.
public sealed class ActsFile
{
    /// Penumbra sort folders, as "Animations/Couples". A ticked folder includes its subfolders. "" is mods in no folder,
    /// and doesn't include the folders below it.
    public List<string> Folders { get; set; } = [];
    public List<ModPick> Mods { get; set; } = [];

    /// Mods the user has already seen; anything else in a ticked folder is "new, not yet picked".
    public HashSet<string> KnownMods { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// Animations of a mod that don't count, by mod directory then <see cref="ExclusionKeyOf"/>. Kept apart from the picks,
    /// so unpicking a mod and picking it again keeps them.
    public Dictionary<string, HashSet<string>> Exclusions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static string ExclusionKeyOf(string group, string option, PoseKey pose)
        => $"{group}|{option}|{pose.EmoteMode}.{pose.Variant}";

    public bool IsExcluded(string modDirectory, string key)
        => Exclusions.TryGetValue(modDirectory, out var keys) && keys.Contains(key);

    /// Excludes or includes animations of one mod. True when anything changed.
    public bool SetExcluded(string modDirectory, IEnumerable<string> keys, bool excluded)
    {
        if (!Exclusions.TryGetValue(modDirectory, out var set))
        {
            if (!excluded) return false;
            Exclusions[modDirectory] = set = new HashSet<string>(StringComparer.Ordinal);
        }
        var changed = false;
        foreach (var key in keys) changed |= excluded ? set.Add(key) : set.Remove(key);
        if (set.Count == 0) Exclusions.Remove(modDirectory);
        return changed;
    }

    public int ExcludedCount(string modDirectory) => Exclusions.TryGetValue(modDirectory, out var keys) ? keys.Count : 0;

    /// From before picks were per mod. Read once by <see cref="Migrate"/> and never written again.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ActEntry>? Acts { get; set; }

    /// From before picks were per mod, as above.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HashSet<string>? KnownKeys { get; set; }

    public ModPick? Find(string modDirectory)
        => Mods.FirstOrDefault(m => string.Equals(m.ModDirectory, modDirectory, StringComparison.OrdinalIgnoreCase));

    /// Turns per-animation picks into per-mod ones: a mod with a picked animation stays picked. True when anything
    /// changed, so the caller can save.
    public bool Migrate()
    {
        if (Acts is null && KnownKeys is null) return false;

        foreach (var act in Acts ?? [])
        {
            if (Find(act.ModDirectory) is not null) continue;
            Mods.Add(new ModPick { ModDirectory = act.ModDirectory, ModName = act.ModName });
            KnownMods.Add(act.ModDirectory);
        }
        foreach (var key in KnownKeys ?? [])
        {
            var bar = key.IndexOf('|');
            if (bar > 0) KnownMods.Add(key[..bar]);
        }
        Acts = null;
        KnownKeys = null;
        return true;
    }

    /// Whether a mod at this Penumbra path sits in a ticked folder.
    public bool IsInTickedFolder(string folder)
        => Folders.Any(ticked => folder.Equals(ticked, StringComparison.OrdinalIgnoreCase)
                              || folder.StartsWith(ticked + "/", StringComparison.OrdinalIgnoreCase));

    /// The folder part of a mod's Penumbra path ("Animations/Couples/Mod" gives "Animations/Couples"); "" at the root.
    public static string FolderOf(string modPath)
    {
        var slash = modPath.Replace('\\', '/').LastIndexOf('/');
        return slash > 0 ? modPath.Replace('\\', '/')[..slash] : "";
    }
}
