using System;
using System.Collections.Generic;
using Lumina.Excel.Sheets;

namespace Corruption.Mods;

/// Maps animation paths and emote commands to the EmoteMode row that loops them, adapted from PoseKit's
/// EmoteAnimationIndex. An emote with no EmoteMode doesn't loop, so it has no mode here. Built once; read only after.
public sealed class EmoteIndex
{
    private readonly Dictionary<string, List<(string Key, string Command)>> byTimeline = new();
    private readonly Dictionary<string, uint> modeByCommand = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, string> commandByMode = new();

    public EmoteIndex()
    {
        foreach (var emote in Plugin.DataManager.GetExcelSheet<Emote>())
        {
            var commandRow = emote.TextCommand;
            if (!commandRow.IsValid) continue;
            var command = commandRow.Value.Command.ExtractText().TrimStart('/');
            if (command.Length == 0) continue;

            foreach (var timeline in emote.ActionTimeline)
            {
                if (!timeline.IsValid) continue;
                var key = timeline.Value.Key.ExtractText();
                if (key.Length == 0) continue;
                var basename = key[(key.LastIndexOf('/') + 1)..];
                if (!byTimeline.TryGetValue(basename, out var list)) byTimeline[basename] = list = [];
                list.Add((key, command));
            }
        }

        foreach (var mode in Plugin.DataManager.GetExcelSheet<EmoteMode>())
        {
            var start = mode.StartEmote;
            if (!start.IsValid || start.RowId == 0 || !start.Value.TextCommand.IsValid) continue;
            var command = start.Value.TextCommand.Value.Command.ExtractText().TrimStart('/');
            if (command.Length == 0) continue;
            modeByCommand.TryAdd(command, mode.RowId);
            commandByMode.TryAdd(mode.RowId, command);
        }
    }

    /// The emote command an animation path belongs to.
    public string? CommandOfPath(string gamePathWithoutExtension)
    {
        var basename = gamePathWithoutExtension[(gamePathWithoutExtension.LastIndexOf('/') + 1)..];
        if (!byTimeline.TryGetValue(basename, out var candidates)) return null;
        foreach (var (key, command) in candidates)
            if (gamePathWithoutExtension.EndsWith(key, StringComparison.OrdinalIgnoreCase)) return command;
        return null;
    }

    public uint? ModeOfCommand(string command) => modeByCommand.TryGetValue(command, out var mode) ? mode : null;

    public string? CommandOfMode(uint mode) => commandByMode.GetValueOrDefault(mode);
}
