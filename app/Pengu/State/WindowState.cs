using System.Text.Json;
using System.Text.Json.Serialization;
using Pengu.Logging;

namespace Pengu.State;

/// <summary>
/// Persisted window placement: position, size, and maximize state.
/// Read at startup to restore the window where the user last left it;
/// written when the window closes.
///
/// <para>Lives at <c>&lt;per-user-data&gt;/window.json</c> (Windows:
/// <c>%LOCALAPPDATA%\.pengu\window.json</c>). Per-user — multiple users on
/// the same machine each remember their own preferred size, even though
/// activation state is machine-wide via ProgramData.</para>
/// </summary>
public sealed record WindowState(
    [property: JsonPropertyName("x")]         int X,
    [property: JsonPropertyName("y")]         int Y,
    [property: JsonPropertyName("width")]     int Width,
    [property: JsonPropertyName("height")]    int Height,
    [property: JsonPropertyName("maximized")] bool Maximized = false);

/// <summary>
/// Read/write helpers for <see cref="WindowState"/>. Failures are silent
/// (returns null on read; logs and swallows on write) — window state is
/// best-effort and shouldn't block the host from starting or shutting down.
/// </summary>
public static class WindowStateStore
{
    public static WindowState? TryLoad(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var state = JsonSerializer.Deserialize(bytes, PenguJsonContext.Default.WindowState);
            return state;
        }
        catch (Exception ex)
        {
            Log.Warn("WindowStateStore: failed to read {0} ({1}); using defaults", filePath, ex.Message);
            return null;
        }
    }

    public static void Save(string filePath, WindowState state)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var bytes = JsonSerializer.SerializeToUtf8Bytes(state, PenguJsonContext.Default.WindowState);
            File.WriteAllBytes(filePath, bytes);
            Log.Debug("WindowStateStore: saved {0} ({1}x{2} at {3},{4} max={5})",
                filePath, state.Width, state.Height, state.X, state.Y, state.Maximized);
        }
        catch (Exception ex)
        {
            Log.Warn("WindowStateStore: failed to save {0} ({1})", filePath, ex.Message);
        }
    }
}
