using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corruption.Core;

namespace Corruption.Mods;

/// One animation a mod option offers: a pose (or, for an emote that doesn't loop, only its command) and the game paths
/// and files that replace it.
public sealed record ActCandidate(
    string ModDirectory,
    string ModName,
    string Folder,
    string Group,
    string Option,
    PoseKey? Pose,
    string? Command,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Redirects)
{
    private string? key;

    public string? Key => key ??= Pose is { } pose ? ActEntry.KeyOf(ModDirectory, Group, Option, pose) : null;

    public string Trigger => Pose switch
    {
        null => Command is null ? "unknown" : $"/{Command} (doesn't loop)",
        { EmoteMode: 1 } pose => $"Ground sit {pose.Variant}",
        { EmoteMode: 2 } pose => $"Sit {pose.Variant}",
        { EmoteMode: 3 } pose => $"Doze {pose.Variant}",
        _ => Command is null ? $"Emote {Pose.Value.EmoteMode}" : $"/{Command}",
    };
}

/// Finds acts in mods by reading each mod's meta.json (schema 4: DefaultData plus Groups), which keeps every option's
/// identity. Adapted from PoseKit's PenumbraPoseScanner. Does file work: run it off the framework thread.
public sealed class ModScanner(EmoteIndex emotes)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private sealed record MetaDto(FilesDto? DefaultData, List<GroupDto>? Groups);
    private sealed record FilesDto(Dictionary<string, string>? Files);
    private sealed record GroupDto(string Type, string Name, List<OptionDto>? Options, List<FilesDto>? Containers);
    private sealed record OptionDto(string Name, Dictionary<string, string>? Files);

    private static readonly Regex SlashHint = new(@"\(/([a-zA-Z]+)\)", RegexOptions.Compiled);

    private static readonly (uint Mode, Regex Pattern)[] VariantFiles =
    [
        (1, new Regex(@"j_pose(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)), // GroundSit
        (2, new Regex(@"s_pose(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)), // Sit
        (3, new Regex(@"l_pose(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)), // Doze
    ];

    /// Variant 0 of each pose-cycling emote isn't a numbered file.
    private static readonly (string Suffix, PoseKey Pose)[] BaseVariantFiles =
    [
        ("/emote/jmn.pap", new PoseKey(1, 0)),
        ("/emote/sit.pap", new PoseKey(2, 0)),
        ("/emote/bed_liedown_loop.pap", new PoseKey(3, 0)),
    ];

    /// <param name="mods">Directory, display name and Penumbra folder of each mod to read.</param>
    public List<ActCandidate> Scan(string modRoot, IEnumerable<(string Directory, string Name, string Folder)> mods)
    {
        var results = new List<ActCandidate>();
        foreach (var (directory, name, folder) in mods)
        {
            var metaPath = Path.Combine(modRoot, directory, "meta.json");
            if (!File.Exists(metaPath)) continue;

            MetaDto? meta;
            try { meta = JsonSerializer.Deserialize<MetaDto>(File.ReadAllText(metaPath), JsonOptions); }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                Plugin.Log.Information($"Skipping unreadable {metaPath}: {ex.Message}");
                continue;
            }
            if (meta is null) continue;

            AddOption(results, directory, name, folder, "", "", meta.DefaultData?.Files);
            foreach (var group in meta.Groups ?? [])
            {
                if (group.Options is null) continue;
                for (var i = 0; i < group.Options.Count; i++)
                    AddOption(results, directory, name, folder, group.Name, group.Options[i].Name, FilesOf(group, i));
            }
        }
        return results;
    }

    /// Combining groups keep files in containers instead of on options: bit i of a container's index means option i is on.
    private static IEnumerable<KeyValuePair<string, string>>? FilesOf(GroupDto group, int option)
    {
        if (!string.Equals(group.Type, "Combining", StringComparison.OrdinalIgnoreCase) || group.Containers is null)
            return group.Options![option].Files;
        return group.Containers.Where((_, index) => (index & (1 << option)) != 0).SelectMany(c => c.Files ?? []);
    }

    private void AddOption(List<ActCandidate> results, string directory, string modName, string folder, string group, string option,
        IEnumerable<KeyValuePair<string, string>>? files)
    {
        if (files is null) return;

        var redirects = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (gamePath, file) in files)
        {
            if (!gamePath.EndsWith(".pap", StringComparison.OrdinalIgnoreCase)) continue;
            if (!redirects.TryGetValue(gamePath, out var set)) redirects[gamePath] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add(file);
        }
        if (redirects.Count == 0) return;

        var triggers = new Dictionary<(PoseKey? Pose, string? Command), Dictionary<string, IReadOnlyCollection<string>>>();
        void Add((PoseKey? Pose, string? Command) trigger, string path)
        {
            if (!triggers.TryGetValue(trigger, out var paths))
                triggers[trigger] = paths = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase);
            paths[path] = redirects[path];
        }

        foreach (var path in redirects.Keys)
            if (Classify(path) is { } trigger) Add(trigger, path);

        // An author's "(/command)" hint names what the whole option plays.
        var hint = SlashHint.Match(option);
        if (!hint.Success) hint = SlashHint.Match(group);
        if (hint.Success)
        {
            var command = hint.Groups[1].Value;
            var trigger = (ToPose(command), (string?)command);
            foreach (var path in redirects.Keys) Add(trigger, path);
        }

        foreach (var ((pose, command), paths) in triggers)
        {
            results.Add(new ActCandidate(directory, modName, folder, group, option, pose, command, paths));
        }
    }

    private PoseKey? ToPose(string command) => emotes.ModeOfCommand(command) is { } mode ? PoseKey.Normalized(mode, 0) : null;

    private (PoseKey? Pose, string? Command)? Classify(string gamePath)
    {
        var normalized = gamePath.Replace('\\', '/');
        foreach (var (mode, pattern) in VariantFiles)
        {
            var match = pattern.Match(normalized);
            // The file number is the variant itself, not 1-based.
            if (match.Success && byte.TryParse(match.Groups[1].Value, out var variant) && variant <= 6)
                return (new PoseKey(mode, variant), null);
        }
        foreach (var (suffix, pose) in BaseVariantFiles)
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return (pose, null);

        return emotes.CommandOfPath(normalized[..^4]) is { } command ? (ToPose(command), command) : null;
    }
}
