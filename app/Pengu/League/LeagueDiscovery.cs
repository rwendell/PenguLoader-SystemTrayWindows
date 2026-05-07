using System.Text.Json;
using Pengu.Logging;

namespace Pengu.League;

/// <summary>
/// League of Legends install discovery via the Riot Client manifest.
/// Windows-only logic; on macOS the equivalent path is in
/// <c>/Users/Shared/Riot Games/</c> but the hub doesn't surface a picker
/// for it (macOS uses OnDemand exclusively and finds the install via the
/// RCS WAMP daemon).
/// </summary>
public static class LeagueDiscovery
{
    /// <summary>
    /// Walk <c>C:\ProgramData\Riot Games\RiotClientInstalls.json</c> looking
    /// for an LCUX install. Returns null if RCS isn't installed, the manifest
    /// is unreadable, or no LeagueClientUx.exe is found in any candidate
    /// directory.
    ///
    /// <para>Search order matches the v1.1.6/Tauri implementation:</para>
    /// <list type="number">
    ///   <item><description><c>rc_live</c> -> sibling <c>League of Legends</c></description></item>
    ///   <item><description><c>rc_default</c> -> sibling <c>League of Legends</c></description></item>
    ///   <item><description>Same two for <c>League of Legends (PBE)</c></description></item>
    ///   <item><description><c>associated_client</c> map keys (final fallback)</description></item>
    /// </list>
    /// </summary>
    public static string? FindInstall()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        const string manifestPath = @"C:\ProgramData\Riot Games\RiotClientInstalls.json";
        if (!File.Exists(manifestPath))
        {
            Log.Debug("LeagueDiscovery: RCS manifest not found at {0}", manifestPath);
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
            var root = doc.RootElement;

            // rc_live / rc_default point at RiotClientServices.exe; LoL lives
            // in a sibling of that exe's parent.
            string? rcParent = null;
            if (root.TryGetProperty("rc_live", out var rcLive) && rcLive.ValueKind == JsonValueKind.String)
                rcParent = Path.GetDirectoryName(Path.GetDirectoryName(rcLive.GetString()));
            else if (root.TryGetProperty("rc_default", out var rcDefault) && rcDefault.ValueKind == JsonValueKind.String)
                rcParent = Path.GetDirectoryName(Path.GetDirectoryName(rcDefault.GetString()));

            if (!string.IsNullOrEmpty(rcParent))
            {
                var live = Path.Combine(rcParent, "League of Legends");
                if (ValidateDir(live)) return live;

                var pbe = Path.Combine(rcParent, "League of Legends (PBE)");
                if (ValidateDir(pbe)) return pbe;
            }

            // associated_client: object whose KEYS are install dirs.
            if (root.TryGetProperty("associated_client", out var ac) && ac.ValueKind == JsonValueKind.Object)
            {
                string? live = null, pbe = null;
                foreach (var prop in ac.EnumerateObject())
                {
                    var dir = prop.Name.TrimEnd('\\', '/');
                    if (dir.Contains("(pbe)", StringComparison.OrdinalIgnoreCase))
                        pbe = dir;
                    else
                        live = dir;
                }
                if (live is not null && ValidateDir(live)) return live;
                if (pbe is not null && ValidateDir(pbe)) return pbe;
            }
        }
        catch (Exception ex)
        {
            Log.Warn("LeagueDiscovery: failed to read manifest ({0})", ex.Message);
        }

        return null;
    }

    /// <summary>
    /// True if <paramref name="dir"/> looks like a LoL install (i.e.
    /// contains <c>LeagueClientUx.exe</c>). Empty / null dirs return false.
    /// </summary>
    public static bool ValidateDir(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return false;
        try
        {
            return File.Exists(Path.Combine(dir, "LeagueClientUx.exe"));
        }
        catch
        {
            return false;
        }
    }
}
