namespace Corruption.Core;

/// Which looping pose a character is in: the EmoteMode row and, for the three pose-cycling emotes, the variant.
public readonly record struct PoseKey(uint EmoteMode, byte Variant)
{
    /// GroundSit, Sit and Doze cycle through variants. Every other mode has one, whatever CPoseState happens to hold.
    public static PoseKey Normalized(uint emoteMode, byte cposeState)
        => new(emoteMode, emoteMode is 1 or 2 or 3 ? cposeState : (byte)0);
}
