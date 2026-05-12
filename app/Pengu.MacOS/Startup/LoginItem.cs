using System.Diagnostics;
using System.Runtime.InteropServices;
using Pengu.Logging;

namespace Pengu.MacOS.Startup;

/// <summary>
/// Login-time auto-start via macOS Login Items (System Events). This is the
/// same mechanism users see in System Settings → General → Login Items, and
/// what the Tauri loader used via the <c>auto-launch</c> crate (with
/// <c>use_launch_agent=false</c>).
///
/// <para>Previously implemented as a <c>~/Library/LaunchAgents/com.pengu.lol.plist</c>
/// + <c>launchctl bootstrap</c>, but bootstrap immediately spawns the agent
/// (because <c>RunAtLoad=true</c>) when the user enables it from a running
/// app — producing a duplicate instance with a second window and tray.
/// Login Items don't have that problem: macOS only launches them at the
/// next GUI login.</para>
///
/// <para>Counterpart of Pengu.Windows's <c>HKCU\...\Run</c> registry entry.</para>
/// </summary>
internal static partial class LoginItem
{
    private const string ItemName = "Pengu";

    /// <summary>
    /// Legacy LaunchAgent plist path. Cleaned up at every Enable/Disable so
    /// users upgrading from the plist-based implementation don't end up with
    /// both mechanisms active.
    /// </summary>
    private static readonly string LegacyPlistPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", "com.pengu.lol.plist");

    public static bool IsEnabled()
    {
        var (rc, stdout, _) = RunOsa(
            $"tell application \"System Events\" to (exists login item \"{ItemName}\")");
        return rc == 0 && stdout.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public static void Enable(string appBundlePath)
    {
        if (string.IsNullOrEmpty(appBundlePath))
            throw new ArgumentException("appBundlePath is required", nameof(appBundlePath));
        if (!Directory.Exists(appBundlePath))
            throw new InvalidOperationException($"Cannot enable startup: {appBundlePath} is not a directory");

        CleanupLegacyPlist();

        // Replace any existing entry — the bundle path may have changed
        // between sessions (user moved Pengu.app, dev rebuilds in a new bin/).
        DeleteLoginItem();

        var script =
            "tell application \"System Events\" to make new login item at end of login items "
            + $"with properties {{name:\"{ItemName}\", path:\"{EscapeApple(appBundlePath)}\", hidden:false}}";

        var (rc, _, stderr) = RunOsa(script);
        if (rc != 0)
            throw new InvalidOperationException($"Failed to add login item: {stderr.Trim()}");

        Log.Info("Login Item enabled: {0} (takes effect at next login)", appBundlePath);
    }

    public static void Disable()
    {
        CleanupLegacyPlist();
        DeleteLoginItem();
        Log.Info("Login Item disabled");
    }

    private static void DeleteLoginItem()
    {
        // Best-effort. AppleScript raises an error if the item doesn't exist,
        // which we don't care about — we just want it gone.
        RunOsa($"tell application \"System Events\" to delete login item \"{ItemName}\"");
    }

    private static void CleanupLegacyPlist()
    {
        if (!File.Exists(LegacyPlistPath)) return;

        // bootout fails if not loaded; that's fine — we just want to ensure
        // it isn't loaded, by whatever path.
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = "launchctl",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            };
            psi.ArgumentList.Add("bootout");
            psi.ArgumentList.Add($"gui/{geteuid()}/com.pengu.lol");
            using var p = Process.Start(psi);
            p?.WaitForExit();
        }
        catch { /* ignore */ }

        try { File.Delete(LegacyPlistPath); }
        catch (Exception ex)
        {
            Log.Warn("Failed to delete legacy plist {0}: {1}", LegacyPlistPath, ex.Message);
        }
    }

    private static (int rc, string stdout, string stderr) RunOsa(string script)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = "osascript",
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };
        psi.ArgumentList.Add("-e");
        psi.ArgumentList.Add(script);

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start osascript");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, stdout, stderr);
    }

    /// <summary>Escape backslash and double-quote for AppleScript string literals.</summary>
    private static string EscapeApple(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    [LibraryImport("/usr/lib/libSystem.dylib")]
    private static partial uint geteuid();
}
