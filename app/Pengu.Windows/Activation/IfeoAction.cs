using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;
using Pengu.Activation;
using Pengu.Logging;

namespace Pengu.Windows.Activation;

/// <summary>
/// Universal-mode activation: writes an IFEO <c>Debugger</c> value under
/// <c>HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\LeagueClientUx.exe</c>
/// pointing at <c>rundll32 "&lt;exe_dir&gt;\core.dll", #6000</c>. When LCUX next
/// launches, IFEO redirects through rundll32 which loads our DLL and calls
/// the <c>_BootstrapEntry</c> export at ordinal 6000.
///
/// <para>Reads use <see cref="Microsoft.Win32.RegistryKey"/> directly — read
/// APIs don't trip AV static analysis, only writes to IFEO do. Writes shell
/// out to <c>cmd /c reg add ... /f</c> with <c>Verb="runas"</c>; the
/// well-known <c>reg.exe</c> binary is system-trusted and bypasses the
/// signature heuristics that flag direct <c>RegSetValueEx</c>-on-IFEO calls.
/// Same approach v1.1.6's WPF loader uses.</para>
/// </summary>
internal sealed class IfeoAction : IActivationAction
{
    private const string IfeoSubKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
    private const string TargetExe  = "LeagueClientUx.exe";
    private const string ValueName  = "Debugger";

    /// <summary>The ordinal of <c>_BootstrapEntry</c> exported from
    /// <c>core.dll</c>. <c>rundll32</c> calls it to start the IFEO bootstrap
    /// sequence. Stable across builds; if it ever changes, both sides
    /// (core's .def file and this constant) need updating in lockstep.</summary>
    private const string BootstrapOrdinal = "#6000";

    /// <summary>HRESULT for "user cancelled the UAC prompt".
    /// <see cref="ProcessStartInfo"/> with <c>Verb="runas"</c> surfaces this
    /// as a <see cref="Win32Exception"/> with <c>NativeErrorCode == 1223</c>
    /// (ERROR_CANCELLED).</summary>
    private const int ErrorCancelled = 1223;

    private readonly string _exeDir;

    public IfeoAction(string exeDir) => _exeDir = exeDir;

    public ActivationMode Mode => ActivationMode.Universal;

    public Task<bool> IsActiveAsync(CancellationToken ct)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"{IfeoSubKey}\{TargetExe}", writable: false);
            if (key is null) return Task.FromResult(false);
            if (key.GetValue(ValueName) is not string debugger || string.IsNullOrWhiteSpace(debugger))
                return Task.FromResult(false);

            // Match "rundll32 <something>" — anything else (a real debugger,
            // a different loader) means we're not the active IFEO.
            if (!debugger.TrimStart().StartsWith("rundll32", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(false);

            // Pull the path out of the first "...". Compare normalised against
            // <exe_dir>\core.dll.
            var quoted = ExtractQuotedPath(debugger);
            if (quoted is null) return Task.FromResult(false);

            var ours = Path.Combine(_exeDir, "core.dll");
            return Task.FromResult(string.Equals(Normalize(quoted), Normalize(ours), StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Log.Warn("IfeoAction.IsActiveAsync failed: {0}", ex.Message);
            return Task.FromResult(false);
        }
    }

    public Task<ActivationResult> SetActiveAsync(bool active, CancellationToken ct)
    {
        var corePath = Path.Combine(_exeDir, "core.dll");

        if (active && corePath.Contains('"'))
        {
            // Embedding a quote in the IFEO value would unbalance the cmd
            // arg quoting and the registry value. Almost impossible in
            // practice (Windows paths don't contain ") but defensive.
            return Task.FromResult(ActivationResult.Fail(
                "Install path contains a literal '\"' character which IFEO can't accept",
                stage: "InvalidPath"));
        }

        // The /d value is `rundll32 "<core.dll>", #6000`. cmd needs the
        // outer "..." around the whole /d arg, plus inner \" so the literal
        // quotes survive into the registry. Same shape as v1.1.6's IFEO.cs.
        var args = active
            ? $"/c reg add \"HKLM\\{IfeoSubKey}\\{TargetExe}\" /v {ValueName} /t REG_SZ /d \"rundll32 \\\"{corePath}\\\", {BootstrapOrdinal}\" /f"
            : $"/c reg delete \"HKLM\\{IfeoSubKey}\\{TargetExe}\" /f";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = args,
                // runas triggers the UAC prompt; cmd inherits admin and
                // reg.exe writes to HKLM. Verb=runas requires UseShellExecute.
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            using var p = Process.Start(psi);
            if (p is null)
                return Task.FromResult(ActivationResult.Fail("Process.Start returned null", stage: "RunElevated"));

            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                Log.Warn("IfeoAction reg exited with code {0}", p.ExitCode);
                return Task.FromResult(ActivationResult.Fail(
                    $"reg exited with code {p.ExitCode}",
                    stage: active ? "SetDebugger" : "DeleteDebugger"));
            }

            Log.Info("IfeoAction set active={0} (core={1})", active, corePath);
            return Task.FromResult(ActivationResult.Success);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            // User clicked No / Cancel on the UAC prompt. Treat as a clean
            // user-rejection rather than an error we have to log loudly.
            return Task.FromResult(ActivationResult.Fail("Elevation cancelled by user", stage: "RunElevated"));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "IfeoAction.SetActiveAsync threw");
            return Task.FromResult(ActivationResult.Fail(ex.Message, stage: "RunElevated"));
        }
    }

    // Daemon callbacks are no-ops for Universal mode — IFEO fires from
    // kernel-side image-load redirection, not from RCS announcements.
    public Task OnSessionCreatedAsync(LcuxSession session, CancellationToken ct) => Task.CompletedTask;
    public Task OnSessionDeletedAsync(LcuxSession session, CancellationToken ct) => Task.CompletedTask;

    /// <summary>Extract the contents of the first <c>"..."</c> pair, or null
    /// if the string isn't quoted. Matches v1.1.6's <c>extract_path</c>.</summary>
    private static string? ExtractQuotedPath(string s)
    {
        int first = s.IndexOf('"');
        if (first < 0) return null;
        int second = s.IndexOf('"', first + 1);
        if (second < 0) return null;
        return s.Substring(first + 1, second - first - 1);
    }

    private static string Normalize(string p) => p.Replace('/', '\\').ToLowerInvariant();
}
