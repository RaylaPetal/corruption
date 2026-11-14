using System;
using System.Collections.Generic;
using System.Linq;
using Corruption.Core;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Ipc;
using Lumina.Excel.Sheets;
using MoodleTuple = (int Version, System.Guid Id, int IconId, string Title, string Description, string CustomVfxPath,
    long ExpireTicks, int Type, int Stacks, int StackSteps, uint Modifiers, System.Guid ChainedStatus, int ChainTrigger,
    string Applier, string Dispeller, bool Permanent);

namespace Corruption.Bridges;

/// Corruption's statuses on the local player through Moodles, applied from data so nothing is added to the player's
/// Moodles library. Only statuses with Corruption's fixed IDs are ever touched. Framework thread.
public sealed class MoodlesService
{
    /// Moodles' status tuple version this matches, as in Oathbound.
    private const int TupleVersion = 2;

    private const int Positive = 0;
    private const int Negative = 1;
    private const int Special = 2;

    private static readonly Dictionary<MoodleKind, Guid> Ids = new()
    {
        [MoodleKind.Aroused] = new Guid("c0aa7105-0000-4c0e-9a11-000000000001"),
        [MoodleKind.Marked] = new Guid("c0aa7105-0000-4c0e-9a11-000000000002"),
        [MoodleKind.Watched] = new Guid("c0aa7105-0000-4c0e-9a11-000000000003"),
        [MoodleKind.Bonded] = new Guid("c0aa7105-0000-4c0e-9a11-000000000004"),
        [MoodleKind.Refractory] = new Guid("c0aa7105-0000-4c0e-9a11-000000000005"),
        [MoodleKind.Fading] = new Guid("c0aa7105-0000-4c0e-9a11-000000000006"),
        [MoodleKind.Fertile] = new Guid("c0aa7105-0000-4c0e-9a11-000000000007"),
        [MoodleKind.Pregnant] = new Guid("c0aa7105-0000-4c0e-9a11-000000000008"),
        [MoodleKind.Craving] = new Guid("c0aa7105-0000-4c0e-9a11-000000000009"),
        [MoodleKind.Unsatisfied] = new Guid("c0aa7105-0000-4c0e-9a11-00000000000a"),
        [MoodleKind.Recovering] = new Guid("c0aa7105-0000-4c0e-9a11-00000000000b"),
        [MoodleKind.Dripping] = new Guid("c0aa7105-0000-4c0e-9a11-00000000000c"),
    };

    /// Game statuses whose icons fit, in order of preference, looked up by English name.
    private static readonly Dictionary<MoodleKind, string[]> IconNames = new()
    {
        [MoodleKind.Aroused] = ["Seduced", "Infatuated", "Fervor", "Haste"],
        [MoodleKind.Marked] = ["Prey", "Vulnerability Up", "Weakness"],
        [MoodleKind.Watched] = ["Spotlight", "Watchful Eye", "Haste"],
        [MoodleKind.Bonded] = ["Dance Partner", "Closed Position", "Kardion"],
        [MoodleKind.Refractory] = ["Weakness", "Heavy", "Slow"],
        [MoodleKind.Fading] = ["Lethargy", "Slow", "Weakness"],
        [MoodleKind.Fertile] = ["Fertile", "Bloom", "Regen"],
        [MoodleKind.Pregnant] = ["Well Fed", "Protect", "Regen"],
        [MoodleKind.Craving] = ["Hungry", "Food Craving", "Haste"],
        [MoodleKind.Unsatisfied] = ["Weakness", "Heavy", "Slow"],
        [MoodleKind.Recovering] = ["Regen", "Rest", "Well Fed"],
        [MoodleKind.Dripping] = ["Dropsy", "Soaked", "Wet", "Doused", "Water Resistance Down", "Sludge"],
    };

    private readonly ICallGateSubscriber<MoodleTuple, IPlayerCharacter, object> addByData;
    private readonly ICallGateSubscriber<Guid, IPlayerCharacter, object> remove;
    private readonly ICallGateSubscriber<List<MoodleTuple>> clientStatuses;
    /// What's on now: the level, and the extra text that can change without it (the Pregnant status's father).
    private readonly Dictionary<MoodleKind, (int Level, string? Extra)> applied = [];
    private Dictionary<MoodleKind, int>? icons;
    private bool checkedOnce;

    public MoodlesService()
    {
        var pi = Plugin.PluginInterface;
        addByData = pi.GetIpcSubscriber<MoodleTuple, IPlayerCharacter, object>("Moodles.AddOrUpdateMoodleByDataByPlayerV2");
        remove = pi.GetIpcSubscriber<Guid, IPlayerCharacter, object>("Moodles.RemoveMoodleByPlayerV2");
        clientStatuses = pi.GetIpcSubscriber<List<MoodleTuple>>("Moodles.GetClientStatusManagerInfoV2");
    }

    public bool IsAvailable
    {
        get
        {
            try { clientStatuses.InvokeFunc(); return true; }
            catch { return false; }
        }
    }

    /// Why statuses aren't showing, for Settings. Null when they are, or nothing has been tried.
    public string? Problem { get; private set; }

    public int AppliedCount => applied.Count;

    /// Sends only what changed between the plan and what's on now. Only the level counts, so the Aroused percentage
    /// doesn't re-send every tick, except that a new father re-sends the Pregnant status.
    public void Update(IReadOnlyDictionary<MoodleKind, int> plan, string applier, double arousal, int layers, string? father)
    {
        if (plan.Count == 0 && applied.Count == 0) return;
        if (Plugin.ObjectTable.LocalPlayer is not { } me) return;

        try
        {
            foreach (var kind in applied.Keys.Where(k => !plan.ContainsKey(k)).ToList())
            {
                remove.InvokeAction(Ids[kind], me);
                applied.Remove(kind);
            }
            var added = false;
            foreach (var (kind, level) in plan)
            {
                var extra = kind == MoodleKind.Pregnant ? father : null;
                if (applied.TryGetValue(kind, out var was) && was == (level, extra)) continue;
                addByData.InvokeAction(Tuple(kind, level, applier, arousal, layers, father), me);
                applied[kind] = (level, extra);
                added = true;
            }
            if (added && !checkedOnce) CheckAccepted();
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"Moodles call failed: {ex.Message}");
            Problem = "Moodles isn't available, or is a version Corruption doesn't know.";
            applied.Clear();
        }
    }

    /// Puts one of Corruption's statuses on with a chosen icon, for the Debug tab's icon browser. The next plan update
    /// replaces it with the real one, or takes it off if it doesn't apply.
    public void Preview(MoodleKind kind, int level, int icon, string applier)
    {
        if (Plugin.ObjectTable.LocalPlayer is not { } me) return;
        try
        {
            addByData.InvokeAction(Tuple(kind, level, applier, 0, level, null, icon), me);
            applied[kind] = (-1, null);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"Moodles preview failed: {ex.Message}");
        }
    }

    /// Moodles drops statuses quietly when other plugins aren't allowed, so look once after the first apply.
    private void CheckAccepted()
    {
        checkedOnce = true;
        var active = clientStatuses.InvokeFunc().Select(s => s.Id).ToHashSet();
        if (applied.Keys.Any(k => active.Contains(Ids[k])))
        {
            Problem = null;
            return;
        }
        Problem = "Moodles didn't accept Corruption's statuses. In Moodles' settings, turn on \"Allow other plugins to apply Moodles\".";
        applied.Clear();
    }

    /// Statuses with Corruption's IDs left from an earlier session, such as after a crash or a logout that came too late
    /// to remove them.
    public void RemoveLeftovers()
    {
        if (Plugin.ObjectTable.LocalPlayer is not { } me) return;
        try
        {
            var ours = Ids.Values.ToHashSet();
            foreach (var id in clientStatuses.InvokeFunc().Select(s => s.Id).Where(ours.Contains))
                remove.InvokeAction(id, me);
        }
        catch (Exception ex) { Plugin.Log.Debug($"Moodles leftover check failed: {ex.Message}"); }
    }

    /// Takes off every status Corruption put on, and nothing else.
    public void RemoveAll()
    {
        checkedOnce = false;
        if (applied.Count == 0 || Plugin.ObjectTable.LocalPlayer is not { } me)
        {
            applied.Clear();
            return;
        }
        foreach (var kind in applied.Keys)
        {
            try { remove.InvokeAction(Ids[kind], me); }
            catch (Exception ex) { Plugin.Log.Debug($"Moodles remove failed: {ex.Message}"); }
        }
        applied.Clear();
    }

    private MoodleTuple Tuple(MoodleKind kind, int level, string applier, double arousal, int layers, string? father,
        int? icon = null)
    {
        var (title, description, type) = kind switch
        {
            MoodleKind.Aroused => ($"Aroused {Roman(level)}", $"Arousal at {PlateComposer.ArousalNumber(arousal)}%.", Special),
            MoodleKind.Marked => AsSpecial(CumStatusText.Layers(layers)),
            MoodleKind.Watched => ("Watched", "Someone's watching the scene.", Positive),
            MoodleKind.Bonded => ("Bonded", "In a scene with their bonded partner.", Positive),
            MoodleKind.Refractory => ("Refractory", "Recovering after a climax.", Negative),
            MoodleKind.Fading => ("Fading", "Their corruption is fading.", Negative),
            MoodleKind.Fertile => ("Fertile", "Their body is ready to conceive.", Special),
            MoodleKind.Pregnant => ($"Pregnant: {(level == 1 ? "first" : level == 2 ? "second" : "third")} trimester", MoodlesPlan.PregnantDescription(father), Positive),
            MoodleKind.Craving => ("Craving", "Craving something only a scene can give.", Special),
            MoodleKind.Unsatisfied => ("Unsatisfied", "Yesterday's craving went unmet.", Negative),
            MoodleKind.Dripping => AsSpecial(CumStatusText.Creampie(level)),
            _ => ("Recovering", "Recovering after giving birth.", Positive),
        };
        return (TupleVersion, Ids[kind], icon ?? IconFor(kind), title, description, "", -1L, type, 1, 0, 0u, Guid.Empty, 0,
            applier, "", false);
    }

    private static (string, string, int) AsSpecial((string Title, string Description) text) => (text.Title, text.Description, Special);

    private static string Roman(int step) => step switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => "V" };

    private int IconFor(MoodleKind kind)
    {
        icons ??= ResolveIcons();
        return icons.TryGetValue(kind, out var icon) ? icon : 0;
    }

    /// The game status icons picked by eye for the two cum statuses, with the Debug tab's icon browser. 0 means not
    /// picked yet, so the name candidates apply. Every client sends the same id, so everyone sees the same icon.
    private static readonly Dictionary<MoodleKind, int> PinnedIcons = new()
    {
        // "Honey-glazed": thick golden goo dripping down.
        [MoodleKind.Marked] = 215583,
        // "Drenched": a water droplet with a splash.
        [MoodleKind.Dripping] = 215814,
    };

    /// A pinned icon when the game still has it, else, as Oathbound's ResolveIconIds, the first named status that
    /// exists, falling back to any status with an icon. Many statuses share a name with different icons, which is why
    /// the cum statuses are pinned.
    private static Dictionary<MoodleKind, int> ResolveIcons()
    {
        var result = new Dictionary<MoodleKind, int>();
        try
        {
            var sheet = Plugin.DataManager.GetExcelSheet<Status>(ClientLanguage.English);
            var byName = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            var all = new HashSet<uint>();
            foreach (var row in sheet)
            {
                if (row.Icon == 0) continue;
                all.Add(row.Icon);
                byName.TryAdd(row.Name.ExtractText(), row.Icon);
            }
            var fallback = byName.Values.FirstOrDefault();
            foreach (var (kind, names) in IconNames)
            {
                if (PinnedIcons.GetValueOrDefault(kind) is > 0 and var pinned)
                {
                    if (all.Contains((uint)pinned))
                    {
                        result[kind] = pinned;
                        continue;
                    }
                    Plugin.Log.Warning($"Moodles: the pinned {kind} icon {pinned} isn't a status icon any more; using its name list. Pick a new one in the Debug tab's icon browser.");
                }
                var icon = names.Select(n => byName.GetValueOrDefault(n)).FirstOrDefault(i => i != 0);
                result[kind] = (int)(icon != 0 ? icon : fallback);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"Couldn't read status icons: {ex.Message}");
        }
        return result;
    }
}
