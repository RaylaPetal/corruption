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
        lastRace = raceCode;
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
        nextCheck = DateTime.MinValue;
    }

    private ActEntry? Check(PoseKey pose, string? raceCode)
    {
        Excluded = null;
        NotServed = null;
        if (!byPose.TryGetValue(pose, out var entry))
        {
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
        NotServed = WhyNotServed(root, entry.Options, resolved, raceCode);
        return null;
    }

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

    /// What to send when asking for help: the reason and what Penumbra serves for the current pose.
    public string Details()
    {
        if (lastPose is not { } pose) return NotServed ?? "";
        var expected = acts.Candidates.Where(c => c.Pose is not null && acts.File.Find(c.ModDirectory) is not null)
            .Select(c => $"{c.ModName}: {c.Trigger} = pose {c.Pose!.Value.EmoteMode}/{c.Pose.Value.Variant}").Distinct();
        return string.Join("\n", new[]
        {
            NotServed ?? "",
            $"Pose: {pose.EmoteMode}/{pose.Variant} (/{emotes.CommandOfMode(pose.EmoteMode) ?? "?"}), race {lastRace ?? "unknown"}",
            $"Penumbra mod folder: {penumbra.TryGetModDirectory()}",
            "Picked poses:",
        }.Concat(expected).Append("For this pose:").Concat(Explain(pose, lastRace)));
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
