using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Corruption.Core;

namespace Corruption.Mods;

/// Recognises a picked animation by its file contents when it plays from somewhere else. Plugins like BypassEmote play
/// an emote the player hasn't unlocked by copying its animation over an emote they have, such as /lean, so Penumbra
/// serves the copy and the game reports the other emote. The copy keeps the picked mod's animation data (the Havok
/// section of the .pap) byte for byte and only rewrites the timeline after it, so that section is what's compared.
/// Framework thread, apart from the index build, which runs in the background.
public sealed class CopiedAnimations
{
    private sealed record Source(string Path, ActEntry Act);

    private Task<Dictionary<long, List<Source>>>? building;
    private Dictionary<long, List<Source>> bySize = new();
    private readonly Dictionary<string, (long Length, DateTime Written, byte[] Hash)> hashes = new(StringComparer.OrdinalIgnoreCase);

    /// Starts indexing the files of every picked animation by size. Matches wait until it's done.
    public void Rebuild(string modRoot, IEnumerable<(ActEntry Act, ActCandidate Candidate)> picked)
    {
        var sources = picked
            .SelectMany(p => p.Candidate.Redirects.Values.SelectMany(files => files)
                .Select(file => (Path: Path.Combine(modRoot, p.Candidate.ModDirectory, file), p.Act)))
            .ToList();
        building = Task.Run(() =>
        {
            var index = new Dictionary<long, List<Source>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (path, act) in sources)
            {
                if (!seen.Add(path)) continue;
                try
                {
                    if (AnimationSection(path) is not { } section) continue;
                    if (!index.TryGetValue(section.Length, out var list)) index[section.Length] = list = [];
                    list.Add(new Source(path, act));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            return index;
        });
    }

    /// The picked animation a served file is a copy of, or null.
    public ActEntry? Match(string servedFile)
    {
        if (building is { IsCompleted: true } done)
        {
            building = null;
            if (done.IsCompletedSuccessfully) bySize = done.Result;
            else Plugin.Log.Warning($"Indexing animation files failed: {done.Exception?.Message}");
        }

        try
        {
            if (AnimationSection(servedFile) is not { } section || !bySize.TryGetValue(section.Length, out var sources)) return null;
            var served = HashOf(servedFile);
            if (served is null) return null;
            foreach (var source in sources)
                if (HashOf(source.Path) is { } hash && hash.AsSpan().SequenceEqual(served)) return source.Act;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return null;
    }

    /// Where the Havok animation data sits in a .pap: between the offsets at bytes 18 and 22 of the header. Null when
    /// the file isn't a .pap or its offsets don't fit.
    private static (long Offset, int Length)? AnimationSection(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[26];
        if (stream.Read(header) != header.Length || !header[..4].SequenceEqual("pap "u8)) return null;
        var havok = BitConverter.ToUInt32(header[18..22]);
        var footer = BitConverter.ToUInt32(header[22..26]);
        if (havok < 26 || footer <= havok || footer > stream.Length) return null;
        return (havok, (int)(footer - havok));
    }

    private byte[]? HashOf(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) return null;
        if (hashes.TryGetValue(path, out var cached) && cached.Length == info.Length && cached.Written == info.LastWriteTimeUtc)
            return cached.Hash;
        if (AnimationSection(path) is not { } section) return null;
        using var stream = File.OpenRead(path);
        stream.Position = section.Offset;
        var bytes = new byte[section.Length];
        stream.ReadExactly(bytes);
        var hash = SHA256.HashData(bytes);
        hashes[path] = (info.Length, info.LastWriteTimeUtc, hash);
        return hash;
    }
}
