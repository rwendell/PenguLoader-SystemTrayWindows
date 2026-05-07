using Microsoft.Win32;
using Pengu.Logging;

namespace Pengu.Windows.Native;

/// <summary>
/// Auto-launch on user login via the per-user Run key.
/// HKCU only (never HKLM): no admin needed, no risk to other users.
///
/// <para>The value's name is stable across versions so toggling matches
/// existing entries written by older builds. The value's data is the
/// quoted full path to the running exe.</para>
/// </summary>
public static class StartupRegistry
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Pengu";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(ValueName) as string;
            if (string.IsNullOrEmpty(value)) return false;

            // Loose match: present + non-empty is "enabled". We don't compare
            // the path string because users may have moved the exe; the entry
            // stays valid (Run key tolerates dead entries by silently skipping).
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn("StartupRegistry.IsEnabled failed: {0}", ex.Message);
            return false;
        }
    }

    public static void SetEnabled(bool enabled, string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException($"failed to open {RunKeyPath}");

            if (enabled)
            {
                // Quote the path so spaces in the install dir don't split argv.
                key.SetValue(ValueName, $"\"{exePath}\"", RegistryValueKind.String);
                Log.Info("StartupRegistry: enabled (exe={0})", exePath);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                Log.Info("StartupRegistry: disabled");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "StartupRegistry.SetEnabled({0}) failed", enabled);
        }
    }
}
