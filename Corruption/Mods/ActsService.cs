using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Corruption.Core;

namespace Corruption.Mods;

/// A mod with animations, as the Acts page lists it.
public sealed record ModRow(string Directory, string Name, string Folder, int Countable, int NotLooping);

/// The user's folder ticks and picked mods, and what the ticked folders currently offer. Framework thread, apart from
/// the file scan, whose result is picked up on the next update.
public sealed class ActsService
{
    private readonly PenumbraIpc penumbra;
    private readonly ModScanner scanner;
    private readonly string path;
    private readonly HashSet<string> folderBaseline = new(StringComparer.OrdinalIgnoreCase);

    private Task<List<ActCandidate>>? scanning;
    private bool rescanWanted;

    public ActsFile File { get; }

    /// A load problem to tell the user once, or null.
    public string? LoadError { get; }

    public IReadOnlyList<ActCandidate> Candidates { get; private set; } = [];

    public IReadOnlyList<ModRow> Mods { get; private set; } = [];

    public IReadOnlyList<string> AllFolders { get; private set; } = [];

    /// Bumped when the candidates or the picks change, for detection and the UI to rebuild from.
    public int Version { get; private set; }

    public bool IsScanning => scanning is not null;

    public ActsService(PenumbraIpc penumbra, ModScanner scanner, string configDirectory)
    {
        this.penumbra = penumbra;
        this.scanner = scanner;
        path = Path.Combine(configDirectory, "acts.json");
        File = JsonStore.Load<ActsFile>(path, out var error);
        LoadError = error;
        if (File.Migrate()) Save();
    }

    public void Save()
    {
        try { JsonStore.Save(path, File); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Plugin.Log.Error(ex, "Couldn't save acts.json."); }
        Version++;
    }

    public void Update()
    {
        if (penumbra.ModsDirty && penumbra.IsAvailable)
        {
            penumbra.ModsDirty = false;
            rescanWanted = true;
        }

        if (scanning is { IsCompleted: true } done)
        {
            scanning = null;
            if (done.IsCompletedSuccessfully) ApplyScan(done.Result);
            else Plugin.Log.Error(done.Exception, "Scanning mods failed.");
        }

        if (rescanWanted && scanning is null && penumbra.IsAvailable) StartScan();
    }

    public void RequestRescan() => rescanWanted = true;

    private void StartScan()
    {
        rescanWanted = false;
        var root = penumbra.TryGetModDirectory();
        var list = penumbra.TryGetModList();
        if (string.IsNullOrEmpty(root) || list is null) return;

        var folders = new Dictionary<string, (string Name, string Folder)>();
        foreach (var (directory, name) in list)
            folders[directory] = (name, ActsFile.FolderOf(penumbra.TryGetModPath(directory, name) ?? name));

        var all = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, folder) in folders.Values)
        {
            if (folder.Length == 0) all.Add("");
            for (var f = folder; f.Length > 0; f = ActsFile.FolderOf(f)) all.Add(f);
        }
        AllFolders = all.ToList();

        var wanted = folders.Where(m => File.IsInTickedFolder(m.Value.Folder))
            .Select(m => (m.Key, m.Value.Name, m.Value.Folder)).ToList();
        scanning = Task.Run(() => scanner.Scan(root, wanted));
    }

    private void ApplyScan(List<ActCandidate> found)
    {
        Candidates = found;
        Mods = found.GroupBy(c => c.ModDirectory, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ModRow(g.Key, g.First().ModName, g.First().Folder, g.Count(c => c.Pose is not null), g.Count(c => c.Pose is null)))
            .ToList();

        // Everything in a folder the user just ticked is what they're looking at, not "new".
        if (folderBaseline.Count > 0)
        {
            foreach (var mod in Mods)
                if (folderBaseline.Any(f => InFolder(mod.Folder, f))) File.KnownMods.Add(mod.Directory);
            folderBaseline.Clear();
            Save();
        }
        Version++;
    }

    private static bool InFolder(string folder, string ticked)
        => folder.Equals(ticked, StringComparison.OrdinalIgnoreCase)
        || (ticked.Length > 0 && folder.StartsWith(ticked + "/", StringComparison.OrdinalIgnoreCase));

    public void SetFolderTicked(string folder, bool ticked)
    {
        if (ticked)
        {
            if (!File.Folders.Contains(folder, StringComparer.OrdinalIgnoreCase)) File.Folders.Add(folder);
            folderBaseline.Add(folder);
        }
        else
        {
            File.Folders.RemoveAll(f => f.Equals(folder, StringComparison.OrdinalIgnoreCase));
        }
        Save();
        rescanWanted = true;
    }

    public bool IsNew(ModRow mod) => !File.KnownMods.Contains(mod.Directory) && File.Find(mod.Directory) is null;

    public void MarkAllSeen()
    {
        foreach (var mod in Mods) File.KnownMods.Add(mod.Directory);
        Save();
    }

    public void Pick(ModRow mod)
    {
        File.Mods.Add(new ModPick { ModDirectory = mod.Directory, ModName = mod.Name });
        File.KnownMods.Add(mod.Directory);
        Save();
    }

    /// Excludes or includes animations of a mod, saving and redetecting when anything changed.
    public void SetExcluded(string modDirectory, IEnumerable<string> keys, bool excluded)
    {
        if (File.SetExcluded(modDirectory, keys, excluded)) Save();
    }

    public void Unpick(ModPick pick)
    {
        File.Mods.Remove(pick);
        Save();
    }
}
