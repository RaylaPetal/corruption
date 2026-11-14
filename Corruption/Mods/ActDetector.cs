using System;
using System.Collections.Generic;
using System.Linq;
using Corruption.Core;

namespace Corruption.Mods;

/// Which animation of a picked mod the local character is playing, confirmed by what Penumbra actually serves. Runs once
/// a second; the IPC check only runs when something may have changed, and every 5 seconds to catch whatever didn't
/// announce itself. Big packs reuse a few emote slots for hundreds of options, so each pose asks Penumbra once, for all
/// its options' paths together.
public sealed class ActDetector(PenumbraIpc penumbra, ActsService acts, EmoteIndex emotes)
{
    private static readonly TimeSpan RecheckInterval = TimeSpan.FromSeconds(5);

    private sealed record PoseOptions(List<(ActEntry Act, ActCandidate Candidate)> Options, string[] Paths);

    private Dictionary<PoseKey, PoseOptions> byPose = new();
    private int builtFrom = -1;
    private readonly CopiedAnimations copies = new();

    /// The copied file the current animation was recognised from, when it plays through another emote. Null otherwise.
    public string? CopiedFrom { get; private set; }

    private PoseKey? lastPose;
    private int lastSettings;
    private DateTime nextCheck = DateTime.MinValue;
    private ActEntry? current;

    /// The animation playing when it's from a picked mod but excluded. Null otherwise.
    public ActEntry? Excluded { get; private set; }

    /// Why a pose a picked mod replaces isn't counting: Penumbra isn't serving that mod's file. Null otherwise.
    public string? NotServed { get; private set; }

    private string? lastRace;
    private string? lastLogged;

    public ActEntry? Update(DateTime now, PoseKey? pose, string? raceCode)
    {
        lastRace = raceCode;
        if (!penumbra.IsAvailable || pose is null)
        {
            lastPose = pose;
            current = null;
            Excluded = null;
            NotServed = null;
            return null;
        }

        if (builtFrom != acts.Version) Rebuild();

        var due = pose != lastPose
               || penumbra.SettingsVersion != lastSettings
               || now >= nextCheck
               || (current is null && byPose.ContainsKey(pose.Value));
        lastPose = pose;
        if (!due) return current;

        lastSettings = penumbra.SettingsVersion;
        nextCheck = now + RecheckInterval;
        current = Check(pose.Value, raceCode);
        return current;
    }

    private void Rebuild()
    {
        builtFrom = acts.Version;
        var options = new Dictionary<PoseKey, List<(ActEntry, ActCandidate)>>();
        foreach (var candidate in acts.Candidates)
        {
            if (candidate.Pose is not { } pose || acts.File.Find(candidate.ModDirectory) is not { } pick) continue;
            if (!options.TryGetValue(pose, out var list)) options[pose] = list = [];
            list.Add((pick.ActFor(candidate.Group, candidate.Option, pose), candidate));
        }

        byPose = options.ToDictionary(o => o.Key, o => new PoseOptions(o.Value,
            o.Value.SelectMany(v => v.Item2.Redirects.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
        if (penumbra.TryGetModDirectory() is { Length: > 0 } root)
            copies.Rebuild(root, options.Values.SelectMany(list => list));
        nextCheck = DateTime.MinValue;
    }

    private ActEntry? Check(PoseKey pose, string? raceCode)
    {
        Excluded = null;
        NotServed = null;
        CopiedFrom = null;
        if (!byPose.TryGetValue(pose, out var entry))
        {
            if (Copied(pose, raceCode) is { } copied) return Counted(copied);
            // Only worth saying for an emote a picked mod replaces under another pose, which means the pose read is off.
            var command = emotes.CommandOfMode(pose.EmoteMode);
            if (command is not null && acts.Candidates.Any(c => c.Command == command && c.Pose != pose
                    && acts.File.Find(c.ModDirectory) is not null))
                NotServed = $"Your /{command} reads as pose {pose.EmoteMode}/{pose.Variant}, which isn't the one Corruption expects for it.";
            return null;
        }
        var root = penumbra.TryGetModDirectory();
        if (string.IsNullOrEmpty(root) || Resolve(entry.Paths) is not { } resolved)
        {
            NotServed = "Penumbra didn't say which animation files it plays for you, so this can't be checked.";
            return null;
        }

        foreach (var (act, candidate) in entry.Options)
        {
            if (!ServedCheck.IsServed(root, candidate.ModDirectory, candidate.Redirects, resolved, raceCode)) continue;
            // What's playing is this animation, so an excluded one doesn't fall through to another candidate.
            if (acts.File.IsExcluded(act.ModDirectory, act.ExclusionKey))
            {
                Excluded = act;
                return null;
            }
            return act;
        }
        if (Copied(pose, raceCode) is { } copy) return Counted(copy);
        NotServed = WhyNotServed(root, entry.Options, resolved, raceCode);
        return null;
    }

    private ActEntry? Counted(ActEntry act)
    {
        if (!acts.File.IsExcluded(act.ModDirectory, act.ExclusionKey)) return act;
        Excluded = act;
        return null;
    }

    /// A picked animation playing through another emote: what Penumbra serves for this emote is a byte-for-byte copy of
    /// a picked mod's file, as plugins like BypassEmote make. The act keeps its own pose, so partners see the real one.
    private ActEntry? Copied(PoseKey pose, string? raceCode)
    {
        if (raceCode is null || emotes.CommandOfMode(pose.EmoteMode) is not { } command) return null;
        // The race's own paths first, then the base skeletons the game falls back to for races without their own file,
        // which is all BypassEmote redirects for some emotes.
        var paths = new[] { raceCode, "c0101", "c0201" }.Distinct()
            .SelectMany(race => emotes.PathsOfCommand(command, race))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (paths.Length == 0 || Resolve(paths) is not { } resolved) return null;
        foreach (var (gamePath, actual) in resolved)
        {
            if (string.Equals(actual.Replace('\\', '/'), gamePath, StringComparison.OrdinalIgnoreCase)) continue;
            if (copies.Match(actual) is not { } act) continue;
            CopiedFrom = actual;
            if (lastCopyLogged != actual)
            {
                lastCopyLogged = actual;
                Plugin.Log.Information($"/{command} plays a copy of {act.ModName} / {act.DisplayName} ({actual}), counting it as that.");
            }
            return act;
        }
        return null;
    }

    private string? lastCopyLogged;

    private string WhyNotServed(string root, List<(ActEntry Act, ActCandidate Candidate)> options, Dictionary<string, string> resolved,
        string? raceCode)
    {
        var (act, candidate) = options[0];
        var instead = ServedCheck.ServedInstead(root, candidate.Redirects, resolved, raceCode);
        var message = instead switch
        {
            null => $"Penumbra didn't resolve {act.ModName}'s files for your race.",
            "" => $"Penumbra says it plays the game's own animation here, not {act.ModName}'s.",
            _ when instead.Contains(':') || instead.StartsWith('/')
                => $"Penumbra plays {instead}, which isn't inside its mod folder, so Corruption can't match it to {act.ModName}.",
            _ when instead.Equals(candidate.ModDirectory, StringComparison.OrdinalIgnoreCase)
                => $"Penumbra plays a different option of {act.ModName} than the one Corruption knows for this pose. Re-scan your mods on the Acts page.",
            _ => $"Penumbra plays {NameOf(instead)} instead of {act.ModName} for this animation. Give {act.ModName} a higher priority, or turn the other mod off.",
        };
        if (message == lastLogged) return message;
        lastLogged = message;
        Plugin.Log.Information($"Act not detected: {message}\n{string.Join("\n", Explain(lastPose!.Value, raceCode))}");
        return message;
    }

    private string NameOf(string directory)
        => penumbra.TryGetModList() is { } mods && mods.FirstOrDefault(m => m.Key.Equals(directory, StringComparison.OrdinalIgnoreCase)).Value is { } name
            ? name
            : directory;

    /// What to send when asking for help: the reason, the emote state as read, the poses picked mods play, and what
    /// Penumbra serves for the current pose. Works whether or not a reason was found.
    /// <param name="rawState">The character's emote state as the game holds it.</param>
    public string Details(string rawState)
    {
        var expected = acts.Candidates.Where(c => acts.File.Find(c.ModDirectory) is not null)
            .Select(c => $"  {c.ModName} / {c.Group} / {c.Option}: {c.Trigger}"
                         + (c.Pose is { } p ? $" = pose {p.EmoteMode}/{p.Variant}" : ""))
            .Distinct();
        var lines = new List<string>
        {
            $"Reason: {NotServed ?? "none found"}",
            $"Emote state: {rawState}",
            lastPose is { } pose
                ? $"Pose: {pose.EmoteMode}/{pose.Variant} (/{emotes.CommandOfMode(pose.EmoteMode) ?? "?"})"
                : "Pose: not in a looping emote",
            $"Race: {lastRace ?? "unknown"}",
            $"Copied animation: {CopiedFrom ?? "none"}",
            $"Penumbra: {(penumbra.IsAvailable ? "available" : "unavailable")}, mod folder {penumbra.TryGetModDirectory()}",
            "Picked animations:",
        };
        lines.AddRange(expected);
        if (lastPose is { } current)
        {
            lines.Add("For this pose:");
            lines.AddRange(Explain(current, lastRace));
        }
        return string.Join("\n", lines);
    }

    private Dictionary<string, string>? Resolve(string[] paths)
    {
        if (penumbra.TryResolve(paths) is not { } resolved || resolved.Length != paths.Length) return null;
        var map = new Dictionary<string, string>(paths.Length, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < paths.Length; i++) map[paths[i]] = resolved[i];
        return map;
    }

    /// For the debug panel: each candidate of a pose, and what Penumbra serves for each of its paths.
    public IEnumerable<string> Explain(PoseKey pose, string? raceCode)
    {
        var root = penumbra.TryGetModDirectory();
        if (string.IsNullOrEmpty(root)) yield break;
        foreach (var candidate in acts.Candidates.Where(c => c.Pose == pose))
        {
            var paths = candidate.Redirects.Keys.ToArray();
            var resolved = Resolve(paths);
            var served = resolved is not null && ServedCheck.IsServed(root, candidate.ModDirectory, candidate.Redirects, resolved, raceCode);
            var picked = acts.File.Find(candidate.ModDirectory) is not null;
            yield return $"{candidate.ModName} / {candidate.Group} / {candidate.Option}: served={served}, mod picked={picked}";
            foreach (var path in paths)
                yield return $"    {path} -> {(resolved is not null && resolved.TryGetValue(path, out var actual) ? actual : "?")}";
        }
    }
}
