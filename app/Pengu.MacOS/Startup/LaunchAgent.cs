using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using Pengu.Logging;

namespace Pengu.MacOS.Startup;

/// <summary>
/// Login-time auto-start via a per-user LaunchAgent plist. Unlike System
/// Events Login Items, LaunchAgents support <c>ProgramArguments</c>, which
/// lets us pass <c>--silent</c> through to <c>AppEnv.ParseCommandLine</c>
/// — the canonical signal for "boot into tray, no main window".
///
/// <para>Counterpart of Pengu.Windows's <c>HKCU\...\Run</c> registry entry.
/// Replaces our earlier System-Events Login Item implementation, which had
/// no arg mechanism and forced us into a fragile Apple-event detection
/// path to recognise login-time launches.</para>
///
/// <para>Enable-time spawn: <c>launchctl bootstrap</c> immediately runs the
/// agent because <c>RunAtLoad=true</c>. This is harmless here because the
/// spawned instance hits <c>SingleInstance</c> and, seeing the
/// <c>--silent</c> flag, exits without activating the running app.</para>
/// </summary>
internal static partial class LaunchAgent
{
    private const string Label = "com.pengu.lol";

    private static readonly string PlistPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", $"{Label}.plist");

    /// <summary>
    /// Legacy AppleScript-Login-Item name. Cleaned up on every Enable/Disable
    /// so users migrating from the System-Events implementation don't end up
    /// with both mechanisms active.
    /// </summary>
    private const string LegacyLoginItemName = "Pengu";

    public static bool IsEnabled() => File.Exists(PlistPath);

    public static void Enable(string penguBinaryPath)
    {
        if (string.IsNullOrEmpty(penguBinaryPath))
            throw new ArgumentException("penguBinaryPath is required", nameof(penguBinaryPath));
        if (!File.Exists(penguBinaryPath))
            throw new InvalidOperationException($"Cannot enable startup: {penguBinaryPath} does not exist");

        CleanupLegacyLoginItem();

        // Drop a fresh plist — the binary path can change between sessions
        // (user moves Pengu.app, dev rebuilds in a new bin/).
        Directory.CreateDirectory(Path.GetDirectoryName(PlistPath)!);
        File.WriteAllText(PlistPath, BuildPlist(penguBinaryPath), Encoding.UTF8);

        // If the agent is already bootstrapped (e.g. user toggled this off
        // then on inside the same session), bootout first so bootstrap
        // re-reads the new plist instead of erroring "already loaded".
        Bootout();
        Bootstrap();

        Log.Info("LaunchAgent enabled at {0} for binary {1}", PlistPath, penguBinaryPath);
    }

    public static void Disable()
    {
        CleanupLegacyLoginItem();
        Bootout();
        if (File.Exists(PlistPath))
        {
            try { File.Delete(PlistPath); }
            catch (Exception ex)
            {
                Log.Warn("Failed to delete {0}: {1}", PlistPath, ex.Message);
            }
        }
        Log.Info("LaunchAgent disabled");
    }

    private static string BuildPlist(string penguBinaryPath)
    {
        // Hand-crafted plist XML — System.Xml + a tiny PLIST DTD would work
        // but PLIST is permissive and the doc is small enough that string
        // composition is clearer than a writer. Both values are
        // XML-escaped via SecurityElement.Escape.
        string esc(string s) => System.Security.SecurityElement.Escape(s)!;
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
              <key>Label</key>
              <string>{esc(Label)}</string>
              <key>ProgramArguments</key>
              <array>
                <string>{esc(penguBinaryPath)}</string>
                <string>--silent</string>
              </array>
              <key>RunAtLoad</key>
              <true/>
              <key>ProcessType</key>
              <string>Interactive</string>
            </dict>
            </plist>
            """;
    }

    private static void Bootstrap()
    {
        RunLaunchctl("bootstrap", $"gui/{geteuid()}", PlistPath);
    }

    private static void Bootout()
    {
        // bootout returns non-zero if the agent isn't loaded, which is fine —
        // we just want "not loaded" as the post-state.
        RunLaunchctl("bootout", $"gui/{geteuid()}/{Label}");
    }

    private static void CleanupLegacyLoginItem()
    {
        // Remove any leftover entry from our previous System-Events
        // implementation. Best-effort; AppleScript errors if the item
        // doesn't exist, which is the expected case for fresh installs.
        try
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
            psi.ArgumentList.Add(
                $"tell application \"System Events\" to delete login item \"{LegacyLoginItemName}\"");
            using var p = Process.Start(psi);
            p?.WaitForExit();
        }
        catch { /* ignore */ }
    }

    private static (int rc, string stderr) RunLaunchctl(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = "launchctl",
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start launchctl");
        p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, stderr);
    }

    [LibraryImport("/usr/lib/libSystem.dylib")]
    private static partial uint geteuid();
}
