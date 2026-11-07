using System;
using System.Collections.Generic;
using System.Linq;
using Corruption.Core;

namespace Corruption.Mods;

/// Which animation of a picked mod the local character is playing, confirmed by what Penumbra actually serves. Runs once
/// a second; the IPC check only runs when something may have changed, and every 5 seconds to catch whatever didn't
/// announce itself. Big packs reuse a few emote slots for hundreds of options, so each pose asks Penumbra once, for all
/// its options' paths together.
public sealed class ActDetector(PenumbraIpc penumbra, ActsService acts)
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

    public ActEntry? Update(DateTime now, PoseKey? pose, string? raceCode)
    {
        if (!penumbra.IsAvailable || pose is null)
        {
            lastPose = pose;
            current = null;
            Excluded = null;
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
        nextCheck = DateTime.MinValue;
    }

    private ActEntry? Check(PoseKey pose, string? raceCode)
    {
        Excluded = null;
        if (!byPose.TryGetValue(pose, out var entry)) return null;
        var root = penumbra.TryGetModDirectory();
        if (string.IsNullOrEmpty(root) || Resolve(entry.Paths) is not { } resolved) return null;

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
        return null;
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
