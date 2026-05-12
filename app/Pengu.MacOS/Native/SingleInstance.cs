using AppKit;
using Foundation;
using Pengu;
using Pengu.Logging;

namespace Pengu.MacOS.Native;

/// <summary>
/// Same-machine single-instance check via
/// <see cref="NSRunningApplication.GetRunningApplications(string)"/>.
/// macOS indexes running apps by bundle identifier in a process table that
/// every launch source — manual, Dock, Finder, <c>launchctl bootstrap</c>,
/// SSH login — feeds into. Asking for everything with our CFBundleIdentifier
/// is a single source of truth that works regardless of which session
/// namespace spawned us.
///
/// <para>Earlier attempts at this used a named <see cref="Mutex"/> (POSIX
/// semaphore under the hood) and then a <c>flock</c>'d file. Both have
/// macOS-specific caveats: POSIX semaphores live in per-bootstrap-namespace
/// directories, so a LaunchAgent-spawned process and a Finder-spawned
/// process don't see the same name; and .NET's <see cref="FileStream"/>
/// on macOS sets its own implicit lock on FileShare access that races
/// with raw <c>flock</c> on the same fd. The bundle-ID enumeration sidesteps
/// both problems.</para>
///
/// <para>Race window: between the check and the rest of startup, a second
/// process could squeak in. That's acceptable for our use cases (manual
/// double-click, LaunchAgent bootstrap, login dispatch) — those launches
/// are well-separated in time. If two processes race within the same
/// millisecond and both observe "no peer", they'll both start; the
/// second will be reaped when the user notices and quits one.</para>
/// </summary>
internal static class SingleInstance
{
    private const string BundleId = "com.pengu.lol";

    /// <summary>
    /// Try to acquire the single-instance slot. Returns true if no other
    /// Pengu process is running; false if another holds it.
    /// </summary>
    public static bool TryAcquire()
    {
        int selfPid = Environment.ProcessId;
        var others = NSRunningApplication
            .GetRunningApplications(BundleId)
            .Where(app => app.ProcessIdentifier != selfPid)
            .ToList();

        if (others.Count == 0)
        {
            Log.Info("Single-instance acquired (no peer with bundle {0})", BundleId);
            return true;
        }

        Log.Info("Another Pengu instance is running (pid={0})", others[0].ProcessIdentifier);

        // --silent path: this instance was spawned by launchctl bootstrap at
        // LaunchAgent-enable time, or by a redundant login dispatch. The user
        // didn't ask for a window — exit quietly without disturbing the
        // running instance. Without this, enabling "Start at login" while
        // Pengu is open would make the running window jump to the front.
        if (AppEnv.Silent)
        {
            Log.Info("--silent: exiting quietly");
            return false;
        }

        // Wake the running instance so its window respawns even after the user
        // closed it earlier. NSRunningApplication.Activate fires
        // applicationShouldHandleReopen on the receiver, which is wired in
        // AppDelegate to call MacOSHost.BringMainWindowToFront.
        try
        {
            others[0].Activate(NSApplicationActivationOptions.ActivateAllWindows);
        }
        catch (Exception ex)
        {
            Log.Warn("Failed to activate running Pengu instance: {0}", ex.Message);
        }

        return false;
    }

    /// <summary>No-op: nothing to release with the bundle-ID approach.</summary>
    public static void Release() { }
}
