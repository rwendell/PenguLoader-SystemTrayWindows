using System.Diagnostics;
using Pengu.Logging;

namespace Pengu.Native;

/// <summary>
/// Cross-platform shell invocations. Static helpers because the operations
/// are stateless and called from both Pengu (core) and the head's IHost
/// implementations.
///
/// <para>Platform branching at call-time via <see cref="OperatingSystem.IsWindows"/> /
/// <see cref="OperatingSystem.IsMacOS"/>. Both head csprojs target a
/// platform-suffixed TFM, so the inactive branch is effectively dead code at
/// publish time — AOT linker drops it.</para>
/// </summary>
public static class Shell
{
    /// <summary>Open a folder in Explorer (Windows) / Finder (macOS). Creates
    /// the folder if it doesn't exist; failure is logged but not thrown.</summary>
    public static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path.Replace('/', '\\')}\"")
                {
                    UseShellExecute = false,
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo("open", $"\"{path}\"")
                {
                    UseShellExecute = false,
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Shell.OpenFolder failed for {0}", path);
        }
    }

    /// <summary>Reveal a file (highlighted in its folder) — Explorer
    /// <c>/select,</c> on Windows, <c>open -R</c> on macOS.</summary>
    public static void RevealFile(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path.Replace('/', '\\')}\"")
                {
                    UseShellExecute = false,
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo("open", $"-R \"{path}\"")
                {
                    UseShellExecute = false,
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Shell.RevealFile failed for {0}", path);
        }
    }

    /// <summary>Open an external URL in the user's default browser. URLs are
    /// vetted to https:// only at the API layer; this helper just executes.</summary>
    public static void OpenExternal(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // ShellExecute via cmd /c start. Process.Start with
                // UseShellExecute=true would also work but we keep the surface
                // minimal-shell-arg-quoted.
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true,
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo("open", $"\"{url}\"")
                {
                    UseShellExecute = false,
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Shell.OpenExternal failed for {0}", url);
        }
    }
}
