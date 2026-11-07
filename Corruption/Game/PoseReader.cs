using Corruption.Core;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace Corruption.Game;

/// Reads a character's looping pose from its emote state, adapted from PoseKit's PoseIdentifier.
public static unsafe class PoseReader
{
    /// Null unless the character is in a looping emote.
    public static PoseKey? Read(IPlayerCharacter? player)
    {
        if (player is null) return null;
        var character = (Character*)player.Address;
        if (character == null) return null;
        if (character->Mode is not (CharacterModes.InPositionLoop or CharacterModes.EmoteLoop)) return null;
        return PoseKey.Normalized(character->ModeParam, character->EmoteController.CPoseState);
    }

    /// The raw state, for the debug panel.
    public static string Describe(IPlayerCharacter? player)
    {
        if (player is null) return "not loaded";
        var character = (Character*)player.Address;
        return character == null
            ? "no character"
            : $"mode {character->Mode}, param {character->ModeParam}, cpose {character->EmoteController.CPoseState}";
    }

    public static string? RaceCode(IPlayerCharacter? player)
    {
        if (player is null) return null;
        var customize = player.Customize;
        return RaceCodes.Of(customize[(int)CustomizeIndex.Race], customize[(int)CustomizeIndex.Tribe], customize[(int)CustomizeIndex.Gender]);
    }
}
