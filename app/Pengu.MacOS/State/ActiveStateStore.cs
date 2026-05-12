using Pengu.Logging;

namespace Pengu.MacOS.State;

/// <summary>
/// Persisted activation toggle: was the watcher running when Pengu last
/// shut down? Read at startup to restore the user's last "Activate" state
/// so they don't have to re-toggle on every launch.
///
/// <para>Lives at <c>&lt;DataRoot&gt;/active</c> as plain text — <c>"1"</c>
/// for active, <c>"0"</c> for inactive. Same filename + format as the Tauri
/// loader (see <c>packages/hub/src-tauri/src/macos/mod.rs</c>) so users
/// upgrading from Tauri keep their last toggle state without migration.</para>
///
/// <para>Windows doesn't need this: IFEO is the persistence (its registry
/// entry IS "are we active") and the loader simply doesn't run when
/// inactive. macOS Universal mode requires Pengu's own daemon to poll
/// <c>proc_listpids</c>, so the bit needs a separate file.</para>
/// </summary>
internal static class ActiveStateStore
{
    public static bool TryLoad(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        try
        {
            return File.ReadAllText(filePath).Trim() == "1";
        }
        catch (Exception ex)
        {
            Log.Warn("ActiveStateStore: failed to read {0} ({1}); defaulting to inactive", filePath, ex.Message);
            return false;
        }
    }

    public static void Save(string filePath, bool active)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(filePath, active ? "1" : "0");
            Log.Debug("ActiveStateStore: saved {0} = {1}", filePath, active);
        }
        catch (Exception ex)
        {
            Log.Warn("ActiveStateStore: failed to save {0} ({1})", filePath, ex.Message);
        }
    }
}
