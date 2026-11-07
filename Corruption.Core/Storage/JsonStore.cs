using System.Text.Json;

namespace Corruption.Core;

/// Atomic JSON files: an interrupted save leaves the old file, and an unreadable one is kept rather than overwritten.
public static class JsonStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// A new <typeparamref name="T"/> when the file doesn't exist. When it can't be read, the file is renamed to a backup
    /// and <paramref name="error"/> says where it went.
    public static T Load<T>(string path, out string? error) where T : new()
    {
        error = null;
        if (!File.Exists(path)) return new T();
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? new T();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            var backup = $"{path}.bak-{DateTime.UtcNow:yyyyMMddHHmmss}";
            try
            {
                File.Move(path, backup);
                error = $"{Path.GetFileName(path)} couldn't be read, so it was kept as {Path.GetFileName(backup)}.";
            }
            catch (Exception moveEx) when (moveEx is IOException or UnauthorizedAccessException)
            {
                error = $"{Path.GetFileName(path)} couldn't be read or backed up: {moveEx.Message}";
            }
            return new T();
        }
    }

    public static void Save<T>(string path, T value)
    {
        var temp = $"{path}.tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, path, overwrite: true);
    }
}
