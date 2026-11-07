namespace Corruption;

/// Local feedback in the chat log. This only prints on this client: nothing here reaches any chat channel.
public static class Notifier
{
    private const string Tag = "Corruption";

    public static void Info(string text) => Plugin.ChatGui.Print(text, Tag);

    public static void Error(string text) => Plugin.ChatGui.PrintError(text, Tag);
}
